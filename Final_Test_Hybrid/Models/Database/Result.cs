using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

/// <summary>
/// Результат измерения параметра при тестировании.
/// </summary>
public class Result
{
    public long Id { get; init; }

    public string? Min { get; set; }

    [Required]
    public string Value { get; set; } = string.Empty;

    public string? Max { get; set; }

    public int? Status { get; set; }

    [Required]
    public long OperationId { get; set; }
    public Operation? Operation { get; set; }

    [Required]
    public long ResultSettingHistoryId { get; set; }
    public ResultSettingHistory? ResultSettingHistory { get; set; }
}
