namespace Final_Test_Hybrid.Models.Steps;

public enum ExecutionState
{
    Idle,
    Processing,
    Running,
    PausedOnError,
    Completed,
    Failed
}

public class ExecutionStateManager
{
    private readonly Queue<StepError> _errorQueue = new();
    private readonly Lock _queueLock = new();
    public bool HasPendingErrors => ErrorCount > 0;
    public bool CanProcessSignals => State == ExecutionState.PausedOnError;
    public bool IsActive => State is ExecutionState.Running or ExecutionState.Processing;
    public event Action<ExecutionState>? OnStateChanged;

    public ExecutionState State { get; private set; } = ExecutionState.Idle;

    public StepError? CurrentError
    {
        get
        {
            lock (_queueLock)
            {
                return _errorQueue.TryPeek(out var e) ? e : null;
            }
        }
    }

    public int ErrorCount
    {
        get
        {
            lock (_queueLock)
            {
                return _errorQueue.Count;
            }
        }
    }
    
    public void TransitionTo(ExecutionState newState)
    {
        State = newState;
        OnStateChanged?.Invoke(newState);
    }

    public void EnqueueError(StepError error)
    {
        lock (_queueLock)
        {
            if (_errorQueue.All(e => e.ColumnIndex != error.ColumnIndex))
            {
                _errorQueue.Enqueue(error);
            }
        }
        OnStateChanged?.Invoke(State);
    }

    public StepError? DequeueError()
    {
        StepError? result;
        lock (_queueLock)
        {
            result = _errorQueue.TryDequeue(out var e) ? e : null;
        }
        OnStateChanged?.Invoke(State);
        return result;
    }

    public void ClearErrors()
    {
        lock (_queueLock)
        {
            _errorQueue.Clear();
        }
        OnStateChanged?.Invoke(State);
    }
}
