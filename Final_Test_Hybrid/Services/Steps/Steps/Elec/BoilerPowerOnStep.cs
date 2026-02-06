using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Elec;

/// <summary>
/// Тестовый шаг включения котла и подачи питания.
/// </summary>
public class BoilerPowerOnStep(
    DualLogger<BoilerPowerOnStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions
{
    private const string BlockPath = "DB_VI.Elec.Boiler_Power_ON";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Boiler_Power_ON\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Boiler_Power_ON\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Boiler_Power_ON\".\"Error\"";

    public string Id => "elec-boiler-power-on";
    public string Name => "Elec/Boiler_Power_ON";
    public string Description => "Включение котла, подача питания.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг включения котла.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск включения котла");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции включения котла.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<PowerOnResult>()
                .WaitForTrue(EndTag, () => PowerOnResult.Success, "End")
                .WaitForTrue(ErrorTag, () => PowerOnResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            PowerOnResult.Success => await HandleSuccessAsync(context, ct),
            PowerOnResult.Error => TestStepResult.Fail("Ошибка включения котла"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное включение котла.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Котёл включен успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null
            ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}")
            : TestStepResult.Pass();
    }

    private enum PowerOnResult { Success, Error }
}
