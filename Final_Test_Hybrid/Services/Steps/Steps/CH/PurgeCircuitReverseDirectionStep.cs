using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.CH;

/// <summary>
/// Тестовый шаг продувки контура отопления в обратном направлении.
/// </summary>
public class PurgeCircuitReverseDirectionStep(
    DualLogger<PurgeCircuitReverseDirectionStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags
{
    private const string BlockPath = "DB_VI.CH.Purge_Circuit_Reverse_Direction";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"CH\".\"Purge_Circuit_Reverse_Direction\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"CH\".\"Purge_Circuit_Reverse_Direction\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"CH\".\"Purge_Circuit_Reverse_Direction\".\"Error\"";

    public string Id => "ch-purge-circuit-reverse-direction";
    public string Name => "CH/Purge_Circuit_Reverse_Direction";
    public string Description => "Контур Отопления. Продувка в обратном направлении.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг продувки контура отопления в обратном направлении.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск продувки контура отопления в обратном направлении");

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
            context.TagWaiter.CreateWaitGroup<PurgeResult>()
                .WaitForTrue(EndTag, () => PurgeResult.Success, "End")
                .WaitForTrue(ErrorTag, () => PurgeResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            PurgeResult.Success => await HandleSuccessAsync(context, ct),
            PurgeResult.Error => TestStepResult.Fail("Ошибка продувки контура отопления"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение продувки.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Продувка контура отопления в обратном направлении завершена успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}") : TestStepResult.Pass();
    }

    private enum PurgeResult { Success, Error }
}
