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

    public void ResetScanTiming()
    {
        lock (_lock)
        {
            _scanPausedByGlobalPauseId = null;
            if (_scanState.IsActive)
            {
                _scanState.Reset();
            }
            else if (_scanStepInfo.HasValue)
            {
                _scanState.Start(_scanStepInfo.Value.Name, _scanStepInfo.Value.Description);
            }
            else
            {
                return;
            }
        }
        StartTimer();
        OnChanged?.Invoke();
    }
}
