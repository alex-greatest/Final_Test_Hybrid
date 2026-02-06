using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.CH;

/// <summary>
/// Тестовый шаг слива воды из контура отопления.
/// </summary>
public class DrainCircuitStep(DualLogger<DrainCircuitStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions
{
    private const string BlockPath = "DB_VI.CH.Drain_Circuit";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"CH\".\"Drain_Circuit\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"CH\".\"Drain_Circuit\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"CH\".\"Drain_Circuit\".\"Error\"";

    public string Id => "ch-drain-circuit";
    public string Name => "CH/Drain_Circuit";
    public string Description => "Контур Отопления. Слив воды.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг слива воды из контура отопления.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск слива воды из контура отопления");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции слива воды.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<DrainResult>()
                .WaitForTrue(EndTag, () => DrainResult.Success, "End")
                .WaitForTrue(ErrorTag, () => DrainResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            DrainResult.Success => await HandleSuccessAsync(context, ct),
            DrainResult.Error => TestStepResult.Fail("Ошибка слива воды из контура"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение слива воды.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Слив воды из контура завершён успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}") : TestStepResult.Pass();
    }

    private enum DrainResult { Success, Error }
}
