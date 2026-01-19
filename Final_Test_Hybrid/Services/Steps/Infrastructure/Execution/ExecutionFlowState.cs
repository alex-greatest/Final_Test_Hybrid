namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public enum ExecutionStopReason
{
    None,
    Operator,
    AutoModeDisabled,
    PlcForceStop,
    PlcSoftReset,
    PlcHardReset
}

public sealed class ExecutionFlowState
{
    private readonly Lock _lock = new();
    private ExecutionStopReason _stopReason = ExecutionStopReason.None;
    private bool _stopAsFailure;

    public ExecutionStopReason StopReason
    {
        get { lock (_lock) return _stopReason; }
    }

    public bool StopAsFailure
    {
        get { lock (_lock) return _stopAsFailure; }
    }

    public bool IsStopRequested
    {
        get { lock (_lock) return _stopReason != ExecutionStopReason.None; }
    }

    public (ExecutionStopReason Reason, bool StopAsFailure) GetSnapshot()
    {
        lock (_lock)
        {
            return (_stopReason, _stopAsFailure);
        }
    }
    public event Action? OnChanged;

    public void RequestStop(ExecutionStopReason reason, bool stopAsFailure)
    {
        lock (_lock)
        {
            if (_stopReason == ExecutionStopReason.None)
            {
                _stopReason = reason;
            }
            _stopAsFailure |= stopAsFailure;
        }
        OnChanged?.Invoke();
    }

    public void ClearStop()
    {
        lock (_lock)
        {
            if (_stopReason == ExecutionStopReason.None && !_stopAsFailure)
            {
                return;
            }
            _stopReason = ExecutionStopReason.None;
            _stopAsFailure = false;
        }
        OnChanged?.Invoke();
    }
}
