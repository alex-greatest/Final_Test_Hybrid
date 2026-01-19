namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

public partial class StepTimingService
{
    public void StartScanTiming(string name, string description)
    {
        lock (_lock)
        {
            _scanState.Start(name, description);
        }
        StartTimer();
        OnChanged?.Invoke();
    }

    public void StopScanTiming()
    {
        lock (_lock)
        {
            if (!_scanState.IsRunning)
            {
                return;
            }
            _scanState.Stop();
        }
        UpdateTimerState();
        OnChanged?.Invoke();
    }

    public void ResetScanTiming()
    {
        lock (_lock)
        {
            if (!_scanState.IsActive)
            {
                return;
            }
            _scanState.Reset();
        }
        StartTimer();
        OnChanged?.Invoke();
    }
}
