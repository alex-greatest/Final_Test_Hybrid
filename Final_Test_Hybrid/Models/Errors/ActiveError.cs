namespace Final_Test_Hybrid.Models.Errors;

public record ActiveError
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime Time { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string TestName { get; init; } = string.Empty;
}
