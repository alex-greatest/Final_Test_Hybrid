namespace Final_Test_Hybrid.Models.Archive;

/// <summary>
/// DTO для отображения времени шага в архиве.
/// </summary>
public record ArchiveStepTimeItem
{
    /// <summary>
    /// Уникальный идентификатор записи времени шага.
    /// </summary>
    public long Id { get; init; }

    public string StepName { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
}
