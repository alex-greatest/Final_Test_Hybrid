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
    private const string ReconnectRejectedMessage = "начато переподключение Modbus до начала выполнения";
    private const string PendingStateMarker = "State=pending";

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
        var deadline = DateTime.UtcNow + readyWaitTimeout;

        while (true)
        {
            var readinessError = await WaitForDispatcherReadyAsync(
                context,
                dispatcher,
                logger,
                GetRemainingTimeout(deadline),
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

            var writeResult = await writeAsync(ct);
            if (!IsReconnectRaceRejected(writeResult))
            {
                return writeResult;
            }

            if (DateTime.UtcNow >= deadline)
            {
                logger.LogWarning(
                    "Повторное ожидание готовности Modbus после reconnect-race не выполнено: общий дедлайн {TimeoutMs} мс исчерпан",
                    readyWaitTimeout.TotalMilliseconds);
                return writeResult;
            }

            logger.LogWarning(
                "Поймана reconnect-race при записи режима Стенд. Повторно ждём готовность Modbus и выполняем ещё одну попытку");
        }
    }

    private static async Task<string?> WaitForDispatcherReadyAsync(
        TestStepContext context,
        IModbusDispatcher dispatcher,
        IDualLogger logger,
        TimeSpan remainingTimeout,
        TimeSpan totalTimeout,
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

        if (remainingTimeout <= TimeSpan.Zero)
        {
            LogReadyTimeout(logger, totalTimeout);
            return BuildReadyTimeoutMessage(totalTimeout);
        }

        logger.LogInformation("Ожидание готовности Modbus после reconnect перед записью режима Стенд");

        var waited = TimeSpan.Zero;
        while (waited < remainingTimeout)
        {
            ct.ThrowIfCancellationRequested();

            var delay = GetDelayChunk(remainingTimeout, pollInterval, waited);
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

        LogReadyTimeout(logger, totalTimeout);
        return BuildReadyTimeoutMessage(totalTimeout);
    }

    private static TimeSpan GetDelayChunk(
        TimeSpan remainingTimeout,
        TimeSpan pollInterval,
        TimeSpan waited)
    {
        var remaining = remainingTimeout - waited;
        return remaining <= pollInterval ? remaining : pollInterval;
    }

    private static TimeSpan GetRemainingTimeout(DateTime deadline)
    {
        var remaining = deadline - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static void LogReadyTimeout(IDualLogger logger, TimeSpan totalTimeout)
    {
        logger.LogWarning(
            "Готовность Modbus не восстановлена за {TimeoutMs} мс перед записью режима Стенд",
            totalTimeout.TotalMilliseconds);
    }

    private static string BuildReadyTimeoutMessage(TimeSpan totalTimeout)
    {
        return $"Готовность Modbus для записи режима Стенд не восстановлена за {totalTimeout.TotalSeconds:F0} с.";
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

    private static bool IsReconnectRaceRejected(DiagnosticWriteResult result)
    {
        if (result is not { Success: false, FailureKind: DiagnosticFailureKind.Communication, Error: not null })
        {
            return false;
        }

        return result.Error.Contains(ReconnectRejectedMessage, StringComparison.OrdinalIgnoreCase)
               || (result.Error.Contains(PendingStateMarker, StringComparison.OrdinalIgnoreCase)
                   && result.Error.Contains("переподключ", StringComparison.OrdinalIgnoreCase));
    }
}
