namespace Final_Test_Hybrid.Models.Errors;

public record ErrorHistoryItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string TestName { get; init; } = string.Empty;
}
