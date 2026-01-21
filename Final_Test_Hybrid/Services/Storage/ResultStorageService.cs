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

        var settingsDict = await LoadSettingsAsync(context, ct);
        var results = CreateResultEntities(testResults, operation, settingsDict);

        logger.LogInformation(
            "Подготовлено {Count} результатов измерений для сохранения",
            results.Count);

        return results;
    }

    /// <summary>
    /// Загружает все активные настройки результатов в словарь.
    /// </summary>
    private static async Task<Dictionary<(string AddressValue, AuditType AuditType), ResultSettingHistory>> LoadSettingsAsync(
        AppDbContext context,
        CancellationToken ct)
    {
        var settings = await context.ResultSettingHistories
            .Where(h => h.IsActive)
            .ToListAsync(ct);

        return settings.ToDictionary(h => (h.AddressValue, h.AuditType));
    }

    /// <summary>
    /// Создает Result entities из результатов теста.
    /// </summary>
    private List<Result> CreateResultEntities(
        IReadOnlyList<Models.Results.TestResultItem> testResults,
        Operation operation,
        Dictionary<(string AddressValue, AuditType AuditType), ResultSettingHistory> settingsDict)
    {
        var results = new List<Result>(testResults.Count);

        foreach (var item in testResults)
        {
            var auditType = item.IsRanged ? AuditType.NumericWithRange : AuditType.SimpleStatus;
            var key = (item.ParameterName, auditType);

            if (!settingsDict.TryGetValue(key, out var settingHistory))
            {
                logger.LogWarning(
                    "ResultSettingHistory не найдена для параметра {ParameterName} с AuditType={AuditType}",
                    item.ParameterName,
                    auditType);
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
