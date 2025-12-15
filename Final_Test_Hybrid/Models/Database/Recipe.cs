using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

public class Recipe
{
    public long Id { get; init; }
    public long BoilerTypeId { get; set; }
    [Required]
    public PlcType PlcType { get; set; }
    public bool IsPlc { get; set; }
    [Required]
    [StringLength(100)]
    public string Address { get; set; } = string.Empty;
    [Required]
    [StringLength(100)]
    public string TagName { get; set; } = string.Empty;
    [Required]
    [StringLength(255)]
    public string Value { get; set; } = string.Empty;
    [StringLength(500)]
    public string? Description { get; set; }
    [StringLength(20)]
    public string? Unit { get; set; }
    public BoilerType? BoilerType { get; set; }
}
