using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.CH;

/// <summary>
/// Тестовый шаг открытия контура отопления.
/// </summary>
public class OpenCircuitNormalStep(
    DualLogger<OpenCircuitNormalStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions
{
    private const string BlockPath = "DB_VI.CH.Open_Circuit_Normal";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"CH\".\"Open_Circuit_Normal\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"CH\".\"Open_Circuit_Normal\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"CH\".\"Open_Circuit_Normal\".\"Error\"";

    public string Id => "ch-open-circuit-normal";
    public string Name => "CH/Open_Circuit_Normal";
    public string Description => "Открытие контура Отопления.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг открытия контура отопления.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск открытия контура отопления");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции открытия контура.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<OpenCircuitResult>()
                .WaitForTrue(EndTag, () => OpenCircuitResult.Success, "End")
                .WaitForTrue(ErrorTag, () => OpenCircuitResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            OpenCircuitResult.Success => await HandleSuccessAsync(context, ct),
            OpenCircuitResult.Error => TestStepResult.Fail("Ошибка открытия контура отопления"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение открытия контура.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Открытие контура отопления завершено успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}") : TestStepResult.Pass();
    }

    private enum OpenCircuitResult { Success, Error }
}
