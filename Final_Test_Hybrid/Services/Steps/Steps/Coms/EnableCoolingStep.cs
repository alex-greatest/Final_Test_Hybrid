using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг включения охлаждения контура отопления.
/// </summary>
public class EnableCoolingStep(
    DualLogger<EnableCoolingStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions
{
    private const string BlockPath = "DB_VI.CH.Enable_Cooling";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"CH\".\"Enable_Cooling\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"CH\".\"Enable_Cooling\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"CH\".\"Enable_Cooling\".\"Error\"";

    public string Id => "ch-enable-cooling";
    public string Name => "Coms/Enable_Cooling";
    public string Description => "Включение охлаждение контура Отопления";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг включения охлаждения контура отопления.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск включения охлаждения контура отопления");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции включения охлаждения.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<CoolingResult>()
                .WaitForTrue(EndTag, () => CoolingResult.Success, "End")
                .WaitForTrue(ErrorTag, () => CoolingResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            CoolingResult.Success => await HandleSuccessAsync(context, ct),
            CoolingResult.Error => TestStepResult.Fail("Ошибка включения охлаждения контура"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение включения охлаждения.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Включение охлаждения контура завершено успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}") : TestStepResult.Pass();
    }

    private enum CoolingResult { Success, Error }
}
