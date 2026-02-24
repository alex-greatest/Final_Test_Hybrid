using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.DHW;

/// <summary>
/// Тестовый шаг для регулировки давления воды в контуре ГВС с измерением и записью результата.
/// </summary>
public class SetCircuitPressureStep(
    DualLogger<SetCircuitPressureStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.DHW.Set_Circuit_Pressure";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Set_Circuit_Pressure\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Set_Circuit_Pressure\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Set_Circuit_Pressure\".\"Error\"";
    private const string InPressTag = "ns=3;s=\"DB_Parameter\".\"DHW\".\"In_Press\"";
    private const string MinRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"PresTest\".\"Value\"";
    private const float MaxValue = 999.000f;

    public string Id => "dhw-set-circuit-pressure";
    public string Name => "DHW/Set_Circuit_Pressure";
    public string Description => "Контур ГВС. Регулировка давления воды";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, InPressTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [MinRecipe];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        var min = context.RecipeProvider.GetValue<float>(MinRecipe);
        return min != null ? $"[{min:F1} .. {MaxValue:F1}]" : null;
    }

    /// <summary>
    /// Выполняет регулировку давления воды в контуре ГВС.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск регулировки давления воды в контуре ГВС");

        testResultsService.Remove("DHW_In_Pres");

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
        var (_, inPress, error) = await context.OpcUa.ReadAsync<float>(InPressTag, ct);
        if (error != null)
        {
            return TestStepResult.Fail($"Ошибка чтения In_Press: {error}");
        }

        var min = context.RecipeProvider.GetValue<float>(MinRecipe)!.Value;
        var status = isSuccess ? 1 : 2;

        testResultsService.Add(
            parameterName: "DHW_In_Pres",
            value: $"{inPress:F3}",
            min: $"{min:F3}",
            max: $"{MaxValue:F3}",
            status: status,
            isRanged: true,
            unit: "",
            test: Name);

        logger.LogInformation("DHW_In_Pres: {Value:F3}, пределы: [{Min:F3} .. {Max:F3}], статус: {Status}",
            inPress, min, MaxValue, status == 1 ? "OK" : "NOK");

        var msg = $"DHW_In_Pres: {inPress:F3}";

        if (isSuccess)
        {
            logger.LogInformation("Регулировка давления воды в контуре ГВС завершена успешно");

            var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
            return resetResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}") : TestStepResult.Pass(msg);
        }

        // При Error НЕ передаём ошибки - они активируются автоматически от PLC
        return TestStepResult.Fail(msg);
    }

    private enum CheckResult { Success, Error }
}
