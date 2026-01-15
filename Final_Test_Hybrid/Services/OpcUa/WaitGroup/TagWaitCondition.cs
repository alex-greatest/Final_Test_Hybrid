namespace Final_Test_Hybrid.Services.OpcUa.WaitGroup;

public record TagWaitCondition
{
    public required string NodeId { get; init; }
    public IReadOnlyList<string>? AdditionalNodeIds { get; init; }
    public required Func<object?, bool> Condition { get; init; }
    public string? Name { get; init; }
}
