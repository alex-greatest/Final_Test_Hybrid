namespace Final_Test_Hybrid.Models.Archive;

/// <summary>
/// DTO для отображения ошибки в архиве.
/// </summary>
public record ArchiveErrorItem
{
    /// <summary>
    /// Уникальный идентификатор записи ошибки.
    /// </summary>
    // ReSharper disable once UnusedAutoPropertyAccessor
    public long Id { get; init; }

    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
