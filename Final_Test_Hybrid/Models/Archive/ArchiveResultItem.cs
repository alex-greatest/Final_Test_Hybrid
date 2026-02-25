namespace Final_Test_Hybrid.Models.Archive;

/// <summary>
/// DTO для отображения результата измерения в архиве.
/// </summary>
public record ArchiveResultItem
{
    public string? TestName { get; init; }
    public string ParameterName { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string? Min { get; init; }
    public string? Max { get; init; }
    public int? Status { get; init; }
    public string? Unit { get; init; }
}
