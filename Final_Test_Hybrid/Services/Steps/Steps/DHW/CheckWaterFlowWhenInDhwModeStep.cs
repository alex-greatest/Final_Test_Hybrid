using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.DHW;

/// <summary>
/// Тестовый шаг для проверки расхода воды в контуре Отопления в режиме ГВС.
/// </summary>
public class CheckWaterFlowWhenInDhwModeStep(
    DualLogger<CheckWaterFlowWhenInDhwModeStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.DHW.Check_Water_Flow_when_in_DHW_Mode";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Check_Water_Flow_when_in_DHW_Mode\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Check_Water_Flow_when_in_DHW_Mode\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Check_Water_Flow_when_in_DHW_Mode\".\"Error\"";
    private const string FlowTag = "ns=3;s=\"DB_Parameter\".\"CH\".\"Flow_In_DHW_Mode\"";
    private const string MaxRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"CH_Flow_in_DHW_mode\"";
    private const float MinConstant = -1.0f;

    public string Id => "dhw-check-water-flow-when-in-dhw-mode";
    public string Name => "DHW/Check_Water_Flow_When_In_DHW_Mode";
    public string Description => "Контур ГВС. Проверка расхода в контуре Отопления в режиме ГВС.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, FlowTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [MaxRecipe];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        var max = context.RecipeProvider.GetValue<float>(MaxRecipe);
        return max != null ? $"[{MinConstant:F1} .. {max:F1}]" : null;
    }

    /// <summary>
    /// Выполняет проверку расхода воды в контуре Отопления в режиме ГВС.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск проверки расхода воды в контуре CH в режиме ГВС");

        testResultsService.Remove("CH_Flow_In_DHW_Mode");

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
        var (_, flowValue, error) = await context.OpcUa.ReadAsync<float>(FlowTag, ct);
        if (error != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Flow_In_DHW_Mode: {error}");
        }

        var max = context.RecipeProvider.GetValue<float>(MaxRecipe)!.Value;
        var status = isSuccess ? 1 : 2;

        testResultsService.Add(
            parameterName: "CH_Flow_In_DHW_Mode",
            value: $"{flowValue:F3}",
            min: $"{MinConstant:F3}",
            max: $"{max:F3}",
            status: status,
            isRanged: true,
            unit: "");

        logger.LogInformation("CH_Flow_In_DHW_Mode: {Value:F3}, пределы: [{Min:F3} .. {Max:F3}], статус: {Status}",
            flowValue, MinConstant, max, status == 1 ? "OK" : "NOK");

        var msg = $"CH_Flow_In_DHW_Mode: {flowValue:F3}";

        if (isSuccess)
        {
            logger.LogInformation("Проверка расхода воды в контуре CH в режиме ГВС завершена успешно");

            var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
            return resetResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}") : TestStepResult.Pass(msg);
        }

        return TestStepResult.Fail(msg);
    }

    private enum CheckResult { Success, Error }
}
