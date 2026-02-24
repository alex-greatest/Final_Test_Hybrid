using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.DHW;

/// <summary>
/// Тестовый шаг установки режима бойлера (БКН) с измерением и валидацией Tank_Mode.
/// </summary>
public class SetTankModeStep(
    DualLogger<SetTankModeStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.DHW.Set_Tank_Mode";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Set_Tank_Mode\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Set_Tank_Mode\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Set_Tank_Mode\".\"Error\"";
    private const string TankModeTag = "ns=3;s=\"DB_Parameter\".\"DHW\".\"Tank_Mode\"";
    private const string TankModeRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"Tank\".\"Mode\"";
    private const float TankModeMaxLimit = 60f;

    public string Id => "dhw-set-tank-mode";
    public string Name => "DHW/Set_Tank_Mode";
    public string Description => "Давление воды в режиме БКН";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, TankModeTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [TankModeRecipe];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        var tankModeLimit = context.RecipeProvider.GetValue<float>(TankModeRecipe);
        return tankModeLimit != null ? $">= {tankModeLimit:F1}" : null;
    }

    /// <summary>
    /// Выполняет шаг установки режима бойлера.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск установки режима бойлера");

        testResultsService.Remove("Tank_DHW_Mode");

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
            context.TagWaiter.CreateWaitGroup<TankModeResult>()
                .WaitForTrue(EndTag, () => TankModeResult.Success, "End")
                .WaitForTrue(ErrorTag, () => TankModeResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            TankModeResult.Success => await HandleCompletionAsync(context, isSuccess: true, ct),
            TankModeResult.Error => await HandleCompletionAsync(context, isSuccess: false, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает завершение: чтение Tank_Mode, валидация и сохранение результата.
    /// </summary>
    private async Task<TestStepResult> HandleCompletionAsync(TestStepContext context, bool isSuccess, CancellationToken ct)
    {
        var (_, tankMode, error) = await context.OpcUa.ReadAsync<float>(TankModeTag, ct);
        if (error != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Tank_Mode: {error}");
        }

        var tankModeLimit = context.RecipeProvider.GetValue<float>(TankModeRecipe)!.Value;
        var status = isSuccess ? 1 : 2;

        testResultsService.Add(
            parameterName: "Tank_DHW_Mode",
            value: $"{tankMode:F3}",
            min: $"{tankModeLimit:F3}",
            max: $"{TankModeMaxLimit:F3}",
            status: status,
            isRanged: true,
            unit: "",
            test: Name);

        logger.LogInformation("Tank_Mode: {TankMode:F3}, порог: {Threshold:F3}, статус: {Status}",
            tankMode, tankModeLimit, status == 1 ? "OK" : "NOK");

        var msg = $"Tank_Mode: {tankMode:F3}";

        if (isSuccess)
        {
            logger.LogInformation("Установка режима бойлера завершена успешно");

            var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
            return resetResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}") : TestStepResult.Pass(msg);
        }

        return TestStepResult.Fail(msg);
    }

    private enum TankModeResult { Success, Error }
}
