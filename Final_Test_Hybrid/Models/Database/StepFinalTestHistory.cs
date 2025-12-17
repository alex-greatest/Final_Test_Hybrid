using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

public class StepFinalTestHistory
{
    public long Id { get; init; }
    [Required]
    public long StepFinalTestId { get; set; }
    [Required]
    [StringLength(500)]
    public string Name { get; set; } = string.Empty;
    [Required]
    public bool IsActive { get; set; }
}
