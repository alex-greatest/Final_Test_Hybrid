using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Settings.OpcUa;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Main.PlcReset;

/// <summary>
/// Координатор сброса теста по сигналу PLC.
/// Обрабатывает Req_Reset, отправляет данные, ждёт Ask_End.
/// </summary>
public sealed partial class PlcResetCoordinator : IAsyncDisposable
{
    private static readonly TimeSpan AskEndWaitSlice = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AskEndSyncTimeout = TimeSpan.FromSeconds(1);

    private readonly ResetSubscription _resetSubscription;
    private readonly IErrorCoordinator _errorCoordinator;
    private readonly TagWaiter _tagWaiter;
    private readonly OpcUaTagService _plcService;
    private readonly OpcUaConnectionState _connectionState;
    private readonly TimeSpan _askEndTimeout;
    private readonly TimeSpan _reconnectWaitTimeout;
    private readonly TimeSpan _resetHardTimeout;
    private readonly ILogger<PlcResetCoordinator> _logger;
    private readonly CancellationTokenSource _disposeCts = new();
    private CancellationTokenSource? _currentResetCts;
    private int _isHandlingReset;
    private volatile bool _disposed;
    private bool _currentResetWasInScanPhase;
    private DateTime _currentResetHardDeadlineUtc;

    /// <summary>
    /// Одноразовый маркер: 1 = PLC Reset в процессе (между началом и Reset()/ForceStop()).
    /// Используется для определения источника HardReset.
    /// </summary>
    public int PlcHardResetPending;

    public bool IsActive { get; private set; }
    public event Action? OnActiveChanged;
    public event Action? OnForceStop;
    public event Func<bool>? OnResetStarting;
    public event Action? OnAskEndReceived;
    public event Action? OnResetCompleted;

    public PlcResetCoordinator(
        ResetSubscription resetSubscription,
        IErrorCoordinator errorCoordinator,
        TagWaiter tagWaiter,
        OpcUaTagService plcService,
        OpcUaConnectionState connectionState,
        IOptions<OpcUaSettings> opcUaSettings,
        ILogger<PlcResetCoordinator> logger)
    {
        _resetSubscription = resetSubscription;
        _errorCoordinator = errorCoordinator;
        _tagWaiter = tagWaiter;
        _plcService = plcService;
        _connectionState = connectionState;
        var timeoutSettings = opcUaSettings.Value.ResetFlowTimeouts;
        _askEndTimeout = TimeSpan.FromSeconds(timeoutSettings.AskEndTimeoutSec);
        _reconnectWaitTimeout = TimeSpan.FromSeconds(timeoutSettings.ReconnectWaitTimeoutSec);
        _resetHardTimeout = TimeSpan.FromSeconds(timeoutSettings.ResetHardTimeoutSec);
        _logger = logger;
        _resetSubscription.OnStateChanged += HandleResetSignal;
    }

    #region Signal Handling

    private void HandleResetSignal()
    {
        if (_disposed)
        {
            return;
        }
        _ = HandleResetAsync().ContinueWith(
            t => _logger.LogError(t.Exception, "Ошибка обработки PLC reset"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task HandleResetAsync()
    {
        if (!TryAcquireResetFlag())
        {
            _logger.LogWarning("PLC Reset проигнорирован — уже обрабатывается");
            return;
        }
        IsActive = true;
        NotifyActiveChangedSafely();
        _currentResetCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
        try
        {
            await ExecuteResetStepsAsync(_currentResetCts.Token);
        }
        catch (Exception ex)
        {
            await HandleResetExceptionAsync(ex);
        }
        finally
        {
            Cleanup();
        }
    }

    private async Task ExecuteResetStepsAsync(CancellationToken ct)
    {
        _logger.LogWarning("╔═══ СБРОС ПО СИГНАЛУ PLC ═══");
        StartResetHardTimeoutWindow();
        LogConfiguredTimeouts();
        _currentResetWasInScanPhase = InvokeEventSafeWithResult(OnResetStarting) ?? false;
        _logger.LogInformation("Состояние до сброса: InScanPhase: {InScanPhase}", _currentResetWasInScanPhase);

        SignalForceStop();
        await SendDataToMesAsync(ct);
        await SendResetAndWaitAckAsync(ct);

        ExecuteSmartReset(_currentResetWasInScanPhase);
        _logger.LogInformation("PLC Reset завершён успешно");
    }

    private async Task HandleResetExceptionAsync(Exception ex)
    {
        switch (ex)
        {
            case OperationCanceledException when _disposed:
                _logger.LogInformation("PLC Reset отменён — disposal");
                break;
            case OperationCanceledException:
                _logger.LogInformation("PLC Reset отменён до подтверждения Ask_End");
                InvokeEventSafe(OnResetCompleted);
                break;
            case TimeoutException:
                await HandleAskEndTimeoutAsync();
                break;
            case PlcConnectionLostDuringResetException:
                await HandleConnectionLostDuringResetAsync(ex);
                break;
            case Exception transientEx when IsTransientOpcDisconnect(transientEx):
                await HandleConnectionLostDuringResetAsync(transientEx);
                break;
            default:
                _logger.LogError(ex, "Неожиданная ошибка PLC Reset — полный сброс");
                Volatile.Write(ref PlcHardResetPending, 1);
                try
                {
                    _errorCoordinator.Reset();
                }
                finally
                {
                    Volatile.Write(ref PlcHardResetPending, 0);
                }
                InvokeEventSafe(OnResetCompleted);
                break;
        }
    }

    private async Task HandleConnectionLostDuringResetAsync(Exception ex)
    {
        _logger.LogWarning(ex, "Потеря связи с PLC во время reset-flow ожидания Ask_End — fail-fast");
        await _errorCoordinator.HandleInterruptAsync(InterruptReason.PlcConnectionLost);
        InvokeEventSafe(OnResetCompleted);
    }

    private async Task HandleAskEndTimeoutAsync()
    {
        _logger.LogWarning(
            "Таймаут reset-flow: AskEnd={AskEndSec} сек, ReconnectWait={ReconnectWaitSec} сек, Hard={HardSec} сек",
            _askEndTimeout.TotalSeconds,
            _reconnectWaitTimeout.TotalSeconds,
            _resetHardTimeout.TotalSeconds);
        await _errorCoordinator.HandleInterruptAsync(InterruptReason.TagTimeout);
        InvokeEventSafe(OnResetCompleted);
    }

    #endregion

    #region Reset Steps

    private void SignalForceStop()
    {
        InvokeEventSafe(OnForceStop);
    }

    private async Task SendDataToMesAsync(CancellationToken ct)
    {
        _logger.LogInformation("MES/DB отправка (заглушка)");
        await Task.Delay(100, ct);
    }

    private async Task SendResetAndWaitAckAsync(CancellationToken ct)
    {
        await TrySendResetSignalAsync(ct);
        await WaitForAskEndFailFastAsync(ct);
        InvokeEventSafe(OnAskEndReceived);
    }

    private async Task TrySendResetSignalAsync(CancellationToken ct)
    {
        var writeResult = await _plcService.WriteAsync(BaseTags.Reset, true, ct);
        if (!writeResult.Success)
        {
            throw new InvalidOperationException($"Не удалось записать Reset: {writeResult.Error}");
        }
    }

    private async Task WaitForAskEndFailFastAsync(CancellationToken ct)
    {
        await SynchronizeStaleAskEndStateAsync(ct);
        var askEndDeadlineUtc = DateTime.UtcNow + _askEndTimeout;
        while (true)
        {
            var remaining = GetRemainingAskEndWindow(askEndDeadlineUtc);
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException("Таймаут ожидания Ask_End");
            }
            var slice = remaining < AskEndWaitSlice ? remaining : AskEndWaitSlice;
            if (await TryWaitAskEndSliceAsync(slice, ct))
            {
                return;
            }
        }
    }

    private void StartResetHardTimeoutWindow()
    {
        _currentResetHardDeadlineUtc = DateTime.UtcNow + _resetHardTimeout;
    }

    private TimeSpan GetRemainingResetHardTimeout()
    {
        var remaining = _currentResetHardDeadlineUtc - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private TimeSpan GetRemainingAskEndWindow(DateTime askEndDeadlineUtc)
    {
        var askEndRemaining = askEndDeadlineUtc - DateTime.UtcNow;
        if (askEndRemaining <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var hardRemaining = GetRemainingResetHardTimeout();
        if (hardRemaining <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return askEndRemaining < hardRemaining ? askEndRemaining : hardRemaining;
    }

    private void LogConfiguredTimeouts()
    {
        _logger.LogInformation(
            "Таймауты reset-flow: AskEnd={AskEndSec} сек, ReconnectWait={ReconnectWaitSec} сек, Hard={HardSec} сек",
            _askEndTimeout.TotalSeconds,
            _reconnectWaitTimeout.TotalSeconds,
            _resetHardTimeout.TotalSeconds);
    }

    private async Task<bool> TryWaitAskEndSliceAsync(TimeSpan timeout, CancellationToken ct)
    {
        if (!_connectionState.IsConnected)
        {
            throw new PlcConnectionLostDuringResetException("Потеря связи перед ожиданием Ask_End");
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(timeout);
        var connectionLost = 0;

        void OnConnectionStateChanged(bool isConnected)
        {
            if (isConnected)
            {
                return;
            }
            Volatile.Write(ref connectionLost, 1);
            linkedCts.Cancel();
        }

        _connectionState.ConnectionStateChanged += OnConnectionStateChanged;
        try
        {
            await _tagWaiter.WaitAnyAsync(
                _tagWaiter.CreateWaitGroup<bool>()
                    .WaitForTrue(BaseTags.AskEnd, () => true, "AskEnd"),
                linkedCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && Volatile.Read(ref connectionLost) == 1)
        {
            throw new PlcConnectionLostDuringResetException("Потеря связи во время ожидания Ask_End");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && linkedCts.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex) when (IsTransientOpcDisconnect(ex))
        {
            throw new PlcConnectionLostDuringResetException("Потеря связи во время ожидания Ask_End", ex);
        }
        finally
        {
            _connectionState.ConnectionStateChanged -= OnConnectionStateChanged;
        }
    }

    private async Task SynchronizeStaleAskEndStateAsync(CancellationToken ct)
    {
        var askEndRead = await _plcService.ReadAsync<bool>(BaseTags.AskEnd, ct);
        if (!askEndRead.Success || askEndRead.Value != true)
        {
            return;
        }
        _logger.LogDebug("Ask_End уже true до reset. Проверяем сброс stale-состояния");
        try
        {
            await _tagWaiter.WaitForFalseAsync(BaseTags.AskEnd, timeout: AskEndSyncTimeout, ct);
            _logger.LogDebug("Stale Ask_End сброшен, ждём новое подтверждение");
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("Stale Ask_End не сброшен быстро, продолжаем обычное ожидание");
        }
    }

    private void ExecuteSmartReset(bool wasInScanPhase)
    {
        if (wasInScanPhase)
        {
            _logger.LogInformation("Мягкий сброс (был в scan phase) — сохраняем Grid и BoilerState");
            _errorCoordinator.ForceStop();
        }
        else
        {
            _logger.LogInformation("Полный сброс (тест выполнялся) — очищаем BoilerState");
            Volatile.Write(ref PlcHardResetPending, 1);
            try
            {
                _errorCoordinator.Reset();
            }
            finally
            {
                Volatile.Write(ref PlcHardResetPending, 0);
            }
        }
        InvokeEventSafe(OnResetCompleted);
    }

    private sealed class PlcConnectionLostDuringResetException(string message, Exception? innerException = null)
        : Exception(message, innerException);

    private static bool IsTransientOpcDisconnect(Exception ex)
    {
        return OpcUaTransientErrorClassifier.IsTransientDisconnect(ex);
    }

    private void InvokeEventSafe(Action? handler)
    {
        try
        {
            handler?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике события");
        }
    }

    private bool? InvokeEventSafeWithResult(Func<bool>? handler)
    {
        try
        {
            return handler?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике события");
            return null;
        }
    }

    #endregion

}
