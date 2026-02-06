using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Gas;

/// <summary>
/// Тестовый шаг проверки утечки контура газа.
/// </summary>
public class LeakTestStep(
    DualLogger<LeakTestStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions
{
    private const string BlockPath = "DB_VI.Gas.Leak_Test";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Leak_Test\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Leak_Test\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Leak_Test\".\"Error\"";

    public string Id => "gas-leak-test";
    public string Name => "Gas/Leak_Test";
    public string Description => "Проверка утечки контура газа.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг проверки утечки контура газа.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск проверки утечки контура газа");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции проверки утечки.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<LeakTestResult>()
                .WaitForTrue(EndTag, () => LeakTestResult.Success, "End")
                .WaitForTrue(ErrorTag, () => LeakTestResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            LeakTestResult.Success => await HandleSuccessAsync(context, ct),
            LeakTestResult.Error => TestStepResult.Fail("Ошибка проверки утечки контура газа"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение проверки утечки.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Проверка утечки контура газа завершена успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}") : TestStepResult.Pass();
    }

    private enum LeakTestResult { Success, Error }
}
