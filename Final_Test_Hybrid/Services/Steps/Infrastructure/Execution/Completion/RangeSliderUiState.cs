using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

/// <summary>
/// Состояние UI для отображения RangeSlider в тестовых шагах.
/// Singleton сервис, управляющий отображением слайдеров для 4 колонок.
/// Автоматически скрывает слайдеры при сбросах PLC.
/// </summary>
public class RangeSliderUiState
{
    private const int ColumnCount = 4;
    private const int DefaultMin = 0;
    private const int DefaultMax = 100;
    private const int ThrottleIntervalMs = 100;

    private readonly Lock _lock = new();
    private readonly PlcResetCoordinator _plcResetCoordinator;
    private readonly IErrorCoordinator _errorCoordinator;
    private readonly OpcUaSubscription _opcUaSubscription;
    private readonly Dictionary<int, RangeSliderState> _states = new();

    private int _sessionCounter;
    private long _lastNotifyTicks;
    private int _pendingNotify;
    private bool _debugMode;
    private string? _title;

    public RangeSliderUiState(
        PlcResetCoordinator plcResetCoordinator,
        IErrorCoordinator errorCoordinator,
        OpcUaSubscription opcUaSubscription)
    {
        _plcResetCoordinator = plcResetCoordinator;
        _errorCoordinator = errorCoordinator;
        _opcUaSubscription = opcUaSubscription;

        _plcResetCoordinator.OnForceStop += HideAll;
        _errorCoordinator.OnReset += HideAll;
    }

    /// <summary>
    /// Есть ли активные слайдеры для отображения.
    /// В Debug Mode всегда возвращает true.
    /// </summary>
    public bool HasActiveSliders
    {
        get
        {
            if (_debugMode)
            {
                return true;
            }

            lock (_lock)
            {
                return _states.Count > 0;
            }
        }
    }

    /// <summary>
    /// Заголовок для отображения над слайдерами.
    /// </summary>
    public string? Title => _debugMode ? "Настройки параметров газа в максимальном режиме" : _title;

    /// <summary>
    /// Устанавливает заголовок для отображения.
    /// </summary>
    public void SetTitle(string? title)
    {
        _title = title;
        ThrottledNotify();
    }

    /// <summary>
    /// Включить режим отладки с тестовыми данными.
    /// </summary>
    public void EnableDebugMode()
    {
        _debugMode = true;
        ThrottledNotify();
    }

    /// <summary>
    /// Выключить режим отладки.
    /// </summary>
    public void DisableDebugMode()
    {
        _debugMode = false;
        ThrottledNotify();
    }

    /// <summary>
    /// Событие изменения состояния (для обновления UI).
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// Получает состояния всех активных слайдеров.
    /// В Debug Mode возвращает тестовые данные.
    /// </summary>
    public IReadOnlyDictionary<int, RangeSliderDisplayData> GetActiveSliders()
    {
        if (_debugMode)
        {
            // Давление: Step=0.1, TickCount = (9.9-7.9)/0.1 = 20
            // Расход: Step=1, TickCount = (20-0)/1 = 20
            return new Dictionary<int, RangeSliderDisplayData>
            {
                [0] = new("Давление газа", "мбар", 7.9, 9.9, 8.9, 8.4, 9.4, 20),
                [1] = new("Расход воды", "л/мин", 0, 20, 12, 8, 16, 20)
            };
        }

        lock (_lock)
        {
            return _states.ToDictionary(
                kvp => kvp.Key,
                kvp => new RangeSliderDisplayData(
                    kvp.Value.Config.Label,
                    kvp.Value.Config.Unit,
                    kvp.Value.Min,
                    kvp.Value.Max,
                    kvp.Value.Value,
                    kvp.Value.GreenZoneStart,
                    kvp.Value.GreenZoneEnd,
                    kvp.Value.TickCount));
        }
    }

    /// <summary>
    /// Показать слайдер для указанной колонки.
    /// </summary>
    public async Task ShowAsync(int columnIndex, RangeSliderConfig config, CancellationToken ct)
    {
        ValidateColumnIndex(columnIndex);

        await HideAsync(columnIndex, ct).ConfigureAwait(false);

        var sessionId = Interlocked.Increment(ref _sessionCounter);
        var limits = CalculateLimits(config);
        Func<object?, Task> callback = value => HandleValueChanged(columnIndex, sessionId, value);

        // Подписываемся ДО записи состояния
        await _opcUaSubscription.SubscribeAsync(config.ValueTag, callback, ct).ConfigureAwait(false);

        // Проверяем отмену после await — если был reset, откатываем подписку
        if (ct.IsCancellationRequested)
        {
            await _opcUaSubscription.UnsubscribeAsync(config.ValueTag, callback, removeTag: false, CancellationToken.None).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
        }

        lock (_lock)
        {
            var step = config.Step > 0 ? config.Step : 1;
            var tickCount = (int)Math.Round((limits.Max - limits.Min) / step);
            _states[columnIndex] = new RangeSliderState
            {
                SessionId = sessionId,
                Config = config,
                Callback = callback,
                Min = limits.Min,
                Max = limits.Max,
                GreenZoneStart = config.GreenZoneStart,
                GreenZoneEnd = config.GreenZoneEnd,
                Value = limits.Min,
                TickCount = tickCount > 0 ? tickCount : 10
            };
        }

        ThrottledNotify();
    }

    /// <summary>
    /// Скрыть слайдер для указанной колонки.
    /// </summary>
    public async Task HideAsync(int columnIndex, CancellationToken ct = default)
    {
        ValidateColumnIndex(columnIndex);

        Func<object?, Task>? callback;
        string? valueTag;

        lock (_lock)
        {
            if (!_states.TryGetValue(columnIndex, out var state))
            {
                return;
            }

            callback = state.Callback;
            valueTag = state.Config.ValueTag;
            _states.Remove(columnIndex);
        }

        if (callback != null && valueTag != null)
        {
            await _opcUaSubscription.UnsubscribeAsync(valueTag, callback, removeTag: false, ct).ConfigureAwait(false);
        }

        ThrottledNotify();
    }

    /// <summary>
    /// Скрыть все слайдеры (вызывается при сбросах).
    /// </summary>
    public void HideAll()
    {
        List<(string ValueTag, Func<object?, Task> Callback)> toUnsubscribe;

        lock (_lock)
        {
            toUnsubscribe = _states
                .Select(kvp => (kvp.Value.Config.ValueTag, kvp.Value.Callback))
                .ToList();
            _states.Clear();
            _title = null;
        }

        foreach (var (valueTag, callback) in toUnsubscribe)
        {
            _ = _opcUaSubscription.UnsubscribeAsync(valueTag, callback, removeTag: false, CancellationToken.None);
        }

        ThrottledNotify();
    }

    private static void ValidateColumnIndex(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex), $"Column index must be between 0 and {ColumnCount - 1}");
        }
    }

    /// <summary>
    /// Вычисляет Min/Max шкалы из конфигурации.
    /// </summary>
    private static (double Min, double Max) CalculateLimits(RangeSliderConfig config)
    {
        var greenWidth = config.GreenZoneEnd - config.GreenZoneStart;
        var margin = greenWidth * 0.5;

        var min = config.MinValue ?? (config.GreenZoneStart - margin);
        var max = config.MaxValue ?? (config.GreenZoneEnd + margin);

        if (max <= min)
        {
            max = min + DefaultMax;
        }

        return (min, max);
    }

    private Task HandleValueChanged(int columnIndex, int sessionId, object? value)
    {
        lock (_lock)
        {
            if (!_states.TryGetValue(columnIndex, out var state))
            {
                return Task.CompletedTask;
            }

            if (state.SessionId != sessionId)
            {
                return Task.CompletedTask;
            }

            state.Value = ConvertToDouble(value);
        }

        ThrottledNotify();
        return Task.CompletedTask;
    }

    private static double ConvertToDouble(object? value)
    {
        if (value == null)
        {
            return 0;
        }

        try
        {
            return Convert.ToDouble(value);
        }
        catch
        {
            return 0;
        }
    }

    private void ThrottledNotify()
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        var lastTicks = Interlocked.Read(ref _lastNotifyTicks);
        var elapsedMs = (nowTicks - lastTicks) / TimeSpan.TicksPerMillisecond;

        if (elapsedMs >= ThrottleIntervalMs)
        {
            Interlocked.Exchange(ref _lastNotifyTicks, nowTicks);
            OnStateChanged?.Invoke();
            return;
        }

        // Планируем отложенное уведомление, если ещё нет pending
        if (Interlocked.CompareExchange(ref _pendingNotify, 1, 0) == 0)
        {
            var delay = ThrottleIntervalMs - (int)elapsedMs;
            _ = Task.Delay(delay).ContinueWith(_ =>
            {
                Interlocked.Exchange(ref _pendingNotify, 0);
                Interlocked.Exchange(ref _lastNotifyTicks, DateTime.UtcNow.Ticks);
                OnStateChanged?.Invoke();
            }, TaskScheduler.Default);
        }
    }

    /// <summary>
    /// Внутреннее состояние слайдера для одной колонки.
    /// </summary>
    private sealed class RangeSliderState
    {
        public required int SessionId { get; init; }
        public required RangeSliderConfig Config { get; init; }
        public required Func<object?, Task> Callback { get; init; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double GreenZoneStart { get; set; }
        public double GreenZoneEnd { get; set; }
        public double Value { get; set; }
        public int TickCount { get; set; }
    }
}

/// <summary>
/// Данные для отображения слайдера в UI.
/// </summary>
public record RangeSliderDisplayData(
    string Label,
    string? Unit,
    double Min,
    double Max,
    double Value,
    double GreenZoneStart,
    double GreenZoneEnd,
    int TickCount);
