namespace Final_Test_Hybrid.Models.Archive;

/// <summary>
/// DTO для отображения времени шага в архиве.
/// </summary>
public record ArchiveStepTimeItem
{
    public string StepName { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
}
