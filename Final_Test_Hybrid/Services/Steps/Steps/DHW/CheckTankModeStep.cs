using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.DHW;

/// <summary>
/// Тестовый шаг проверки расхода воды в режиме БКН.
/// </summary>
public class CheckTankModeStep(
    DualLogger<CheckTankModeStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.DHW.Check_Tank_Mode";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Check_Tank_Mode\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Check_Tank_Mode\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Check_Tank_Mode\".\"Error\"";
    private const string TankPressTag = "ns=3;s=\"DB_Parameter\".\"DHW\".\"Check_Tank_Mode\"";
    private const string WaterMinRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"Tank\".\"WaterMin\"";
    private const string WaterMaxRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"Tank\".\"WaterMax\"";

    public string Id => "dhw-check-tank-mode";
    public string Name => "DHW/Check_Tank_Mode";
    public string Description => "ГВС. Проверка расхода воды режима БКН.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, TankPressTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [WaterMinRecipe, WaterMaxRecipe];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        var min = context.RecipeProvider.GetValue<float>(WaterMinRecipe);
        var max = context.RecipeProvider.GetValue<float>(WaterMaxRecipe);
        return min != null && max != null ? $"[{min:F1} .. {max:F1}]" : null;
    }

    /// <summary>
    /// Выполняет шаг проверки расхода воды.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск проверки расхода воды режима БКН");

        testResultsService.Remove("Tank_DHW_Press");

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
    /// Обрабатывает завершение: чтение значения, валидация и сохранение результата.
    /// </summary>
    private async Task<TestStepResult> HandleCompletionAsync(TestStepContext context, bool isSuccess, CancellationToken ct)
    {
        var (_, tankPress, error) = await context.OpcUa.ReadAsync<float>(TankPressTag, ct);
        if (error != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Check_Tank_Mode: {error}");
        }

        var min = context.RecipeProvider.GetValue<float>(WaterMinRecipe)!.Value;
        var max = context.RecipeProvider.GetValue<float>(WaterMaxRecipe)!.Value;
        var status = isSuccess ? 1 : 2;

        testResultsService.Add(
            parameterName: "Tank_DHW_Press",
            value: $"{tankPress:F3}",
            min: $"{min:F3}",
            max: $"{max:F3}",
            status: status,
            isRanged: true,
            unit: "");

        logger.LogInformation("Tank_DHW_Press: {Value:F3}, пределы: [{Min:F3} .. {Max:F3}], статус: {Status}",
            tankPress, min, max, status == 1 ? "OK" : "NOK");

        var msg = $"Tank_DHW_Press: {tankPress:F3}";

        if (isSuccess)
        {
            logger.LogInformation("Проверка расхода воды режима БКН завершена успешно");

            var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
            return resetResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}") : TestStepResult.Pass(msg);
        }

        // При Error НЕ передаём ошибки - они активируются автоматически от PLC
        return TestStepResult.Fail(msg);
    }

    private enum CheckResult { Success, Error }
}
