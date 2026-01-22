using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Final_Test_Hybrid.Services.Storage.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Services.Storage;

/// <summary>
/// Сервис для обновления Operation в базе данных.
/// </summary>
public class OperationStorageService(DualLogger<OperationStorageService> logger) : IOperationStorageService
{
    /// <summary>
    /// Находит и обновляет Operation для текущего теста.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="serialNumber">Серийный номер котла.</param>
    /// <param name="testResult">Результат теста (1 = Ok, 2 = Nok).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Найденная Operation или null, если не найдена.</returns>
    public async Task<Operation?> UpdateOperationAsync(
        AppDbContext context,
        string serialNumber,
        int testResult,
        CancellationToken ct)
    {
        var boiler = await context.Boilers
            .FirstOrDefaultAsync(b => b.SerialNumber == serialNumber, ct);

        if (boiler == null)
        {
            logger.LogWarning("Котел с серийным номером {SerialNumber} не найден", serialNumber);
            return null;
        }

        var operation = await context.Operations
            .FirstOrDefaultAsync(o => o.BoilerId == boiler.Id && o.Status == OperationResultStatus.InWork, ct);

        if (operation == null)
        {
            logger.LogWarning("Operation со статусом InWork не найдена для котла {SerialNumber}", serialNumber);
            return null;
        }

        operation.Status = testResult == 1 ? OperationResultStatus.Ok : OperationResultStatus.Nok;
        operation.DateEnd = DateTime.UtcNow;

        logger.LogInformation(
            "Operation {OperationId} обновлена: Status={Status}, DateEnd={DateEnd}",
            operation.Id,
            operation.Status,
            operation.DateEnd);

        return operation;
    }
}
