using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

public class Boiler
{
    public long Id { get; init; }
    [Required]
    [StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;
    [Required]
    public long BoilerTypeCycleId { get; set; }
    public BoilerTypeCycle? BoilerTypeCycle { get; set; }
    [Required]
    public DateTime DateCreate { get; set; }
    public DateTime? DateUpdate { get; set; }
    [Required]
    public OperationResultStatus Status { get; set; }
    [Required]
    [StringLength(255)]
    public string Operator { get; set; } = string.Empty;
}
