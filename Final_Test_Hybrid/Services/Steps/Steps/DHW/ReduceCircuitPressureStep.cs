using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.DHW;

/// <summary>
/// Тестовый шаг уменьшения давления в контуре ГВС.
/// </summary>
public class ReduceCircuitPressureStep(
    DualLogger<ReduceCircuitPressureStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions
{
    private const string BlockPath = "DB_VI.DHW.Reduce_Circuit_Pressure";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Reduce_Circuit_Pressure\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Reduce_Circuit_Pressure\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Reduce_Circuit_Pressure\".\"Error\"";

    public string Id => "dhw-reduce-circuit-pressure";
    public string Name => "DHW/Reduce_Circuit_Pressure";
    public string Description => "Контур ГВС. Уменьшение давления.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг уменьшения давления в контуре ГВС.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск уменьшения давления в контуре ГВС");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции уменьшения давления.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<ReduceResult>()
                .WaitForTrue(EndTag, () => ReduceResult.Success, "End")
                .WaitForTrue(ErrorTag, () => ReduceResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            ReduceResult.Success => await HandleSuccessAsync(context, ct),
            ReduceResult.Error => TestStepResult.Fail("Ошибка уменьшения давления контура ГВС"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение уменьшения давления.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Уменьшение давления в контуре ГВС завершено успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}") : TestStepResult.Pass();
    }

    private enum ReduceResult { Success, Error }
}
