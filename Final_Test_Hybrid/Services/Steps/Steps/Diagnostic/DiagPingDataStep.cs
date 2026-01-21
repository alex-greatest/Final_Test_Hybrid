using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Diagnostic;

/// <summary>
/// Ждёт появления LastPingData и логирует ModeKey + BoilerStatus.
/// </summary>
public class DiagPingDataStep(
    IModbusDispatcher dispatcher,
    DualLogger<DiagPingDataStep> logger) : ITestStep
{
    private const int MaxWaitMs = 10_000;
    private const int PollIntervalMs = 500;

    public string Id => "diag-ping-data";
    public string Name => "DiagPingData";
    public string Description => "Проверка ping данных ModbusDispatcher";

    /// <summary>
    /// Выполняет ожидание и проверку данных ping.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("▶ Ожидание данных ping...");

        var waited = 0;

        while (dispatcher.LastPingData == null && waited < MaxWaitMs)
        {
            await context.DelayAsync(TimeSpan.FromMilliseconds(PollIntervalMs), ct);
            waited += PollIntervalMs;
            logger.LogDebug("Ожидание... {Waited}ms", waited);
        }

        var pingData = dispatcher.LastPingData;

        if (pingData == null)
        {
            logger.LogError("◼ Таймаут: LastPingData не получен за {MaxWait}ms", MaxWaitMs);
            return TestStepResult.Fail("Ping данные не получены");
        }

        var modeDescription = GetModeDescription(pingData.ModeKey);

        logger.LogInformation(
            "◼ Ping данные получены:\n" +
            "  ModeKey: 0x{ModeKey:X8} ({ModeDescription})\n" +
            "  BoilerStatus: {Status}",
            pingData.ModeKey,
            modeDescription,
            pingData.BoilerStatus);

        return TestStepResult.Pass($"ModeKey: 0x{pingData.ModeKey:X8} ({modeDescription}), Status: {pingData.BoilerStatus}");
    }

    /// <summary>
    /// Возвращает описание режима по ключу.
    /// </summary>
    private static string GetModeDescription(uint modeKey) => modeKey switch
    {
        0xD7F8DB56 => "Стендовый режим",
        0xFA87CD5E => "Инженерный режим",
        _ => "Обычный режим"
    };
}
