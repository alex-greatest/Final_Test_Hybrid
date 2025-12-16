using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

public class BoilerTypeCycle
{
    public long Id { get; set; }
    [Required]
    public long BoilerTypeId { get; set; }
    [Required]
    [StringLength(100)]
    public string Type { get; set; } = string.Empty;
    [Required]
    public bool IsActive { get; set; }
    [Required]
    [StringLength(10)]
    public string Article { get; set; } = string.Empty;
}
