using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

public class BoilerTypeCycle
{
    public long Id { get; set; }
    [Required]
    public long BoilerTypeId { get; set; }
    [Required]
    public string Type { get; set; } = string.Empty;
    [Required]
    public bool IsActive { get; set; }
    [Required]
    public string Article { get; set; } = string.Empty;
    [ConcurrencyCheck]
    public int Version { get; set; }
}
