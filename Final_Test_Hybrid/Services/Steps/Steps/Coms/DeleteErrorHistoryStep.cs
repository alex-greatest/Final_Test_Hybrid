using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг очистки журнала ошибок котла.
/// Записывает значение 0 в регистр 1154 (очистка журнала ошибок).
/// </summary>
public class DeleteErrorHistoryStep : ITestStep
{
    private const ushort RegisterErrorHistoryClear = 1154;
    private const ushort ClearValue = 0;
    private static readonly TimeSpan ReadyWaitTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private const string ReconnectRejectedMessage = "начато переподключение Modbus до начала выполнения";
    private const string PendingStateMarker = "State=pending";
    private const string RejectedStateMarker = "State=rejected";

    private readonly DiagnosticSettings _settings;
    private readonly IModbusDispatcher _dispatcher;
    private readonly DualLogger<DeleteErrorHistoryStep> _logger;
    private readonly TimeSpan _readyWaitTimeout;
    private readonly TimeSpan _pollInterval;

    public DeleteErrorHistoryStep(
        IOptions<DiagnosticSettings> settings,
        IModbusDispatcher dispatcher,
        DualLogger<DeleteErrorHistoryStep> logger)
        : this(settings.Value, dispatcher, logger, ReadyWaitTimeout, PollInterval)
    {
    }

    internal DeleteErrorHistoryStep(
        DiagnosticSettings settings,
        IModbusDispatcher dispatcher,
        DualLogger<DeleteErrorHistoryStep> logger,
        TimeSpan readyWaitTimeout,
        TimeSpan pollInterval)
    {
        _settings = settings;
        _dispatcher = dispatcher;
        _logger = logger;
        _readyWaitTimeout = readyWaitTimeout;
        _pollInterval = pollInterval;
    }

    public string Id => "coms-delete-error-history";
    public string Name => "Coms/Delete_Error_History";
    public string Description => "Сброс ошибок котла";

    /// <summary>
    /// Записывает значение 0 в регистр очистки журнала ошибок (1154).
    /// </summary>
    /// <param name="context">Контекст выполнения тестового шага.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        _logger.LogInformation("Запись значения {Value} в регистр {Register} (очистка журнала ошибок)",
            ClearValue, RegisterErrorHistoryClear);

        var modbusAddress = (ushort)(RegisterErrorHistoryClear - _settings.BaseAddressOffset);
        var result = await WriteWithReconnectRetryAsync(context, modbusAddress, ct);

        if (!result.Success)
        {
            var msg = BuildFailureMessage(result, modbusAddress);
            _logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        _logger.LogInformation("Журнал ошибок очищен");
        return TestStepResult.Pass();
    }

    private async Task<DiagnosticWriteResult> WriteWithReconnectRetryAsync(
        TestStepContext context,
        ushort modbusAddress,
        CancellationToken ct)
    {
        var initialResult = await context.PacedDiagWriter.WriteUInt16Async(modbusAddress, ClearValue, ct);
        if (!IsReconnectRaceRejected(initialResult))
        {
            return initialResult;
        }

        _logger.LogWarning(
            "Поймана reconnect-race при очистке журнала ошибок. Ждём готовность Modbus и повторяем запись");

        var readinessError = await WaitForDispatcherReadyAsync(context, ct);
        if (readinessError != null)
        {
            return DiagnosticWriteResult.Fail(
                modbusAddress,
                readinessError,
                DiagnosticFailureKind.Communication);
        }

        _logger.LogInformation("Готовность Modbus восстановлена, повторяем очистку журнала ошибок");
        return await context.PacedDiagWriter.WriteUInt16Async(modbusAddress, ClearValue, ct);
    }

    private async Task<string?> WaitForDispatcherReadyAsync(TestStepContext context, CancellationToken ct)
    {
        var stoppedMessage = GetStoppedDispatcherMessage("повторной записью очистки журнала ошибок");
        if (stoppedMessage != null)
        {
            return stoppedMessage;
        }

        if (IsDispatcherReady())
        {
            return null;
        }

        _logger.LogInformation("Ожидание готовности Modbus после reconnect перед повторной очисткой журнала ошибок");

        var deadline = DateTime.UtcNow + _readyWaitTimeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var delay = GetDelayChunk(deadline);
            await context.DelayAsync(delay, ct);

            stoppedMessage = GetStoppedDispatcherMessage("ожидания повторной очистки журнала ошибок");
            if (stoppedMessage != null)
            {
                return stoppedMessage;
            }

            if (!IsDispatcherReady())
            {
                continue;
            }

            return null;
        }

        _logger.LogWarning(
            "Готовность Modbus не восстановлена за {TimeoutMs} мс перед повторной очисткой журнала ошибок",
            _readyWaitTimeout.TotalMilliseconds);

        return $"Готовность Modbus для очистки журнала ошибок не восстановлена за {_readyWaitTimeout.TotalSeconds:F0} с.";
    }

    private TimeSpan GetDelayChunk(DateTime deadline)
    {
        var remaining = deadline - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return remaining <= _pollInterval ? remaining : _pollInterval;
    }

    private static string BuildFailureMessage(DiagnosticWriteResult result, ushort modbusAddress)
    {
        var operation =
            $"записи команды очистки журнала ошибок в регистр {RegisterErrorHistoryClear} (Modbus {modbusAddress})";
        var functionalMessage =
            $"Ошибка записи в регистр {RegisterErrorHistoryClear} (Modbus {modbusAddress}). {result.Error}";
        return ComsStepFailureHelper.BuildWriteMessage(result, operation, functionalMessage);
    }

    private string? GetStoppedDispatcherMessage(string phase)
    {
        return _dispatcher.IsStarted
            ? null
            : $"ModbusDispatcher остановлен перед {phase}.";
    }

    private bool IsDispatcherReady()
    {
        return _dispatcher is { IsStarted: true, IsConnected: true, IsReconnecting: false, LastPingData: not null };
    }

    private static bool IsReconnectRaceRejected(DiagnosticWriteResult result)
    {
        if (result is not { Success: false, FailureKind: DiagnosticFailureKind.Communication, Error: not null })
        {
            return false;
        }

        return result.Error.Contains(ReconnectRejectedMessage, StringComparison.OrdinalIgnoreCase)
               || ((result.Error.Contains(PendingStateMarker, StringComparison.OrdinalIgnoreCase)
                    || result.Error.Contains(RejectedStateMarker, StringComparison.OrdinalIgnoreCase))
                   && result.Error.Contains("переподключ", StringComparison.OrdinalIgnoreCase));
    }
}
