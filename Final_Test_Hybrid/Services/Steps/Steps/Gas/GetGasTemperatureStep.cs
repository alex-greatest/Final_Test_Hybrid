using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Gas;

/// <summary>
/// Шаг измерения температуры газа.
/// Читает значение из DB_Measure.Temper.GAS_TAG и сохраняет в результаты теста.
/// </summary>
public class GetGasTemperatureStep(
    DualLogger<GetGasTemperatureStep> logger,
    ITestResultsService testResultsService) : ITestStep, IRequiresPlcSubscriptions
{
    private const string GasTempTag = "ns=3;s=\"DB_Measure\".\"Temper\".\"GAS_TAG\"";

    public string Id => "gas-get-temperature";
    public string Name => "Gas/Get_Temperature";
    public string Description => "Измерение температуры газа";
    public IReadOnlyList<string> RequiredPlcTags => [GasTempTag];

    /// <summary>
    /// Выполняет измерение температуры газа.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск измерения температуры газа");
        testResultsService.Remove("Gas_Temp");

        var (_, gasTemp, opcError) = await context.OpcUa.ReadAsync<float>(GasTempTag, ct);
        if (opcError != null)
        {
            logger.LogError("Ошибка чтения GAS_TAG: {Error}", opcError);
            return TestStepResult.Fail($"Ошибка чтения температуры газа: {opcError}");
        }

        testResultsService.Add(
            parameterName: "Gas_Temp",
            value: $"{gasTemp:F3}",
            min: "",
            max: "",
            status: 1,
            isRanged: false,
            unit: "°C",
            test: Name);

        logger.LogInformation("Температура газа: {GasTemp:F3}°C", gasTemp);
        return TestStepResult.Pass($"{gasTemp:F3}°C");
    }
}
