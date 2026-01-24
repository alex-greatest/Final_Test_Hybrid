namespace Final_Test_Hybrid.Models.Archive;

/// <summary>
/// DTO для отображения ошибки в архиве.
/// </summary>
public record ArchiveErrorItem
{
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
