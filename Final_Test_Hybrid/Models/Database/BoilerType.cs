using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

public class BoilerType
{
    public long Id { get; set; }
    [Required]
    [StringLength(10, MinimumLength = 10)]
    public string Article { get; set; } = string.Empty;
    [Required]
    public string Name { get; set; } = string.Empty;
    [ConcurrencyCheck]
    public int Version { get; set; }
}
