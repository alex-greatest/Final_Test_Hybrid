namespace Final_Test_Hybrid.Models.Plc;

public record SubscriptionResult(
    List<string> SuccessfulNodes,
    List<SubscriptionError> FailedNodes
)
{
    public static SubscriptionResult Empty { get; } = new([], []);
}

public record SubscriptionError(string NodeId, string ErrorMessage);
