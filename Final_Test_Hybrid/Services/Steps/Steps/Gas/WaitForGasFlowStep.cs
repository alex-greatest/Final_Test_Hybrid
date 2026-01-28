using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Gas;

/// <summary>
/// Тестовый шаг ожидания потока газа.
/// </summary>
public class WaitForGasFlowStep(
    DualLogger<WaitForGasFlowStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags
{
    private const string BlockPath = "DB_VI.Gas.Wait_for_Gas_Flow";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Wait_for_Gas_Flow\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Wait_for_Gas_Flow\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Wait_for_Gas_Flow\".\"Error\"";

    public string Id => "gas-wait-for-gas-flow";
    public string Name => "Gas/Wait_for_Gas_Flow";
    public string Description => "Ожидание потока газа.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг ожидания потока газа.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск ожидания потока газа");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции ожидания потока газа.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<WaitForGasFlowResult>()
                .WaitForTrue(EndTag, () => WaitForGasFlowResult.Success, "End")
                .WaitForTrue(ErrorTag, () => WaitForGasFlowResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            WaitForGasFlowResult.Success => await HandleSuccessAsync(context, ct),
            WaitForGasFlowResult.Error => TestStepResult.Fail("Ошибка ожидания потока газа"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение ожидания потока газа.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Ожидание потока газа завершено успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}") : TestStepResult.Pass();
    }

    private enum WaitForGasFlowResult { Success, Error }
}
