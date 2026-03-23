using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa.Connection;

namespace Final_Test_Hybrid.Services.OpcUa.Heartbeat;

/// <summary>
/// Периодически взводит флаг HMI heartbeat для контроля связи с PLC.
/// PLC сбрасывает флаг каждые 5 сек, если HMI не взведёт его вовремя - PLC выдаёт ошибку.
/// </summary>
public class HmiHeartbeatService : IDisposable
{
    private readonly OpcUaConnectionState _connectionState;
    private readonly OpcUaTagService _tagService;
    private readonly HmiHeartbeatHealthMonitor _healthMonitor;
    private readonly DualLogger<HmiHeartbeatService> _logger;
    private readonly Lock _lock = new();
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private Task? _heartbeatTask;
    private bool _isRunning;
    private bool _disposed;
    private const int HeartbeatIntervalMs = 2000;

    public HmiHeartbeatService(
        OpcUaConnectionState connectionState,
        OpcUaTagService tagService,
        HmiHeartbeatHealthMonitor healthMonitor,
        DualLogger<HmiHeartbeatService> logger)
    {
        _connectionState = connectionState;
        _tagService = tagService;
        _healthMonitor = healthMonitor;
        _logger = logger;

        connectionState.ConnectionStateChanged += OnConnectionStateChanged;

        if (connectionState.IsConnected)
        {
            StartHeartbeat();
        }
    }

    private void OnConnectionStateChanged(bool isConnected)
    {
        if (isConnected)
        {
            StartHeartbeat();
        }
        else
        {
            StopHeartbeat();
        }
    }

    private void StartHeartbeat()
    {
        lock (_lock)
        {
            if (_isRunning || _disposed)
            {
                return;
            }
            _isRunning = true;
            _healthMonitor.MarkMonitoringStarted();
            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(HeartbeatIntervalMs));
            _heartbeatTask = HeartbeatLoopAsync(_timer, _cts.Token);
            _logger.LogInformation("HMI Heartbeat запущен (интервал: {Interval} мс)", HeartbeatIntervalMs);
        }
    }

    private async Task HeartbeatLoopAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            await WriteHeartbeatAsync(ct);
            await RunPeriodicWritesAsync(timer, ct);
        }
        catch (OperationCanceledException)
        {
            // Нормальное завершение
        }
    }

    private async Task RunPeriodicWritesAsync(PeriodicTimer timer, CancellationToken ct)
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            await WriteHeartbeatAsync(ct);
        }
    }

    private async Task WriteHeartbeatAsync(CancellationToken ct)
    {
        try
        {
            var result = await _tagService.WriteAsync(BaseTags.HmiHeartbeat, true, ct, silent: true);
            if (result.Success)
            {
                LogHeartbeatTransition(_healthMonitor.RecordWriteSuccess());
                return;
            }

            LogHeartbeatTransition(_healthMonitor.RecordWriteFailure(result.Error ?? "Неизвестная ошибка записи"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogHeartbeatTransition(_healthMonitor.RecordWriteFailure($"Exception: {ex.Message}"));
        }
    }

    private void StopHeartbeat()
    {
        CancellationTokenSource? oldCts;
        PeriodicTimer? oldTimer;
        Task? oldTask;

        lock (_lock)
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;

            oldCts = _cts;
            oldTimer = _timer;
            oldTask = _heartbeatTask;

            _cts = null;
            _timer = null;
            _heartbeatTask = null;
        }

        _healthMonitor.MarkMonitoringStopped();
        oldCts?.Cancel();

        if (oldTask != null)
        {
            try
            {
                oldTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Игнорируем
            }
        }

        oldTimer?.Dispose();
        oldCts?.Dispose();

        _logger.LogInformation("HMI Heartbeat остановлен");
    }

    private void LogHeartbeatTransition(HmiHeartbeatHealthTransition? transition)
    {
        if (transition is null)
        {
            return;
        }

        if (transition.Value.CurrentState == HeartbeatHealthState.Healthy)
        {
            _logger.LogInformation(
                "Heartbeat восстановлен. Предыдущее состояние={PreviousState}. Возраст heartbeat, мс={HeartbeatAgeMs}. Результат последней записи={LastHeartbeatWriteResult}",
                transition.Value.PreviousState,
                transition.Value.AgeMs?.ToString() ?? "n/a",
                transition.Value.LastWriteResult);
            return;
        }

        if (transition.Value.CurrentState == HeartbeatHealthState.WriteFailed)
        {
            _logger.LogWarning(
                "Heartbeat: ошибка записи. Предыдущее состояние={PreviousState}. Возраст heartbeat, мс={HeartbeatAgeMs}. Результат последней записи={LastHeartbeatWriteResult}",
                transition.Value.PreviousState,
                transition.Value.AgeMs?.ToString() ?? "n/a",
                transition.Value.LastWriteResult);
            return;
        }

        _logger.LogWarning(
            "Heartbeat: превышен допустимый интервал. Предыдущее состояние={PreviousState}. Возраст heartbeat, мс={HeartbeatAgeMs}. Результат последней записи={LastHeartbeatWriteResult}",
            transition.Value.PreviousState,
            transition.Value.AgeMs?.ToString() ?? "n/a",
            transition.Value.LastWriteResult);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _connectionState.ConnectionStateChanged -= OnConnectionStateChanged;
        StopHeartbeat();
        GC.SuppressFinalize(this);
    }
}
