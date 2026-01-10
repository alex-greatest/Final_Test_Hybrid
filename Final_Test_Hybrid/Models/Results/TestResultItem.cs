namespace Final_Test_Hybrid.Models.Results;

public record TestResultItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime Time { get; init; }
    public string ParameterName { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Tolerances { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
}
