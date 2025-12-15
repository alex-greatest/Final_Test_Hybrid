using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

public class BoilerType
{
    public long Id { get; init; }
    [Required]
    [StringLength(10, MinimumLength = 10)]
    public string Article { get; init; } = string.Empty;
    [Required]
    [StringLength(100)]
    public string Type { get; init; } = string.Empty;
    [ConcurrencyCheck]
    public int Version { get; set; }
}
