using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Results;
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
    private const string TestMissingOrEmptyLogTemplate =
        "TestMissingOrEmpty: OperationId={OperationId}, ParameterName={ParameterName}, Test={Test}";

    private const string StepHistoryNotFoundLogTemplate =
        "StepHistoryNotFound: OperationId={OperationId}, ParameterName={ParameterName}, Test={Test}";

    private const string DuplicateStepHistoryNameResolvedLogTemplate =
        "DuplicateStepHistoryNameResolved: OperationId={OperationId}, Name={Name}, CandidateIds={CandidateIds}, SelectedId={SelectedId}";

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
        var stepHistoryMap = await LoadActiveStepHistoryMapAsync(context, operation.Id, testResults, ct);
        var results = CreateResultEntities(testResults, operation, settingsDict, stepHistoryMap);

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
    /// Загружает активные шаги по именам из testResults и строит карту для точного сопоставления.
    /// </summary>
    private async Task<Dictionary<string, StepFinalTestHistory>> LoadActiveStepHistoryMapAsync(
        AppDbContext context,
        long operationId,
        IReadOnlyList<TestResultItem> testResults,
        CancellationToken ct)
    {
        var requestedNames = testResults
            .Select(item => item.Test)
            .Where(static testName => !string.IsNullOrEmpty(testName))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (requestedNames.Count == 0)
        {
            return new Dictionary<string, StepFinalTestHistory>(StringComparer.Ordinal);
        }

        var histories = await context.StepFinalTestHistories
            .Where(history => history.IsActive && requestedNames.Contains(history.Name))
            .ToListAsync(ct);

        return BuildStepHistoryMap(operationId, histories);
    }

    /// <summary>
    /// Строит карту Name -> StepFinalTestHistory c детерминированным разрешением дублей по минимальному Id.
    /// </summary>
    private Dictionary<string, StepFinalTestHistory> BuildStepHistoryMap(
        long operationId,
        IReadOnlyList<StepFinalTestHistory> histories)
    {
        var map = new Dictionary<string, StepFinalTestHistory>(StringComparer.Ordinal);

        foreach (var groupedHistories in histories.GroupBy(history => history.Name, StringComparer.Ordinal))
        {
            var orderedHistories = groupedHistories.OrderBy(history => history.Id).ToList();
            if (orderedHistories.Count > 1)
            {
                logger.LogWarning(
                    DuplicateStepHistoryNameResolvedLogTemplate,
                    operationId,
                    groupedHistories.Key,
                    string.Join(", ", orderedHistories.Select(history => history.Id)),
                    orderedHistories[0].Id);
            }

            map[groupedHistories.Key] = orderedHistories[0];
        }

        return map;
    }

    /// <summary>
    /// Пытается разрешить StepFinalTestHistoryId для результата; при ошибке пишет warning и возвращает false.
    /// </summary>
    private bool TryResolveStepHistoryId(
        long operationId,
        TestResultItem item,
        IReadOnlyDictionary<string, StepFinalTestHistory> stepHistoryMap,
        out long stepHistoryId)
    {
        stepHistoryId = default;

        if (string.IsNullOrEmpty(item.Test))
        {
            logger.LogWarning(
                TestMissingOrEmptyLogTemplate,
                operationId,
                item.ParameterName,
                item.Test);
            return false;
        }

        if (stepHistoryMap.TryGetValue(item.Test, out var stepHistory))
        {
            stepHistoryId = stepHistory.Id;
            return true;
        }

        logger.LogWarning(
            StepHistoryNotFoundLogTemplate,
            operationId,
            item.ParameterName,
            item.Test);
        return false;
    }

    /// <summary>
    /// Создает Result entities из результатов теста.
    /// </summary>
    private List<Result> CreateResultEntities(
        IReadOnlyList<TestResultItem> testResults,
        Operation operation,
        IReadOnlyDictionary<string, ResultSettingHistory> settingsDict,
        IReadOnlyDictionary<string, StepFinalTestHistory> stepHistoryMap)
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

            if (!TryResolveStepHistoryId(operation.Id, item, stepHistoryMap, out var stepHistoryId))
            {
                continue;
            }

            results.Add(new Result
            {
                OperationId = operation.Id,
                ResultSettingHistoryId = settingHistory.Id,
                StepFinalTestHistoryId = stepHistoryId,
                Value = item.Value,
                Min = string.IsNullOrEmpty(item.Min) ? null : item.Min,
                Max = string.IsNullOrEmpty(item.Max) ? null : item.Max,
                Status = item.Status
            });
        }

        return results;
    }
}
