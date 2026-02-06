using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.CH;

/// <summary>
/// Тестовый шаг проверки расхода воды в контуре отопления.
/// </summary>
public class CheckWaterFlowStep(
    DualLogger<CheckWaterFlowStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.CH.Check_Water_Flow";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"CH\".\"Check_Water_Flow\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"CH\".\"Check_Water_Flow\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"CH\".\"Check_Water_Flow\".\"Error\"";
    private const string FlowRateTag = "ns=3;s=\"DB_Parameter\".\"CH\".\"Flow_Rate\"";
    private const string FlowRateMinRecipe = "ns=3;s=\"DB_Recipe\".\"CH\".\"Flow_Rate_Min\"";
    private const string FlowRateMaxRecipe = "ns=3;s=\"DB_Recipe\".\"CH\".\"Flow_Rate_Max\"";
    private const string ChPressMax = "ns=3;s=\"DB_Recipe\".\"CH\".\"Press\".\"Max\"";
    private const string ChPressMin = "ns=3;s=\"DB_Recipe\".\"CH\".\"Press\".\"Min\"";

    public string Id => "ch-check-water-flow";
    public string Name => "CH/Check_Water_Flow";
    public string Description => "Контур Отопления. Проверка расхода воды.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, FlowRateTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [FlowRateMinRecipe, FlowRateMaxRecipe, ChPressMax, ChPressMin];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    /// <param name="context">Контекст с индексом колонки и провайдером рецептов.</param>
    /// <returns>Строка с пределами или null, если пределы недоступны.</returns>
    public string? GetLimits(LimitsContext context)
    {
        var min = context.RecipeProvider.GetValue<float>(FlowRateMinRecipe);
        var max = context.RecipeProvider.GetValue<float>(FlowRateMaxRecipe);
        var minCh = context.RecipeProvider.GetValue<float>(FlowRateMinRecipe);
        var maxCh = context.RecipeProvider.GetValue<float>(FlowRateMaxRecipe);
        return min != null && max != null ? $"[{minCh} .. {maxCh}] [{min} .. {max}]" : null;
    }

    /// <summary>
    /// Выполняет шаг проверки расхода воды в контуре отопления.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск проверки расхода воды");
        testResultsService.Remove("CH_Flow_Rate");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции проверки расхода воды.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<FlowResult>()
                .WaitForTrue(EndTag, () => FlowResult.Success, "End")
                .WaitForTrue(ErrorTag, () => FlowResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            FlowResult.Success => await HandleCompletionAsync(context, isSuccess: true, ct),
            FlowResult.Error => await HandleCompletionAsync(context, isSuccess: false, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает завершение: чтение расхода воды, валидация и сохранение результата.
    /// </summary>
    private async Task<TestStepResult> HandleCompletionAsync(TestStepContext context, bool isSuccess, CancellationToken ct)
    {
        var (_, flowRate, error) = await context.OpcUa.ReadAsync<float>(FlowRateTag, ct);
        if (error != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Flow_Rate: {error}");
        }

        var minValue = context.RecipeProvider.GetValue<float>(FlowRateMinRecipe);
        var maxValue = context.RecipeProvider.GetValue<float>(FlowRateMaxRecipe);
        if (minValue == null || maxValue == null)
        {
            return TestStepResult.Fail("Рецепты Flow_Rate_Min/Max не загружены");
        }

        var min = minValue.Value;
        var max = maxValue.Value;
        var status = isSuccess ? 1 : 2;

        testResultsService.Add(
            parameterName: "CH_Flow_Rate",
            value: $"{flowRate:F3}",
            min: $"{min:F3}",
            max: $"{max:F3}",
            status: status,
            isRanged: true,
            unit: "");

        logger.LogInformation("Расход воды: {FlowRate:F3}, пределы: {Min:F3} - {Max:F3}, статус: {Status}",
            flowRate, min, max, status == 1 ? "OK" : "NOK");

        var msg = $"Расход воды: {flowRate:F3}";

        if (isSuccess)
        {
            logger.LogInformation("Проверка расхода воды завершена успешно");

            var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
            return resetResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}") : TestStepResult.Pass(msg);
        }

        return TestStepResult.Fail(msg);
    }

    private enum FlowResult { Success, Error }
}
