namespace Final_Test_Hybrid.Models.Steps;

public record InterruptBehavior(
    string Message,
    InterruptAction Action,
    TimeSpan? Delay = null,
    TimeSpan? WaitForRecovery = null
);

public enum InterruptAction
{
    PauseAndWait,
    ResetAfterDelay,
    ResetImmediately
}
