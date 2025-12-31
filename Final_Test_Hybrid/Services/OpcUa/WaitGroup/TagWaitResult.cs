namespace Final_Test_Hybrid.Services.OpcUa.WaitGroup;

public record TagWaitResult
{
    public required int WinnerIndex { get; init; }
    public required string NodeId { get; init; }
    public required object? Value { get; init; }
    public string? Name { get; init; }
}

public record TagWaitResult<T>
{
    public required int WinnerIndex { get; init; }
    public required string NodeId { get; init; }
    public required object? RawValue { get; init; }
    public required T Result { get; init; }
    public string? Name { get; init; }
}
