using System.ComponentModel.DataAnnotations;

namespace Final_Test_Hybrid.Models.Database;

/// <summary>
/// Глобальный счётчик успешных результатов тестирования.
/// </summary>
public class SuccessCount
{
    public long Id { get; init; }

    [Required]
    public long Count { get; set; }
}
