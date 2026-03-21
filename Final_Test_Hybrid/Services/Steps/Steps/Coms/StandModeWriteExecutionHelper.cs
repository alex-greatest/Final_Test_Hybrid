using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Общий helper для безопасной записи режима Стенд из execution шагов.
/// </summary>
internal static class StandModeWriteExecutionHelper
{
    private const ushort FailureAddress = 0;
    private static readonly TimeSpan ReadyWaitTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public static Task<DiagnosticWriteResult> ExecuteAsync(
        TestStepContext context,
        IModbusDispatcher dispatcher,
        Func<CancellationToken, Task<DiagnosticWriteResult>> writeAsync,
        IDualLogger logger,
        CancellationToken ct)
    {
        return ExecuteAsync(
            context,
            dispatcher,
            writeAsync,
            logger,
            ReadyWaitTimeout,
            PollInterval,
            ct);
    }

    internal static async Task<DiagnosticWriteResult> ExecuteAsync(
        TestStepContext context,
        IModbusDispatcher dispatcher,
        Func<CancellationToken, Task<DiagnosticWriteResult>> writeAsync,
        IDualLogger logger,
        TimeSpan readyWaitTimeout,
        TimeSpan pollInterval,
        CancellationToken ct)
    {
        var readinessError = await WaitForDispatcherReadyAsync(
            context,
            dispatcher,
            logger,
            readyWaitTimeout,
            pollInterval,
            ct);

        if (readinessError != null)
        {
            return DiagnosticWriteResult.Fail(
                FailureAddress,
                readinessError,
                DiagnosticFailureKind.Communication);
        }

        return await writeAsync(ct);
    }

    private static async Task<string?> WaitForDispatcherReadyAsync(
        TestStepContext context,
        IModbusDispatcher dispatcher,
        IDualLogger logger,
        TimeSpan readyWaitTimeout,
        TimeSpan pollInterval,
        CancellationToken ct)
    {
        var startFailure = GetStoppedDispatcherMessage(dispatcher, "записью режима Стенд");
        if (startFailure != null)
        {
            return startFailure;
        }

        if (IsDispatcherReady(dispatcher))
        {
            return null;
        }

        logger.LogInformation("Ожидание готовности Modbus после reconnect перед записью режима Стенд");

        var waited = TimeSpan.Zero;
        while (waited < readyWaitTimeout)
        {
            ct.ThrowIfCancellationRequested();

            var delay = GetDelayChunk(readyWaitTimeout, pollInterval, waited);
            await context.DelayAsync(delay, ct);
            waited += delay;

            var waitFailure = GetStoppedDispatcherMessage(dispatcher, "ожидания записи режима Стенд");
            if (waitFailure != null)
            {
                return waitFailure;
            }

            if (!IsDispatcherReady(dispatcher))
            {
                continue;
            }

            logger.LogInformation(
                "Готовность Modbus восстановлена через {ElapsedMs} мс, продолжаем запись режима Стенд",
                waited.TotalMilliseconds);
            return null;
        }

        logger.LogWarning(
            "Готовность Modbus не восстановлена за {TimeoutMs} мс перед записью режима Стенд",
            readyWaitTimeout.TotalMilliseconds);

        return $"Готовность Modbus для записи режима Стенд не восстановлена за {readyWaitTimeout.TotalSeconds:F0} с.";
    }

    private static TimeSpan GetDelayChunk(
        TimeSpan readyWaitTimeout,
        TimeSpan pollInterval,
        TimeSpan waited)
    {
        var remaining = readyWaitTimeout - waited;
        return remaining <= pollInterval ? remaining : pollInterval;
    }

    private static string? GetStoppedDispatcherMessage(
        IModbusDispatcher dispatcher,
        string phase)
    {
        return dispatcher.IsStarted
            ? null
            : $"ModbusDispatcher остановлен перед {phase}.";
    }

    private static bool IsDispatcherReady(IModbusDispatcher dispatcher)
    {
        return dispatcher is { IsStarted: true, IsConnected: true, IsReconnecting: false, LastPingData: not null };
    }
}
