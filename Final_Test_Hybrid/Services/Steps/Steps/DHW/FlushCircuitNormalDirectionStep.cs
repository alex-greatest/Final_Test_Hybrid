using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.DHW;

/// <summary>
/// Тестовый шаг продувки контура ГВС в прямом направлении.
/// </summary>
public class FlushCircuitNormalDirectionStep(
    DualLogger<FlushCircuitNormalDirectionStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags
{
    private const string BlockPath = "DB_VI.DHW.Flush_Circuit_Normal_Direction";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Flush_Circuit_Normal_Direction\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Flush_Circuit_Normal_Direction\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Flush_Circuit_Normal_Direction\".\"Error\"";

    public string Id => "dhw-flush-circuit-normal-direction";
    public string Name => "DHW/Flush_DHW_Circuit_Normal_Direction";
    public string Description => "Контур ГВС. Продувка в прямом направлении.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг продувки контура ГВС в прямом направлении.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск продувки контура ГВС в прямом направлении");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции продувки.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<FlushResult>()
                .WaitForTrue(EndTag, () => FlushResult.Success, "End")
                .WaitForTrue(ErrorTag, () => FlushResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            FlushResult.Success => await HandleSuccessAsync(context, ct),
            FlushResult.Error => TestStepResult.Fail("Ошибка продувки контура ГВС"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение продувки.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Продувка контура ГВС в прямом направлении завершена успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}") : TestStepResult.Pass();
    }

    private enum FlushResult { Success, Error }
}
