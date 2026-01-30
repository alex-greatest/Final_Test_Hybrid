using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг чтения установленной температуры контура ГВС (Read DHW Poti).
/// Читает значение из регистра 1013 и проверяет соответствие диапазону 35..60 °С.
/// </summary>
public class ReadDhwPotiSetpointStep(
    IOptions<DiagnosticSettings> settings,
    ITestResultsService testResultsService,
    DualLogger<ReadDhwPotiSetpointStep> logger) : ITestStep, IProvideLimits
{
    private const ushort RegisterDhwTempSetpoint = 1013;
    private const ushort MinValue = 35;
    private const ushort MaxValue = 60;
    private const string ResultName = "DHW_Temp_SP";
    private const string Unit = "°С";

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-read-dhw-poti-setpoint";
    public string Name => "Coms/Read_DHW_Poti_Setpoint";
    public string Description => "Чтение установленной температуры контура ГВС";

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        return $"{MinValue} .. {MaxValue} {Unit}";
    }

    /// <summary>
    /// Выполняет чтение и верификацию установленной температуры ГВС.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        ClearPreviousResults();

        logger.LogInformation("Чтение установленной температуры ГВС из регистра {Register}", RegisterDhwTempSetpoint);

        var (success, actualValue, result) = await ReadDhwTempSetpointAsync(context, ct);
        if (!success)
        {
            return result!;
        }

        var isInRange = actualValue is >= MinValue and <= MaxValue;

        testResultsService.Add(
            parameterName: ResultName,
            value: actualValue.ToString(),
            min: MinValue.ToString(),
            max: MaxValue.ToString(),
            status: isInRange ? 1 : 0,
            isRanged: true,
            unit: Unit);

        logger.LogInformation("Установленная температура ГВС: {Actual} {Unit}, диапазон: [{Min}..{Max}], статус: {Status}",
            actualValue, Unit, MinValue, MaxValue, isInRange ? "OK" : "NOK");

        var msg = $"{actualValue} {Unit}";

        if (!isInRange)
        {
            logger.LogError("Установленная температура ГВС ({Actual} {Unit}) вне допустимого диапазона [{Min}..{Max}]",
                actualValue, Unit, MinValue, MaxValue);
            return TestStepResult.Fail(msg);
        }

        return TestStepResult.Pass(msg);
    }

    /// <summary>
    /// Читает установленную температуру ГВС из регистра 1013.
    /// </summary>
    private async Task<(bool Success, ushort Value, TestStepResult? Result)> ReadDhwTempSetpointAsync(
        TestStepContext context, CancellationToken ct)
    {
        var address = (ushort)(RegisterDhwTempSetpoint - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении регистра {RegisterDhwTempSetpoint}. {result.Error}";
            logger.LogError(msg);
            return (false, 0, TestStepResult.Fail(msg));
        }

        return (true, result.Value, null);
    }

    /// <summary>
    /// Очищает предыдущие результаты для поддержки Retry.
    /// </summary>
    private void ClearPreviousResults()
    {
        testResultsService.Remove(ResultName);
    }
}
