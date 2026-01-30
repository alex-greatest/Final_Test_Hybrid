using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Elec;

/// <summary>
/// Тестовый шаг выключения котла и отключения питания.
/// </summary>
public class BoilerPowerOffStep(
    DualLogger<BoilerPowerOffStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags
{
    private const string BlockPath = "DB_VI.Elec.Boiler_Power_OFF";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Boiler_Power_OFF\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Boiler_Power_OFF\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Boiler_Power_OFF\".\"Error\"";

    public string Id => "elec-boiler-power-off";
    public string Name => "Elec/Boiler_Power_OFF";
    public string Description => "Выключение котла, отключение питания.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг выключения котла.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск выключения котла");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции выключения котла.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<PowerOffResult>()
                .WaitForTrue(EndTag, () => PowerOffResult.Success, "End")
                .WaitForTrue(ErrorTag, () => PowerOffResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            PowerOffResult.Success => await HandleSuccessAsync(context, ct),
            PowerOffResult.Error => TestStepResult.Fail("Ошибка выключения котла"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное выключение котла.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Котёл выключен успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null
            ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}")
            : TestStepResult.Pass();
    }

    private enum PowerOffResult { Success, Error }
}
