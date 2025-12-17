using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

public class StepFinalTest
{
    public long Id { get; init; }
    [Required]
    [StringLength(500)]
    public string Name { get; set; } = string.Empty;
}
