using System.Diagnostics.CodeAnalysis;
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
/// Собирает runtime-снимок результатов теста для MES и interrupt-flow.
/// </summary>
public class FinalTestResultsSnapshotBuilder(
    BoilerState boilerState,
    OperatorState operatorState,
    AppSettingsService appSettings,
    ITestResultsService testResultsService,
    IErrorService errorService,
    IStepTimingService stepTimingService,
    DualLogger<FinalTestResultsSnapshotBuilder> logger)
{
    public bool TryBuild(
        int result,
        [NotNullWhen(true)] out FinalTestResultsRequest? request,
        out string errorMessage)
    {
        var serialNumber = boilerState.SerialNumber;
        if (string.IsNullOrEmpty(serialNumber))
        {
            logger.LogWarning("SerialNumber не найден в BoilerState");
            request = null;
            errorMessage = "Серийный номер котла не найден";
            return false;
        }

        var operatorName = operatorState.Username;
        if (string.IsNullOrEmpty(operatorName))
        {
            logger.LogWarning("Оператор не авторизован");
            request = null;
            errorMessage = "Оператор не авторизован";
            return false;
        }

        request = BuildRequest(serialNumber, operatorName, result);
        errorMessage = string.Empty;
        return true;
    }

    private FinalTestResultsRequest BuildRequest(string serialNumber, string operatorName, int result)
    {
        var runtimeResults = testResultsService.GetResults();
        var errorHistory = errorService.GetHistory();
        var timings = stepTimingService.GetAll();
        var items = CreateItems(runtimeResults);
        var itemsLimited = CreateItemsLimited(runtimeResults);
        var times = CreateTimes(timings);
        var errors = errorHistory
            .Select(item => item.Code)
            .ToList();

        logger.LogDebug(
            "Собран snapshot результатов: Items={ItemsCount}, ItemsLimited={ItemsLimitedCount}, Times={TimesCount}, ErrorHistoryRecords={ErrorsCount}, Result={Result}",
            items.Count,
            itemsLimited.Count,
            times.Count,
            errors.Count,
            result);

        return new FinalTestResultsRequest
        {
            SerialNumber = serialNumber,
            StationName = appSettings.NameStation,
            Operator = operatorName,
            Items = items,
            ItemsLimited = itemsLimited,
            Time = times,
            Errors = errors,
            Result = result
        };
    }

    private static List<FinalTestResultItem> CreateItems(IReadOnlyList<Models.Results.TestResultItem> runtimeResults)
    {
        return runtimeResults
            .Where(item => !item.IsRanged)
            .Select(item => new FinalTestResultItem
            {
                Name = item.ParameterName,
                Value = item.Value,
                Status = item.Status.ToString(),
                Test = item.Test,
                ValueType = ResolveValueType(item.ParameterName)
            })
            .ToList();
    }

    private static List<FinalTestResultItemLimited> CreateItemsLimited(IReadOnlyList<Models.Results.TestResultItem> runtimeResults)
    {
        return runtimeResults
            .Where(item => item.IsRanged)
            .Select(item => new FinalTestResultItemLimited
            {
                Name = item.ParameterName,
                Value = item.Value,
                Min = item.Min,
                Max = item.Max,
                Status = item.Status.ToString(),
                Test = item.Test,
                ValueType = ResolveValueType(item.ParameterName)
            })
            .ToList();
    }

    private static List<FinalTestResultTime> CreateTimes(IReadOnlyList<Models.Steps.StepTimingRecord> timings)
    {
        return timings
            .Select(item => new FinalTestResultTime
            {
                Test = item.Name,
                Time = item.Duration
            })
            .ToList();
    }

    private static string ResolveValueType(string parameterName)
    {
        return parameterName is "Timer_1" or "Timer_2" ? "string" : "real";
    }
}
