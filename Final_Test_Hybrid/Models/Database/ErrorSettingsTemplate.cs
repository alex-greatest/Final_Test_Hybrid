using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

public class ErrorSettingsTemplate
{
    public long Id { get; init; }
    public long? StepId { get; set; }
    public StepFinalTest? Step { get; set; }
    [Required]
    [StringLength(500)]
    public string AddressError { get; set; } = string.Empty;
    public string? Description { get; set; }
}
