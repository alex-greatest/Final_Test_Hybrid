using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.DHW;

/// <summary>
/// Тестовый шаг заполнения контура ГВС в прямом направлении.
/// </summary>
public class FillCircuitNormalDirectionStep(
    DualLogger<FillCircuitNormalDirectionStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags
{
    private const string BlockPath = "DB_VI.DHW.Fill_Circuit_Normal_Direction";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Fill_Circuit_Normal_Direction\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Fill_Circuit_Normal_Direction\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Fill_Circuit_Normal_Direction\".\"Error\"";

    public string Id => "dhw-fill-circuit-normal-direction";
    public string Name => "DHW/Fill_Circuit_Normal_Direction";
    public string Description => "Заполнение контура ГВС в прямом направлении";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг заполнения контура ГВС в прямом направлении.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск заполнения контура ГВС в прямом направлении");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции заполнения.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<FillResult>()
                .WaitForTrue(EndTag, () => FillResult.Success, "End")
                .WaitForTrue(ErrorTag, () => FillResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            FillResult.Success => await HandleSuccessAsync(context),
            FillResult.Error => TestStepResult.Fail("Ошибка заполнения контура ГВС"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение заполнения.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context)
    {
        logger.LogInformation("Заполнение контура ГВС в прямом направлении завершено успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false);
        return writeResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}") : TestStepResult.Pass();
    }

    private enum FillResult { Success, Error }
}
