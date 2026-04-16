using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг запуска максимального нагрева контура отопления без связи с котлом.
/// </summary>
public class ChStartMaxHeatoutWithoutStep(
    DualLogger<ChStartMaxHeatoutWithoutStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions
{
    private const string BlockPath = "DB_Coms.DB_CH_Start_Max_Heatout_Without";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Max_Heatout_Without\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Max_Heatout_Without\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Max_Heatout_Without\".\"Error\"";

    public string Id => "coms-ch-start-max-heatout-without";
    public string Name => "Coms/CH_Start_Max_Heatout_Without";
    public string Description => "Запуск максимального нагрева контура отопления без связи с котлом";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет запуск максимального нагрева без Modbus-связи с котлом.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск максимального нагрева контура отопления без связи с котлом");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения PLC-блока.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<HeatoutWithoutResult>()
                .WaitForTrue(EndTag, () => HeatoutWithoutResult.Success, "End")
                .WaitForTrue(ErrorTag, () => HeatoutWithoutResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            HeatoutWithoutResult.Success => await HandleSuccessAsync(context, ct),
            HeatoutWithoutResult.Error => TestStepResult.Fail("Ошибка запуска максимального нагрева без связи с котлом"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение PLC-блока.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Максимальный нагрев без связи с котлом запущен успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null
            ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}")
            : TestStepResult.Pass();
    }

    private enum HeatoutWithoutResult { Success, Error }
}
