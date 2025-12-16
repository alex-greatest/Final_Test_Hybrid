using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

public class ResultSettings
{
    public long Id { get; init; }
    [Required]
    [StringLength(100)]
    public string ParameterName { get; set; } = string.Empty;
    [Required]
    [StringLength(100)]
    public string AddressValue { get; set; } = string.Empty;
    [StringLength(100)]
    public string? AddressMin { get; set; }
    [StringLength(100)]
    public string? AddressMax { get; set; }
    [StringLength(100)]
    public string? AddressStatus { get; set; }
    [Required]
    public PlcType PlcType { get; set; }
    [StringLength(30)]
    public string? Nominal { get; set; }
    [StringLength(20)]
    public string? Unit { get; set; }
    [StringLength(500)]
    public string? Description { get; set; }
    [Required]
    public AuditType AuditType { get; set; }
}
