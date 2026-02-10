using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Elec;

/// <summary>
/// Тестовый шаг проверки напряжения питания котла.
/// </summary>
public class PowerSupplyTestStep(
    DualLogger<PowerSupplyTestStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.Elec.Power_Supply_Test";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Power_Supply_Test\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Power_Supply_Test\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Power_Supply_Test\".\"Error\"";
    private const string SupplyTag = "ns=3;s=\"DB_Parameter\".\"Blr\".\"Supply\"";
    private const string VoltageMinRecipe = "ns=3;s=\"DB_Recipe\".\"Misc\".\"MainsVoltageMin\"";
    private const string VoltageMaxRecipe = "ns=3;s=\"DB_Recipe\".\"Misc\".\"MainsVoltageMax\"";

    public string Id => "elec-power-supply-test";
    public string Name => "Elec/Power_Supply_Test";
    public string Description => "Проверка напряжения питания котла";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, SupplyTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [VoltageMinRecipe, VoltageMaxRecipe];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        var min = context.RecipeProvider.GetValue<float>(VoltageMinRecipe);
        var max = context.RecipeProvider.GetValue<float>(VoltageMaxRecipe);
        return min != null && max != null ? $"{min:F1} - {max:F1} V" : null;
    }

    /// <summary>
    /// Выполняет шаг проверки напряжения питания котла.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск проверки напряжения питания котла");

        testResultsService.Remove("Blr_Supply");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции проверки напряжения.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<TestResult>()
                .WaitForTrue(EndTag, () => TestResult.Success, "End")
                .WaitForTrue(ErrorTag, () => TestResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            TestResult.Success => await HandleCompletionAsync(context, isSuccess: true, ct),
            TestResult.Error => await HandleCompletionAsync(context, isSuccess: false, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает завершение: чтение напряжения, валидация и сохранение результата.
    /// </summary>
    private async Task<TestStepResult> HandleCompletionAsync(TestStepContext context, bool isSuccess, CancellationToken ct)
    {
        var (_, supply, error) = await context.OpcUa.ReadAsync<float>(SupplyTag, ct);
        if (error != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Supply: {error}");
        }

        var min = context.RecipeProvider.GetValue<float>(VoltageMinRecipe)!.Value;
        var max = context.RecipeProvider.GetValue<float>(VoltageMaxRecipe)!.Value;
        var status = isSuccess ? 1 : 2;

        testResultsService.Add(
            parameterName: "Blr_Supply",
            value: $"{supply:F1}",
            min: $"{min:F1}",
            max: $"{max:F1}",
            status: status,
            isRanged: true,
            unit: "V");

        logger.LogInformation("Напряжение: {Supply:F1} V, пределы: {Min:F1} - {Max:F1}, статус: {Status}",
            supply, min, max, status == 1 ? "OK" : "NOK");

        var msg = $"Напряжение: {supply:F1} V";

        if (isSuccess)
        {
            logger.LogInformation("Проверка напряжения питания завершена успешно");

            var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
            return resetResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}") : TestStepResult.Pass(msg);
        }

        return TestStepResult.Fail(msg);
    }

    private enum TestResult { Success, Error }
}
