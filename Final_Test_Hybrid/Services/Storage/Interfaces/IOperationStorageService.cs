using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Database.Config;

namespace Final_Test_Hybrid.Services.Storage.Interfaces;

/// <summary>
/// Сервис для обновления Operation в базе данных.
/// </summary>
public interface IOperationStorageService
{
    /// <summary>
    /// Находит и обновляет Operation для текущего теста.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="serialNumber">Серийный номер котла.</param>
    /// <param name="testResult">Результат теста (1 = Ok, 2 = Nok).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Найденная Operation или null, если не найдена.</returns>
    Task<Operation?> UpdateOperationAsync(
        AppDbContext context,
        string serialNumber,
        int testResult,
        CancellationToken ct);
}
