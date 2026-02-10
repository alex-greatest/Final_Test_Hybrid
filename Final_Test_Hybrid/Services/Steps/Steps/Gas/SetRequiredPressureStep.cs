using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Gas;

/// <summary>
/// Тестовый шаг настройки установочного давления газа на входе.
/// </summary>
public class SetRequiredPressureStep(
    DualLogger<SetRequiredPressureStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions
{
    private const string BlockPath = "DB_VI.Gas.Set_Required_Pressure";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Set_Required_Pressure\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Set_Required_Pressure\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Set_Required_Pressure\".\"Error\"";
    private const string GasPagTag = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_PAG\"";
    private const string GasPaTag = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_Pa\"";

    public string Id => "gas-set-required-pressure";
    public string Name => "Gas/Set_Required_Pressure";
    public string Description => "Настройка установочного давления газа на входе";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, GasPagTag, GasPaTag];

    /// <summary>
    /// Выполняет шаг настройки установочного давления газа.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск настройки установочного давления газа");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionAsync(context, ct);
    }

    /// <summary>
    /// Ожидает завершения операции настройки давления газа.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<SetResult>()
                .WaitForTrue(EndTag, () => SetResult.Success, "End")
                .WaitForTrue(ErrorTag, () => SetResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            SetResult.Success => await HandleCompletionAsync(context, isSuccess: true, ct),
            SetResult.Error => await HandleCompletionAsync(context, isSuccess: false, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает завершение операции: чтение показаний датчиков и сброс Start.
    /// </summary>
    private async Task<TestStepResult> HandleCompletionAsync(TestStepContext context, bool isSuccess, CancellationToken ct)
    {
        var (_, gasPag, gasPagError) = await context.OpcUa.ReadAsync<float>(GasPagTag, ct);
        if (gasPagError != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Gas_PAG: {gasPagError}");
        }

        var (_, gasPa, gasPaError) = await context.OpcUa.ReadAsync<float>(GasPaTag, ct);
        if (gasPaError != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Gas_Pa: {gasPaError}");
        }

        var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        if (resetResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}");
        }

        logger.LogInformation("Gas_PAG: {GasPag:F3}, Gas_Pa: {GasPa:F3}, статус: {Status}",
            gasPag, gasPa, isSuccess ? "OK" : "NOK");

        if (isSuccess)
        {
            return TestStepResult.Pass($"PAG: {gasPag:F3}, Pa: {gasPa:F3}");
        }

        return TestStepResult.Fail($"Ошибка настройки давления газа. PAG: {gasPag:F3}, Pa: {gasPa:F3}");
    }

    private enum SetResult { Success, Error }
}
