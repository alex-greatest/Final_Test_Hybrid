using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.CH;

/// <summary>
/// Тестовый шаг замера температуры холодной воды в контуре отопления.
/// </summary>
public class GetChwFlowNtcColdStep(
    DualLogger<GetChwFlowNtcColdStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions, IProvideLimits
{
    private const string BlockPath = "DB_VI.CH.Get_CHW_Flow_NTC_Cold";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"CH\".\"Get_CHW_Flow_NTC_Cold\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"CH\".\"Get_CHW_Flow_NTC_Cold\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"CH\".\"Get_CHW_Flow_NTC_Cold\".\"Error\"";
    private const string FlwTempColdTag = "ns=3;s=\"DB_Parameter\".\"CH\".\"Flw_Temp_Cold\"";

    public string Id => "ch-get-chw-flow-ntc-cold";
    public string Name => "CH/Get_CHW_Flow_NTC_Cold";
    public string Description => "Контур Отопление. Замер температуры холодной воды";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, FlwTempColdTag];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        return "<= 3.000";
    }

    /// <summary>
    /// Выполняет шаг замера температуры холодной воды.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск замера температуры холодной воды");

        testResultsService.Remove("CH_Flw_Temp_Cold");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции замера температуры.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<MeasureResult>()
                .WaitForTrue(EndTag, () => MeasureResult.Success, "End")
                .WaitForTrue(ErrorTag, () => MeasureResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            MeasureResult.Success => await HandleCompletionAsync(context, isSuccess: true, ct),
            MeasureResult.Error => await HandleCompletionAsync(context, isSuccess: false, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает завершение операции: чтение Flw_Temp_Cold и сохранение результата.
    /// </summary>
    private async Task<TestStepResult> HandleCompletionAsync(TestStepContext context, bool isSuccess, CancellationToken ct)
    {
        var (_, flwTempCold, error) = await context.OpcUa.ReadAsync<float>(FlwTempColdTag, ct);
        if (error != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Flw_Temp_Cold: {error}");
        }

        var status = isSuccess ? 1 : 2;

        testResultsService.Add(
            parameterName: "CH_Flw_Temp_Cold",
            value: $"{flwTempCold:F3}",
            min: "",
            max: "",
            status: status,
            isRanged: false,
            unit: "",
            test: Name);

        logger.LogInformation("Температура холодной воды: {FlwTempCold:F3}, статус: {Status}",
            flwTempCold, status == 1 ? "OK" : "NOK");

        var msg = $"Температура: {flwTempCold:F3}";

        if (isSuccess)
        {
            logger.LogInformation("Замер температуры холодной воды завершен успешно");

            var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
            if (resetResult.Error != null)
            {
                return TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}");
            }

            return TestStepResult.Pass(msg);
        }

        return TestStepResult.Fail(msg);
    }

    private enum MeasureResult { Success, Error }
}
