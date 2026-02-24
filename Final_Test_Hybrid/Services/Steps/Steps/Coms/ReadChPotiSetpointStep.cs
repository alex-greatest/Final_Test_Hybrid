using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг чтения установленной температуры контура Отопления (CH).
/// Читает значение из регистра 1008 и проверяет соответствие диапазону 40..82 °C.
/// </summary>
public class ReadChPotiSetpointStep(
    IOptions<DiagnosticSettings> settings,
    ITestResultsService testResultsService,
    DualLogger<ReadChPotiSetpointStep> logger) : ITestStep, IProvideLimits
{
    private const ushort RegisterChTempSetpoint = 1008;
    private const ushort MinTemp = 40;
    private const ushort MaxTemp = 82;
    private const string ResultName = "CH_Temp_SP";

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-read-ch-poti-setpoint";
    public string Name => "Coms/Read_CH_Poti_Setpoint";
    public string Description => "Чтение установленной температуры контура Отопления";

    /// <summary>
    /// Возвращает фиксированные пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context) => $"{MinTemp} .. {MaxTemp}";

    /// <summary>
    /// Читает установленную температуру CH из регистра 1008 и проверяет диапазон.
    /// </summary>
    /// <param name="context">Контекст выполнения тестового шага.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        ClearPreviousResults();

        logger.LogInformation("Чтение установленной температуры CH из регистра {Register}", RegisterChTempSetpoint);

        var modbusAddress = (ushort)(RegisterChTempSetpoint - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(modbusAddress, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении регистра {RegisterChTempSetpoint}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var value = result.Value;
        var isInRange = value >= MinTemp && value <= MaxTemp;

        testResultsService.Add(
            parameterName: ResultName,
            value: value.ToString(),
            min: MinTemp.ToString(),
            max: MaxTemp.ToString(),
            status: isInRange ? 1 : 2,
            isRanged: true,
            unit: "°C",
            test: Name);

        logger.LogInformation("Установленная температура CH: {Value} °C, диапазон: [{Min}..{Max}], статус: {Status}",
            value, MinTemp, MaxTemp, isInRange ? "OK" : "NOK");

        var msg2 = $"CH_Temp_SP: {value} °C [{MinTemp}..{MaxTemp}]";

        if (!isInRange)
        {
            logger.LogError("Установленная температура CH ({Value} °C) вне допустимого диапазона [{Min}..{Max}]",
                value, MinTemp, MaxTemp);
            return TestStepResult.Fail(msg2);
        }

        return TestStepResult.Pass(msg2);
    }

    /// <summary>
    /// Очищает предыдущие результаты для поддержки Retry.
    /// </summary>
    private void ClearPreviousResults()
    {
        testResultsService.Remove(ResultName);
    }
}
