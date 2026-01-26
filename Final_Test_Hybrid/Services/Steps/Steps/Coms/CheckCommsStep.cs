using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Шаг проверки связи с котлом через Modbus.
/// Оператор подключает кабель связи, шаг запускает диагностическое соединение и ждёт установки связи.
/// Реализует INonSkippable — пропуск запрещён (связь обязательна для теста).
/// </summary>
public class CheckCommsStep(
    IModbusDispatcher dispatcher,
    ExecutionPhaseState phaseState,
    DualLogger<CheckCommsStep> logger) : ITestStep, INonSkippable
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromMinutes(2);
    private const int PollIntervalMs = 500;
    private const int LogIntervalMs = 10_000;

    public string Id => "coms-check-comms";
    public string Name => "Coms/Check_Comms";
    public string Description => "Проверка связи с котлом";

    /// <summary>
    /// Выполняет проверку связи с котлом.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        phaseState.SetPhase(ExecutionPhase.WaitingForDiagnosticConnection);

        try
        {
            if (!dispatcher.IsStarted)
            {
                await dispatcher.StartAsync(ct);
            }

            var pingData = await WaitForPingDataAsync(context, ct);

            if (pingData == null)
            {
                await dispatcher.StopAsync();
                return TestStepResult.Fail(
                    "Нет связи с котлом",
                    errors: [ErrorDefinitions.NoDiagnosticConnection]);
            }

            logger.LogInformation(
                "Связь с котлом установлена. ModeKey: 0x{ModeKey:X8}, BoilerStatus: {Status}",
                pingData.ModeKey, pingData.BoilerStatus);

            return TestStepResult.Pass();
        }
        finally
        {
            phaseState.Clear();
        }
    }

    /// <summary>
    /// Ожидает получения ping данных от котла.
    /// </summary>
    private async Task<DiagnosticPingData?> WaitForPingDataAsync(TestStepContext context, CancellationToken ct)
    {
        var waited = TimeSpan.Zero;
        var lastLogTime = TimeSpan.Zero;

        while (waited < ConnectionTimeout)
        {
            ct.ThrowIfCancellationRequested();

            var pingData = dispatcher.LastPingData;
            if (pingData != null)
            {
                return pingData;
            }

            await context.DelayAsync(TimeSpan.FromMilliseconds(PollIntervalMs), ct);
            waited += TimeSpan.FromMilliseconds(PollIntervalMs);

            if ((waited - lastLogTime).TotalMilliseconds >= LogIntervalMs)
            {
                logger.LogDebug("Ожидание ping данных... {Elapsed:F0}с", waited.TotalSeconds);
                lastLogTime = waited;
            }
        }

        return null;
    }
}
