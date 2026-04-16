namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

/// <summary>
/// Результат выполнения flow ввода причины прерывания.
/// </summary>
public enum InterruptFlowOutcome
{
    Saved,
    Cancelled,
    RepeatBypass
}

public record InterruptFlowResult(InterruptFlowOutcome Outcome, string AdminUsername)
{
    public bool IsSuccess => Outcome == InterruptFlowOutcome.Saved;
    public bool IsCancelled => Outcome == InterruptFlowOutcome.Cancelled;
    public bool IsRepeatBypass => Outcome == InterruptFlowOutcome.RepeatBypass;

    public static InterruptFlowResult Success(string admin) => new(InterruptFlowOutcome.Saved, admin);
    public static InterruptFlowResult Cancelled() => new(InterruptFlowOutcome.Cancelled, string.Empty);
    public static InterruptFlowResult RepeatBypass() => new(InterruptFlowOutcome.RepeatBypass, string.Empty);
}

public sealed class InterruptReasonDialogResult
{
    public SaveResult? SaveResult { get; init; }
    public bool IsRepeatBypass { get; init; }

    public static InterruptReasonDialogResult Saved(SaveResult result) => new() { SaveResult = result };
    public static InterruptReasonDialogResult RepeatBypass() => new() { IsRepeatBypass = true };
}
