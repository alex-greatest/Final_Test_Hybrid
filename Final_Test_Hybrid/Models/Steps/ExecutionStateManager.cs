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
    public ExecutionState State { get; private set; } = ExecutionState.Idle;
    public StepError? CurrentError { get; private set; }
    public bool HasErrors { get; private set; }

    public bool CanProcessSignals => State == ExecutionState.PausedOnError;
    public bool IsActive => State is ExecutionState.Running or ExecutionState.Processing;

    public event Action<ExecutionState>? OnStateChanged;

    public void TransitionTo(ExecutionState newState, StepError? error = null)
    {
        State = newState;
        CurrentError = error;
        OnStateChanged?.Invoke(newState);
    }

    public void SetHasErrors(bool value)
    {
        if (HasErrors == value)
        {
            return;
        }
        HasErrors = value;
        OnStateChanged?.Invoke(State);
    }
}
