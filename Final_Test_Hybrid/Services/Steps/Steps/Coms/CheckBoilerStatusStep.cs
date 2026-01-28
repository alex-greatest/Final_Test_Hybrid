using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Проверяет статус котла и корректность подключения датчиков.
/// </summary>
public class CheckBoilerStatusStep(
    IOptions<DiagnosticSettings> settings,
    DualLogger<CheckBoilerStatusStep> logger) : ITestStep
{
    private const ushort RegisterLastError = 1047;
    private const ushort RegisterChTemperature = 1006;
    private const ushort ErrorIdE2 = 3;
    private const ushort ErrorIdA7 = 4;
    private const ushort ErrorIdC4 = 9;
    private const ushort ErrorIdC6 = 8;
    private const ushort ErrorIdC7 = 7;
    private const ushort ErrorIdC1 = 10;
    private const ushort ErrorIdCE = 14;
    private const ushort ErrorIdD7 = 12;
    private const ushort ErrorIdEA = 2;
    private const ushort ErrorIdFD = 18;
    private const ushort ErrorIdE9 = 1;
    private const int TemperatureThreshold = 100;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-check-boiler-status";
    public string Name => "Coms/Check_Boiler_Status";
    public string Description => "Запуск мониторинга статуса котла";

    /// <summary>
    /// Проверяет наличие критичных ошибок котла.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var errorResult = await ReadLastErrorAsync(context, ct);
        if (!errorResult.Success)
        {
            var msg = $"Ошибка при чтении регистра {RegisterLastError}. {errorResult.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var errorId = errorResult.Value;

        if (errorId == ErrorIdE2)
        {
            return FailWithLog("E2", "Проверить подключение CH NTC");
        }

        if (errorId == ErrorIdA7)
        {
            return FailWithLog("A7", "Проверить подключение DHW NTC");
        }

        if (errorId == ErrorIdC4)
        {
            return FailWithLog("C4", "Проверить подключение APS");
        }

        if (errorId == ErrorIdC6)
        {
            return FailWithLog("C6", "Проверить подключение APS");
        }

        if (errorId == ErrorIdC7)
        {
            return FailWithLog("C7", "Проверить подключение вентилятора");
        }

        if (errorId == ErrorIdC1)
        {
            return FailWithLog("C1", "Проверить подключение вентилятора");
        }

        if (errorId == ErrorIdCE)
        {
            return FailWithLog("CE", "Проверить подключение датчика давления");
        }

        if (errorId == ErrorIdD7)
        {
            return FailWithLog("D7", "Проверить подключение модулирующей катушки");
        }

        if (errorId == ErrorIdEA)
        {
            return FailWithLog("EA", "Проверить электрод розжига");
        }

        if (errorId == ErrorIdFD)
        {
            return FailWithLog("FD", "Залипание кнопок");
        }

        if (errorId == ErrorIdE9)
        {
            var tempResult = await ReadChTemperatureAsync(context, ct);
            if (!tempResult.Success)
            {
                var msg = $"Ошибка при чтении регистра {RegisterChTemperature}. {tempResult.Error}";
                logger.LogError(msg);
                return TestStepResult.Fail(msg);
            }

            if (tempResult.Value < TemperatureThreshold)
            {
                logger.LogError("Ошибка E9: температура {Temp}°C < {Threshold}°C",
                    tempResult.Value, TemperatureThreshold);
                return TestStepResult.Fail("Проверить подключение STB");
            }
        }

        logger.LogInformation("Проверка статуса котла пройдена");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает последнюю ошибку из регистра.
    /// </summary>
    private async Task<DiagnosticReadResult<ushort>> ReadLastErrorAsync(
        TestStepContext context, CancellationToken ct)
    {
        var address = (ushort)(RegisterLastError - _settings.BaseAddressOffset);
        return await context.DiagReader.ReadUInt16Async(address, ct);
    }

    /// <summary>
    /// Читает температуру подающей линии CH.
    /// </summary>
    private async Task<DiagnosticReadResult<short>> ReadChTemperatureAsync(
        TestStepContext context, CancellationToken ct)
    {
        var address = (ushort)(RegisterChTemperature - _settings.BaseAddressOffset);
        return await context.DiagReader.ReadInt16Async(address, ct);
    }

    /// <summary>
    /// Логирует ошибку и возвращает Fail результат.
    /// </summary>
    private TestStepResult FailWithLog(string errorCode, string message)
    {
        logger.LogError("Ошибка {Code}: {Message}", errorCode, message);
        return TestStepResult.Fail(message);
    }
}
