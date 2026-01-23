using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.CH;

/// <summary>
/// Тестовый шаг медленного заполнения контура отопления с измерением и валидацией давления потока.
/// </summary>
public class SlowFillCircuitStep(
    DualLogger<SlowFillCircuitStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.CH.Slow_Fill_Circuit";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"CH\".\"Slow_Fill_Circuit\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"CH\".\"Slow_Fill_Circuit\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"CH\".\"Slow_Fill_Circuit\".\"Error\"";
    private const string FlowPressTag = "ns=3;s=\"DB_Parameter\".\"CH\".\"Flow_Press\"";
    private const string PressTestValueRecipe = "ns=3;s=\"DB_Recipe\".\"CH\".\"PresTestValue\"";

    public string Id => "ch-slow-fill-circuit";
    public string Name => "CH/Slow_Fill_Circuit";
    public string Description => "Контур Отопления. Медленное заполнение контура.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, FlowPressTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [PressTestValueRecipe];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        var pressTestValue = context.RecipeProvider.GetValue<float>(PressTestValueRecipe);
        return pressTestValue != null ? $">= {pressTestValue:F3}" : null;
    }

    /// <summary>
    /// Выполняет шаг медленного заполнения контура отопления.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск медленного заполнения контура отопления");

        testResultsService.Remove("CH_Flow_Press");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции медленного заполнения.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<FillResult>()
                .WaitForTrue(EndTag, () => FillResult.Success, "End")
                .WaitForTrue(ErrorTag, () => FillResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            FillResult.Success => await HandleCompletionAsync(context, isSuccess: true, ct),
            FillResult.Error => await HandleCompletionAsync(context, isSuccess: false, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает завершение операции: чтение Flow_Press, сброс Start, валидация и сохранение результата.
    /// </summary>
    private async Task<TestStepResult> HandleCompletionAsync(TestStepContext context, bool isSuccess, CancellationToken ct)
    {
        var (_, flowPress, error) = await context.OpcUa.ReadAsync<float>(FlowPressTag, ct);
        if (error != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Flow_Press: {error}");
        }

        var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        if (resetResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}");
        }

        var pressTestValue = context.RecipeProvider.GetValue<float>(PressTestValueRecipe)!.Value;
        var status = flowPress >= pressTestValue ? 1 : 2;

        testResultsService.Add(
            parameterName: "CH_Flow_Press",
            value: $"{flowPress:F3}",
            min: $"{pressTestValue:F3}",
            max: "",
            status: status,
            isRanged: false,
            unit: "");

        logger.LogInformation("Давление потока: {FlowPress:F3}, порог: {Threshold:F3}, статус: {Status}",
            flowPress, pressTestValue, status == 1 ? "OK" : "NOK");

        if (isSuccess)
        {
            logger.LogInformation("Медленное заполнение контура завершено успешно");
            return TestStepResult.Pass($"{flowPress:F3}");
        }

        return TestStepResult.Fail($"Ошибка медленного заполнения контура: {flowPress:F3}");
    }

    private enum FillResult { Success, Error }
}
