using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.CH;

/// <summary>
/// Тестовый шаг закрытия клапанов контура отопления.
/// </summary>
public class CloseCircuitValveStep(
    DualLogger<CloseCircuitValveStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags
{
    private const string BlockPath = "DB_VI.CH.Close_Circuit_Valve";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"CH\".\"Close_Circuit_Valve\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"CH\".\"Close_Circuit_Valve\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"CH\".\"Close_Circuit_Valve\".\"Error\"";

    public string Id => "ch-close-circuit-valve";
    public string Name => "CH/Close_Circuit_Valve";
    public string Description => "Контур Отопления. Закрытие клапанов контура.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг закрытия клапанов контура отопления.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск закрытия клапанов контура отопления");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции закрытия клапанов.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<ValveResult>()
                .WaitForTrue(EndTag, () => ValveResult.Success, "End")
                .WaitForTrue(ErrorTag, () => ValveResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            ValveResult.Success => await HandleSuccessAsync(context, ct),
            ValveResult.Error => TestStepResult.Fail("Ошибка закрытия клапанов контура"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение закрытия клапанов.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Закрытие клапанов контура завершено успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null
            ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}")
            : TestStepResult.Pass();
    }

    private enum ValveResult { Success, Error }
}
