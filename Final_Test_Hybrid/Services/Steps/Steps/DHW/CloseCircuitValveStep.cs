using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.DHW;

/// <summary>
/// Тестовый шаг закрытия клапанов контура ГВС.
/// </summary>
public class CloseCircuitValveStep(
    DualLogger<CloseCircuitValveStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions
{
    private const string BlockPath = "DB_VI.DHW.Close_Circuit_Valve";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Close_Circuit_Valve\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Close_Circuit_Valve\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Close_Circuit_Valve\".\"Error\"";

    public string Id => "dhw-close-circuit-valve";
    public string Name => "DHW/Close_Circuit_Valve";
    public string Description => "Контур ГВС. Закрытие клапанов контура.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг закрытия клапанов контура ГВС.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск закрытия клапанов контура ГВС");

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
            context.TagWaiter.CreateWaitGroup<CloseValveResult>()
                .WaitForTrue(EndTag, () => CloseValveResult.Success, "End")
                .WaitForTrue(ErrorTag, () => CloseValveResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            CloseValveResult.Success => await HandleSuccessAsync(context, ct),
            CloseValveResult.Error => TestStepResult.Fail("Ошибка закрытия клапанов контура ГВС"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение закрытия клапанов.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Закрытие клапанов контура ГВС завершено успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}") : TestStepResult.Pass();
    }

    private enum CloseValveResult { Success, Error }
}
