using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Storage.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Services.Storage;

/// <summary>
/// Сервис для сохранения Result (результатов измерений) в базу данных.
/// </summary>
public class ResultStorageService(
    ITestResultsService testResultsService,
    DualLogger<ResultStorageService> logger) : IResultStorageService
{
    /// <summary>
    /// Создает список Result для batch insert.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="operation">Operation, к которой привязываются результаты.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Список Result для добавления в БД.</returns>
    public async Task<List<Result>> CreateResultsAsync(
        AppDbContext context,
        Operation operation,
        CancellationToken ct)
    {
        var testResults = testResultsService.GetResults();
        if (testResults.Count == 0)
        {
            logger.LogInformation("Нет результатов измерений для сохранения");
            return [];
        }

        var boilerTypeId = await GetBoilerTypeIdAsync(context, operation.BoilerId, ct);
        if (boilerTypeId == null)
        {
            logger.LogWarning(
                "Не удалось определить BoilerTypeId для Boiler {BoilerId}",
                operation.BoilerId);
            return [];
        }

        var settingsDict = await LoadSettingsAsync(context, boilerTypeId.Value, ct);
        var results = CreateResultEntities(testResults, operation, settingsDict);

        logger.LogInformation(
            "Подготовлено {Count} результатов измерений для сохранения",
            results.Count);

        return results;
    }

    /// <summary>
    /// Получает BoilerTypeId через цепочку Boiler → BoilerTypeCycle.
    /// </summary>
    private static async Task<long?> GetBoilerTypeIdAsync(
        AppDbContext context,
        long boilerId,
        CancellationToken ct)
    {
        return await context.Boilers
            .Where(b => b.Id == boilerId)
            .Select(b => (long?)b.BoilerTypeCycle!.BoilerTypeId)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Загружает активные настройки результатов для указанного типа котла в словарь.
    /// </summary>
    private static async Task<Dictionary<string, ResultSettingHistory>> LoadSettingsAsync(
        AppDbContext context,
        long boilerTypeId,
        CancellationToken ct)
    {
        var settings = await context.ResultSettingHistories
            .Where(h => h.IsActive && h.BoilerTypeId == boilerTypeId)
            .ToListAsync(ct);

        return settings.ToDictionary(h => h.AddressValue);
    }

    /// <summary>
    /// Создает Result entities из результатов теста.
    /// </summary>
    private List<Result> CreateResultEntities(
        IReadOnlyList<Models.Results.TestResultItem> testResults,
        Operation operation,
        Dictionary<string, ResultSettingHistory> settingsDict)
    {
        var results = new List<Result>(testResults.Count);

        foreach (var item in testResults)
        {
            if (!settingsDict.TryGetValue(item.ParameterName, out var settingHistory))
            {
                logger.LogWarning(
                    "ResultSettingHistory не найдена для параметра {ParameterName}",
                    item.ParameterName);
                continue;
            }

            results.Add(new Result
            {
                OperationId = operation.Id,
                ResultSettingHistoryId = settingHistory.Id,
                Value = item.Value,
                Min = string.IsNullOrEmpty(item.Min) ? null : item.Min,
                Max = string.IsNullOrEmpty(item.Max) ? null : item.Max,
                Status = item.Status
            });
        }

        return results;
    }
}
