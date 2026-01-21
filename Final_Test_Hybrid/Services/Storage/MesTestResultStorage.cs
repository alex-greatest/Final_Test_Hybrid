using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Finish;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

namespace Final_Test_Hybrid.Services.Storage;

/// <summary>
/// Реализация ITestResultStorage для отправки результатов теста в MES.
/// Все данные берутся из runtime-сервисов, без обращения к БД.
/// </summary>
public class MesTestResultStorage(
    BoilerState boilerState,
    OperatorState operatorState,
    AppSettingsService appSettings,
    ITestResultsService testResultsService,
    IErrorService errorService,
    IStepTimingService stepTimingService,
    OperationFinishService finishService,
    DualLogger<MesTestResultStorage> logger) : ITestResultStorage
{
    /// <summary>
    /// Отправляет результаты теста в MES.
    /// </summary>
    /// <param name="testResult">Результат теста (1 = Ok, 2 = Nok).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат сохранения.</returns>
    public async Task<SaveResult> SaveAsync(int testResult, CancellationToken ct)
    {
        if (testResult is not (1 or 2))
        {
            logger.LogError("Некорректное значение testResult={TestResult}, ожидается 1 (Ok) или 2 (Nok)", testResult);
            return SaveResult.Fail($"Некорректный результат теста: {testResult}");
        }

        var serialNumber = boilerState.SerialNumber;
        if (string.IsNullOrEmpty(serialNumber))
        {
            logger.LogWarning("SerialNumber не найден в BoilerState");
            return SaveResult.Fail("Серийный номер котла не найден");
        }

        var operatorName = operatorState.Username;
        if (string.IsNullOrEmpty(operatorName))
        {
            logger.LogWarning("Оператор не авторизован");
            return SaveResult.Fail("Оператор не авторизован");
        }

        logger.LogInformation(
            "Подготовка результатов теста для MES: SerialNumber={SerialNumber}, Result={TestResult}",
            serialNumber,
            testResult);

        var request = BuildRequest(serialNumber, operatorName, testResult);
        return await finishService.FinishOperationAsync(request, ct);
    }

    private FinalTestResultsRequest BuildRequest(string serialNumber, string operatorName, int testResult)
    {
        var results = testResultsService.GetResults();
        var history = errorService.GetHistory();
        var timings = stepTimingService.GetAll();

        var items = results
            .Where(r => !r.IsRanged)
            .Select(r => new FinalTestResultItem
            {
                Name = r.ParameterName,
                Value = r.Value,
                Status = r.Status.ToString(),
                Test = string.Empty,
                ValueType = "real"
            })
            .ToList();

        var itemsLimited = results
            .Where(r => r.IsRanged)
            .Select(r => new FinalTestResultItemLimited
            {
                Name = r.ParameterName,
                Value = r.Value,
                Min = r.Min,
                Max = r.Max,
                Status = r.Status.ToString(),
                Test = string.Empty,
                ValueType = "real"
            })
            .ToList();

        var times = timings
            .Select(t => new FinalTestResultTime
            {
                Test = t.Name,
                Time = t.Duration
            })
            .ToList();

        var errors = history
            .Select(e => e.Code)
            .Distinct()
            .ToList();

        logger.LogDebug(
            "Собрано результатов: Items={ItemsCount}, ItemsLimited={ItemsLimitedCount}, Times={TimesCount}, Errors={ErrorsCount}",
            items.Count,
            itemsLimited.Count,
            times.Count,
            errors.Count);

        return new FinalTestResultsRequest
        {
            SerialNumber = serialNumber,
            StationName = appSettings.NameStation,
            Operator = operatorName,
            Items = items,
            ItemsLimited = itemsLimited,
            Time = times,
            Errors = errors,
            Result = testResult
        };
    }
}
