using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Gas;

/// <summary>
/// Тестовый шаг закрытия клапанов контура газа.
/// </summary>
public class CloseCircuitStep(
    DualLogger<CloseCircuitStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags
{
    private const string BlockPath = "DB_VI.Gas.Close_Circuit";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Close_Circuit\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Close_Circuit\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Close_Circuit\".\"Error\"";

    public string Id => "gas-close-circuit";
    public string Name => "Gas/Close_Circuit";
    public string Description => "Закрытие клапанов контура газа.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг закрытия клапанов контура газа.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск закрытия клапанов контура газа");

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
            context.TagWaiter.CreateWaitGroup<CloseCircuitResult>()
                .WaitForTrue(EndTag, () => CloseCircuitResult.Success, "End")
                .WaitForTrue(ErrorTag, () => CloseCircuitResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            CloseCircuitResult.Success => await HandleSuccessAsync(context, ct),
            CloseCircuitResult.Error => TestStepResult.Fail("Ошибка закрытия клапанов контура газа"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение закрытия клапанов.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Закрытие клапанов контура газа завершено успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}") : TestStepResult.Pass();
    }

    private enum CloseCircuitResult { Success, Error }
}
