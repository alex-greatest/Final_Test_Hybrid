using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;

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
    private readonly IRecipeProvider _recipeProvider;
    private readonly Dictionary<int, RangeSliderState> _states = new();

    private int _sessionCounter;
    private long _lastNotifyTicks;
    private int _pendingNotify;

    public RangeSliderUiState(
        PlcResetCoordinator plcResetCoordinator,
        IErrorCoordinator errorCoordinator,
        OpcUaSubscription opcUaSubscription,
        IRecipeProvider recipeProvider)
    {
        _plcResetCoordinator = plcResetCoordinator;
        _errorCoordinator = errorCoordinator;
        _opcUaSubscription = opcUaSubscription;
        _recipeProvider = recipeProvider;

        _plcResetCoordinator.OnForceStop += HideAll;
        _errorCoordinator.OnReset += HideAll;
    }

    /// <summary>
    /// Есть ли активные слайдеры для отображения.
    /// </summary>
    public bool HasActiveSliders
    {
        get
        {
            lock (_lock)
            {
                return _states.Count > 0;
            }
        }
    }

    /// <summary>
    /// Событие изменения состояния (для обновления UI).
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// Получает состояния всех активных слайдеров.
    /// </summary>
    public IReadOnlyDictionary<int, RangeSliderDisplayData> GetActiveSliders()
    {
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
                    kvp.Value.Config.TickCount));
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
        var limits = LoadLimitsFromRecipes(config);
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
            _states[columnIndex] = new RangeSliderState
            {
                SessionId = sessionId,
                Config = config,
                Callback = callback,
                Min = limits.Min,
                Max = limits.Max,
                GreenZoneStart = limits.GreenStart,
                GreenZoneEnd = limits.GreenEnd,
                Value = limits.Min
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

    private (double Min, double Max, double GreenStart, double GreenEnd) LoadLimitsFromRecipes(RangeSliderConfig config)
    {
        var min = _recipeProvider.GetValue<double>(config.MinRecipeAddress) ?? DefaultMin;
        var max = _recipeProvider.GetValue<double>(config.MaxRecipeAddress) ?? DefaultMax;
        var greenStart = _recipeProvider.GetValue<double>(config.GreenStartRecipeAddress) ?? min;
        var greenEnd = _recipeProvider.GetValue<double>(config.GreenEndRecipeAddress) ?? max;

        if (max <= min)
        {
            max = min + DefaultMax;
        }

        return (min, max, greenStart, greenEnd);
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
