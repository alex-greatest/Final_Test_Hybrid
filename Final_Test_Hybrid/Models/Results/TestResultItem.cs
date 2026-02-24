namespace Final_Test_Hybrid.Models.Results;

public record TestResultItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime Time { get; init; }
    public string Test { get; init; } = string.Empty;
    public string ParameterName { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Min { get; init; } = string.Empty;
    public string Max { get; init; } = string.Empty;
    public int Status { get; init; }
    public bool IsRanged { get; init; }
    public string Unit { get; init; } = string.Empty;
}
