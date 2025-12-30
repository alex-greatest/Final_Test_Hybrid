using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

public class Operation
{
    public long Id { get; init; }
    [Required]
    public DateTime DateStart { get; set; }
    public DateTime? DateEnd { get; set; }
    [Required]
    public long BoilerId { get; set; }
    public Boiler? Boiler { get; set; }
    [Required]
    public OperationResultStatus Status { get; set; }
    [Required]
    public int NumberShift { get; set; }
    public string? Comment { get; set; }
    [Required]
    public int Version { get; set; }
    [Required]
    [StringLength(255)]
    public string Operator { get; set; } = string.Empty;
}
