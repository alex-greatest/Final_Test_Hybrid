using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Diagnostic.Services;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Шаг проверки связи с котлом через Modbus.
/// Оператор подключает кабель связи, шаг запускает диагностическое соединение и ждёт установки связи.
/// При AutoReady = false выполняет fail-fast с NoDiagnosticConnection.
/// Реализует INonSkippable — пропуск запрещён (связь обязательна для теста).
/// </summary>
public class CheckCommsStep(
    IModbusDispatcher dispatcher,
    DiagnosticDispatcherOwnership dispatcherOwnership,
    AutoReadySubscription autoReady,
    ExecutionPhaseState phaseState,
    DualLogger<CheckCommsStep> logger) : ITestStep, INonSkippable
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromMinutes(2);
    private const int PollIntervalMs = 500;
    private const int LogIntervalMs = 10_000;

    public string Id => "coms-check-comms";
    public string Name => "Coms/Check_Comms";
    public string Description => "Провека связи с котлом";

    /// <summary>
    /// Выполняет проверку связи с котлом.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        phaseState.SetPhase(ExecutionPhase.WaitingForDiagnosticConnection);
        _ = context.ColumnIndex;
        DiagnosticDispatcherLease? dispatcherLease = null;

        try
        {
            if (!autoReady.IsReady)
            {
                logger.LogWarning("AutoReady=false: проверка связи с котлом прервана");
                return BuildNoConnectionResult();
            }

            dispatcherLease = dispatcherOwnership.AcquireRuntimeLease();
            if (dispatcherLease.ShouldStartDispatcher)
            {
                await dispatcher.StartAsync(ct);
            }

            var pingData = await WaitForPingDataAsync(ct);

            if (pingData == null)
            {
                return BuildNoConnectionResult();
            }

            logger.LogInformation(
                "Связь с котлом установлена. ModeKey: 0x{ModeKey:X8}, BoilerStatus: {Status}",
                pingData.ModeKey, pingData.BoilerStatus);

            dispatcherLease.PromoteToPersistentRuntimeOwnership();
            dispatcherLease = null;
            return TestStepResult.Pass();
        }
        finally
        {
            try
            {
                if (dispatcherLease?.Release().ShouldStopDispatcher == true)
                {
                    await StopDispatcherSafelyAsync();
                }
            }
            finally
            {
                phaseState.Clear();
            }
        }
    }

    /// <summary>
    /// Ожидает получения ping данных от котла.
    /// </summary>
    private async Task<DiagnosticPingData?> WaitForPingDataAsync(CancellationToken ct)
    {
        var waited = TimeSpan.Zero;
        var lastLogTime = TimeSpan.Zero;

        while (waited < ConnectionTimeout)
        {
            ct.ThrowIfCancellationRequested();

            if (!autoReady.IsReady)
            {
                logger.LogWarning("AutoReady=false во время ожидания связи с котлом");
                return null;
            }

            var pingData = dispatcher.LastPingData;
            if (pingData != null)
            {
                return pingData;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(PollIntervalMs), ct);
            waited += TimeSpan.FromMilliseconds(PollIntervalMs);

            if ((waited - lastLogTime).TotalMilliseconds < LogIntervalMs)
            {
                continue;
            }

            logger.LogDebug("Ожидание ping данных... {Elapsed:F0}с", waited.TotalSeconds);
            lastLogTime = waited;
        }

        return null;
    }

    private static TestStepResult BuildNoConnectionResult()
    {
        return TestStepResult.Fail(
            "Нет связи с котлом",
            errors: [ErrorDefinitions.NoDiagnosticConnection]);
    }

    private async Task StopDispatcherSafelyAsync()
    {
        try
        {
            await dispatcher.StopAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка остановки ModbusDispatcher после неуспешной проверки связи");
        }
    }
}
