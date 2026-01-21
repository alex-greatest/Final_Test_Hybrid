using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Storage.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Services.Storage;

/// <summary>
/// Сервис для сохранения Error (ошибок тестирования) в базу данных.
/// </summary>
public class ErrorStorageService(
    IErrorService errorService,
    DualLogger<ErrorStorageService> logger) : IErrorStorageService
{
    /// <summary>
    /// Создает список Error для batch insert.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="operation">Operation, к которой привязываются ошибки.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Список Error для добавления в БД.</returns>
    public async Task<List<Error>> CreateErrorsAsync(
        AppDbContext context,
        Operation operation,
        CancellationToken ct)
    {
        var errorHistory = errorService.GetHistory();
        if (errorHistory.Count == 0)
        {
            logger.LogInformation("Нет ошибок для сохранения");
            return [];
        }

        var settingsDict = await LoadSettingsAsync(context, ct);
        var errors = CreateErrorEntities(errorHistory, operation, settingsDict);

        logger.LogInformation(
            "Подготовлено {Count} ошибок для сохранения",
            errors.Count);

        return errors;
    }

    /// <summary>
    /// Загружает все активные настройки ошибок в словарь.
    /// </summary>
    private static async Task<Dictionary<string, ErrorSettingsHistory>> LoadSettingsAsync(
        AppDbContext context,
        CancellationToken ct)
    {
        var settings = await context.ErrorSettingsHistories
            .Where(h => h.IsActive)
            .ToListAsync(ct);

        return settings.ToDictionary(h => h.AddressError);
    }

    /// <summary>
    /// Создает Error entities из истории ошибок.
    /// </summary>
    private List<Error> CreateErrorEntities(
        IReadOnlyList<Models.Errors.ErrorHistoryItem> errorHistory,
        Operation operation,
        Dictionary<string, ErrorSettingsHistory> settingsDict)
    {
        var errors = new List<Error>(errorHistory.Count);

        foreach (var item in errorHistory)
        {
            if (!settingsDict.TryGetValue(item.Code, out var settingHistory))
            {
                logger.LogWarning(
                    "ErrorSettingsHistory не найдена для кода ошибки {ErrorCode}",
                    item.Code);
                continue;
            }

            errors.Add(new Error
            {
                OperationId = operation.Id,
                ErrorSettingsHistoryId = settingHistory.Id
            });
        }

        return errors;
    }
}
