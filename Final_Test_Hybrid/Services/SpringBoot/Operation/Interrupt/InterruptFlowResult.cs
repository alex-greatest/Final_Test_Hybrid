namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

/// <summary>
/// Результат выполнения flow ввода причины прерывания.
/// </summary>
public record InterruptFlowResult(bool IsSuccess, bool IsCancelled, string AdminUsername)
{
    public static InterruptFlowResult Success(string admin) => new(true, false, admin);
    public static InterruptFlowResult Cancelled() => new(false, true, string.Empty);
}
