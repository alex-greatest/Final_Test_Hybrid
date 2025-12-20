using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

public class ErrorSettingsHistory
{
    public long Id { get; init; }
    public long? ErrorSettingsTemplateId { get; set; }
    public long? StepHistoryId { get; set; }
    public StepFinalTestHistory? StepHistory { get; set; }
    [Required]
    [StringLength(500)]
    public string AddressError { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required]
    public bool IsActive { get; set; }
}
