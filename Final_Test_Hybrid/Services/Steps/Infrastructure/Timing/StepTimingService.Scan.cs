namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

public partial class StepTimingService
{
    public void StartScanTiming(string name, string description)
    {
        lock (_lock)
        {
            _scanPausedByGlobalPauseId = null;
            _scanStepInfo = (name, description);
            _scanState.Start(name, description);
        }
        StartTimer();
        OnChanged?.Invoke();
    }

    public void StopScanTiming()
    {
        lock (_lock)
        {
            if (!_scanState.IsActive)
            {
                return;
            }
            _scanState.Pause();
            _scanPausedByGlobalPauseId = null;
        }
        UpdateTimerState();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Сбрасывает таймер сканирования, если он остановлен.
    /// Если таймер уже работает (например, после ошибки scan step) - ничего не делает.
    /// </summary>
    public void ResetScanTiming()
    {
        bool restarted;
        lock (_lock)
        {
            _scanPausedByGlobalPauseId = null;
            restarted = TryRestartScanStateLocked();
        }

        if (!restarted)
            return;

        StartTimer();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Перезапускает scan state. Вызывать только под _lock.
    /// </summary>
    private bool TryRestartScanStateLocked()
    {
        if (_scanState.IsActive)
        {
            if (_scanState.IsRunning)
                return false; // Таймер уже работает - не трогаем

            _scanState.Reset();
            return true;
        }

        if (!_scanStepInfo.HasValue)
            return false;

        _scanState.Start(_scanStepInfo.Value.Name, _scanStepInfo.Value.Description);
        return true;
    }
}
