using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.DHW;

/// <summary>
/// Тестовый шаг для регулировки давления воды в контуре ГВС с измерением и записью результата.
/// </summary>
public class SetCircuitPressureStep(
    DualLogger<SetCircuitPressureStep> logger,
    ITestResultsService testResultsService) : IHasPlcBlockPath, IRequiresPlcSubscriptions, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.DHW.Set_Circuit_Pressure";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Set_Circuit_Pressure\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Set_Circuit_Pressure\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Set_Circuit_Pressure\".\"Error\"";
    private const string InPressTag = "ns=3;s=\"DB_Parameter\".\"DHW\".\"In_Press\"";
    private const string TargetRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"PresTest\".\"Value\"";
    private const string ToleranceRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"PresTest\".\"Tol\"";

    public string Id => "dhw-set-circuit-pressure";
    public string Name => "DHW/Set_Circuit_Pressure";
    public string Description => "Контур ГВС. Регулировка давления воды";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, InPressTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [TargetRecipe, ToleranceRecipe];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        var target = context.RecipeProvider.GetValue<float>(TargetRecipe);
        var tolerance = context.RecipeProvider.GetValue<float>(ToleranceRecipe);
        if (target == null || tolerance == null)
        {
            return null;
        }

        var (min, max) = GetPressureLimits(target.Value, tolerance.Value);
        return $"[{min:F1} .. {max:F1}]";
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

        var target = context.RecipeProvider.GetValue<float>(TargetRecipe)!.Value;
        var tolerance = context.RecipeProvider.GetValue<float>(ToleranceRecipe)!.Value;
        var (min, max) = GetPressureLimits(target, tolerance);
        var status = isSuccess ? 1 : 2;

        testResultsService.Add(
            parameterName: "DHW_In_Pres",
            value: $"{inPress:F3}",
            min: $"{min:F3}",
            max: $"{max:F3}",
            status: status,
            isRanged: true,
            unit: "",
            test: Name);

        logger.LogInformation("DHW_In_Pres: {Value:F3}, пределы: [{Min:F3} .. {Max:F3}], статус: {Status}",
            inPress, min, max, status == 1 ? "OK" : "NOK");

        var msg = $"DHW_In_Pres: {inPress:F3}";
        if (!isSuccess)
        {
            // При Error НЕ передаём ошибки - они активируются автоматически от PLC
            return TestStepResult.Fail(msg);
        }

        logger.LogInformation("Регулировка давления воды в контуре ГВС завершена успешно");

        var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return resetResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}") : TestStepResult.Pass(msg);
    }

    private static (float Min, float Max) GetPressureLimits(float target, float tolerance)
    {
        return (target - tolerance, target + tolerance);
    }

    private enum CheckResult { Success, Error }
}
