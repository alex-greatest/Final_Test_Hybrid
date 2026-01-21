using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

/// <summary>
/// Время выполнения шага тестирования.
/// </summary>
public class StepTime
{
    public long Id { get; init; }

    [Required]
    public long StepFinalTestHistoryId { get; set; }
    public StepFinalTestHistory? StepFinalTestHistory { get; set; }

    [Required]
    public long OperationId { get; set; }
    public Operation? Operation { get; set; }

    [Required]
    public string Duration { get; set; } = string.Empty;
}
