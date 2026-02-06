using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.DHW;

/// <summary>
/// Тестовый шаг для проверки расхода воды в контуре ГВС.
/// </summary>
public class CheckFlowRateStep(
    DualLogger<CheckFlowRateStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.DHW.Check_Flow_Rate";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Check_Flow_Rate\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Check_Flow_Rate\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Check_Flow_Rate\".\"Error\"";
    private const string FlowRateTag = "ns=3;s=\"DB_Parameter\".\"DHW\".\"Flow_Rate\"";
    private const string FlowMinRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"Flow\".\"Min\"";
    private const string FlowMaxRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"Flow\".\"Max\"";

    public string Id => "dhw-check-flow-rate";
    public string Name => "DHW/Check_Flow_Rate";
    public string Description => "Контур ГВС. Проверка расхода воды.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, FlowRateTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [FlowMinRecipe, FlowMaxRecipe];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        var min = context.RecipeProvider.GetValue<float>(FlowMinRecipe);
        var max = context.RecipeProvider.GetValue<float>(FlowMaxRecipe);
        return min != null && max != null ? $"[{min:F1} .. {max:F1}]" : null;
    }

    /// <summary>
    /// Выполняет проверку расхода воды в контуре ГВС.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск проверки расхода воды в контуре ГВС");

        testResultsService.Remove("DHW_Flow_Rate");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<CheckResult>()
                .WaitForTrue(EndTag, () => CheckResult.Success, "End")
                .WaitForTrue(ErrorTag, () => CheckResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            CheckResult.Success => await HandleCompletionAsync(context, isSuccess: true, ct),
            CheckResult.Error => await HandleCompletionAsync(context, isSuccess: false, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает завершение: чтение значения и сохранение результата.
    /// </summary>
    private async Task<TestStepResult> HandleCompletionAsync(TestStepContext context, bool isSuccess, CancellationToken ct)
    {
        var (_, flowRate, error) = await context.OpcUa.ReadAsync<float>(FlowRateTag, ct);
        if (error != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Flow_Rate: {error}");
        }

        var min = context.RecipeProvider.GetValue<float>(FlowMinRecipe)!.Value;
        var max = context.RecipeProvider.GetValue<float>(FlowMaxRecipe)!.Value;
        var status = isSuccess ? 1 : 2;

        testResultsService.Add(
            parameterName: "DHW_Flow_Rate",
            value: $"{flowRate:F3}",
            min: $"{min:F3}",
            max: $"{max:F3}",
            status: status,
            isRanged: true,
            unit: "");

        logger.LogInformation("DHW_Flow_Rate: {Value:F3}, пределы: [{Min:F3} .. {Max:F3}], статус: {Status}",
            flowRate, min, max, status == 1 ? "OK" : "NOK");

        var msg = $"DHW_Flow_Rate: {flowRate:F3}";

        if (isSuccess)
        {
            logger.LogInformation("Проверка расхода воды в контуре ГВС завершена успешно");

            var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
            return resetResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}") : TestStepResult.Pass(msg);
        }

        return TestStepResult.Fail(msg);
    }

    private enum CheckResult { Success, Error }
}
