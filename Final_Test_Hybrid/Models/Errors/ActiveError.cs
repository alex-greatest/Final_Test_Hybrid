namespace Final_Test_Hybrid.Models.Errors;

public record ActiveError
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime Time { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ErrorSeverity Severity { get; init; }
    public ErrorSource Source { get; init; }
    public string? StepId { get; init; }
    public string? StepName { get; init; }
    public bool ActivatesResetButton { get; init; }
}
