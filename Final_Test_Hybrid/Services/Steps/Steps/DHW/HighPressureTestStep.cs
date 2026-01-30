using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.DHW;

/// <summary>
/// Тестовый шаг для теста высокого давления контура ГВС.
/// </summary>
public class HighPressureTestStep(
    DualLogger<HighPressureTestStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.DHW.High_Pressure_Test";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"DHW\".\"High_Pressure_Test\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"DHW\".\"High_Pressure_Test\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"DHW\".\"High_Pressure_Test\".\"Error\"";
    private const string HighPressureTag = "ns=3;s=\"DB_Parameter\".\"DHW\".\"High_Pressure\"";
    private const string MinRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"OpenPresReliefValve\".\"Min\"";
    private const string MaxRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"OpenPresReliefValve\".\"Max\"";

    public string Id => "dhw-high-pressure-test";
    public string Name => "DHW/High_Pressure_Test";
    public string Description => "Контур ГВС. Тест высокого давления.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, HighPressureTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [MinRecipe, MaxRecipe];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        var min = context.RecipeProvider.GetValue<float>(MinRecipe);
        var max = context.RecipeProvider.GetValue<float>(MaxRecipe);
        return min != null && max != null ? $"[{min:F1} .. {max:F1}]" : null;
    }

    /// <summary>
    /// Выполняет тест высокого давления.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск теста высокого давления контура ГВС");

        testResultsService.Remove("DHW_High_Pressure");

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
        var (_, highPressure, error) = await context.OpcUa.ReadAsync<float>(HighPressureTag, ct);
        if (error != null)
        {
            return TestStepResult.Fail($"Ошибка чтения High_Pressure: {error}");
        }

        var min = context.RecipeProvider.GetValue<float>(MinRecipe)!.Value;
        var max = context.RecipeProvider.GetValue<float>(MaxRecipe)!.Value;
        var status = isSuccess ? 1 : 2;

        testResultsService.Add(
            parameterName: "DHW_High_Pressure",
            value: $"{highPressure:F3}",
            min: $"{min:F3}",
            max: $"{max:F3}",
            status: status,
            isRanged: true,
            unit: "");

        logger.LogInformation("DHW_High_Pressure: {Value:F3}, пределы: [{Min:F3} .. {Max:F3}], статус: {Status}",
            highPressure, min, max, status == 1 ? "OK" : "NOK");

        var msg = $"DHW_High_Pressure: {highPressure:F3}";

        if (isSuccess)
        {
            logger.LogInformation("Тест высокого давления контура ГВС завершён успешно");

            var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
            return resetResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}") : TestStepResult.Pass(msg);
        }

        // При Error НЕ передаём ошибки - они активируются автоматически от PLC
        return TestStepResult.Fail(msg);
    }

    private enum CheckResult { Success, Error }
}
