using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

/// <summary>
/// Ошибка, зафиксированная при тестировании.
/// </summary>
public class Error
{
    public long Id { get; init; }

    [Required]
    public long ErrorSettingsHistoryId { get; set; }
    public ErrorSettingsHistory? ErrorSettingsHistory { get; set; }

    [Required]
    public long OperationId { get; set; }
    public Operation? Operation { get; set; }
}
