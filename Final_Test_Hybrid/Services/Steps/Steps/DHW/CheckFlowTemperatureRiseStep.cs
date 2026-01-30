using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.DHW;

/// <summary>
/// Тестовый шаг проверки роста температуры контура ГВС.
/// </summary>
public class CheckFlowTemperatureRiseStep(
    DualLogger<CheckFlowTemperatureRiseStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.DHW.Check_Flow_Temperature_Rise";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Check_Flow_Temperature_Rise\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Check_Flow_Temperature_Rise\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Check_Flow_Temperature_Rise\".\"Error\"";

    private const string FlwTempHotTag = "ns=3;s=\"DB_Parameter\".\"DHW\".\"Flw_Temp_Hot\"";
    private const string FlowNtcTempRiseTag = "ns=3;s=\"DB_Parameter\".\"DHW\".\"Flow_NTC_Temp_Rise\"";

    private const string FlwTempRiseMinRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"FlwTempRise\".\"Min\"";
    private const string FlwTempRiseMaxRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"FlwTempRise\".\"Max\"";

    public string Id => "dhw-check-flow-temperature-rise";
    public string Name => "DHW/Check_Flow_Temperature_Rise";
    public string Description => "Контур ГВС. Проверка роста температуры";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, FlwTempHotTag, FlowNtcTempRiseTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [FlwTempRiseMinRecipe, FlwTempRiseMaxRecipe];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    /// <param name="context">Контекст с индексом колонки и провайдером рецептов.</param>
    /// <returns>Строка с пределами или null, если пределы недоступны.</returns>
    public string? GetLimits(LimitsContext context)
    {
        var min = context.RecipeProvider.GetValue<float>(FlwTempRiseMinRecipe);
        var max = context.RecipeProvider.GetValue<float>(FlwTempRiseMaxRecipe);
        return (min, max) switch
        {
            (not null, not null) => $"[{min:F1} .. {max:F1}]",
            _ => null
        };
    }

    /// <summary>
    /// Выполняет шаг проверки роста температуры контура ГВС.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск проверки роста температуры контура ГВС");
        testResultsService.Remove("DHW_Flw_Temp_Hot");
        testResultsService.Remove("DHW_Flow_NTC_Temp_Rise");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции проверки роста температуры.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<TempRiseResult>()
                .WaitForTrue(EndTag, () => TempRiseResult.Success, "End")
                .WaitForTrue(ErrorTag, () => TempRiseResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            TempRiseResult.Success => await HandleCompletionAsync(context, isSuccess: true, ct),
            TempRiseResult.Error => await HandleCompletionAsync(context, isSuccess: false, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает завершение: чтение параметров и сохранение результатов.
    /// </summary>
    private async Task<TestStepResult> HandleCompletionAsync(TestStepContext context, bool isSuccess, CancellationToken ct)
    {
        var (_, flwTempHot, error1) = await context.OpcUa.ReadAsync<float>(FlwTempHotTag, ct);
        if (error1 != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Flw_Temp_Hot: {error1}");
        }

        var (_, flowNtcTempRise, error2) = await context.OpcUa.ReadAsync<float>(FlowNtcTempRiseTag, ct);
        if (error2 != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Flow_NTC_Temp_Rise: {error2}");
        }

        var minValue = context.RecipeProvider.GetValue<float>(FlwTempRiseMinRecipe);
        var maxValue = context.RecipeProvider.GetValue<float>(FlwTempRiseMaxRecipe);
        if (minValue == null || maxValue == null)
        {
            return TestStepResult.Fail("Рецепты FlwTempRise.Min/Max не загружены");
        }

        var min = minValue.Value;
        var max = maxValue.Value;
        var status = isSuccess ? 1 : 2;

        testResultsService.Add(
            parameterName: "DHW_Flw_Temp_Hot",
            value: $"{flwTempHot:F3}",
            min: "",
            max: "",
            status: status,
            isRanged: false,
            unit: "");

        testResultsService.Add(
            parameterName: "DHW_Flow_NTC_Temp_Rise",
            value: $"{flowNtcTempRise:F3}",
            min: $"{min:F3}",
            max: $"{max:F3}",
            status: status,
            isRanged: true,
            unit: "");

        var msg = $"Температура: Разница: {flwTempHot:F3} {flowNtcTempRise:F3}";

        logger.LogInformation(
            "Flw_Temp_Hot: {FlwTempHot:F3}, Flow_NTC_Temp_Rise: {FlowNtcTempRise:F3}, пределы: [{Min:F3} .. {Max:F3}], статус: {Status}",
            flwTempHot, flowNtcTempRise, min, max, status == 1 ? "OK" : "NOK");

        if (isSuccess)
        {
            // Сброс Start только при успехе (End от PLC)
            // При ошибке координатор сбросит через ResetBlockStartAsync
            var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
            if (resetResult.Error != null)
            {
                return TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}");
            }

            logger.LogInformation("Проверка роста температуры ГВС завершена успешно");
            return TestStepResult.Pass(msg);
        }

        // Ошибки автоматически активируются через PlcErrorMonitorService
        // когда PLC поднимает Al_* сигналы - не нужно передавать errors
        return TestStepResult.Fail(msg);
    }

    private enum TempRiseResult { Success, Error }
}
