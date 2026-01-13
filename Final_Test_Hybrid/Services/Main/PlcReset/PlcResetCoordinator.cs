namespace Final_Test_Hybrid.Services.Main.PlcReset;

using Models.Plc.Tags;
using OpcUa;
using Steps.Infrastructure.Execution;
using Steps.Infrastructure.Execution.Scanning;
using Microsoft.Extensions.Logging;

/// <summary>
/// Координатор сброса теста по сигналу PLC.
/// Обрабатывает Req_Reset, отправляет данные, ждёт Ask_End.
/// </summary>
public sealed class PlcResetCoordinator : IAsyncDisposable
{
    private readonly ResetSubscription _resetSubscription;
    private readonly ResetMessageState _resetMessage;
    private readonly ErrorCoordinator _errorCoordinator;
    private readonly ScanStateManager _scanStateManager;
    private readonly ScanModeController _scanModeController;
    private readonly TagWaiter _tagWaiter;
    private readonly OpcUaTagService _plcService;
    private readonly ILogger<PlcResetCoordinator> _logger;
    private readonly CancellationTokenSource _disposeCts = new();
    private CancellationTokenSource? _currentResetCts;
    private int _isHandlingReset;
    private volatile bool _disposed;

    private static readonly TimeSpan AskEndTimeout = TimeSpan.FromSeconds(60);

    public event Action? OnForceStop;

    public PlcResetCoordinator(
        ResetSubscription resetSubscription,
        ResetMessageState resetMessage,
        ErrorCoordinator errorCoordinator,
        ScanStateManager scanStateManager,
        ScanModeController scanModeController,
        TagWaiter tagWaiter,
        OpcUaTagService plcService,
        ILogger<PlcResetCoordinator> logger)
    {
        _resetSubscription = resetSubscription;
        _resetMessage = resetMessage;
        _errorCoordinator = errorCoordinator;
        _scanStateManager = scanStateManager;
        _scanModeController = scanModeController;
        _tagWaiter = tagWaiter;
        _plcService = plcService;
        _logger = logger;

        _resetSubscription.OnStateChanged += HandleResetSignal;
    }

    #region Signal Handling

    private void HandleResetSignal()
    {
        if (_disposed) { return; }

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
        _logger.LogWarning("═══ СБРОС ПО СИГНАЛУ PLC ═══");

        var wasInScanPhase = IsInScanningPhase();
        _logger.LogInformation("Состояние до сброса: {State}, InScanPhase: {InScanPhase}",
            _scanStateManager.State, wasInScanPhase);

        _scanModeController.EnterResettingMode();

        SignalForceStop();
        await SendDataToMesAsync(ct);
        await SendResetAndWaitAckAsync(ct);

        ExecuteSmartReset(wasInScanPhase);
        _logger.LogInformation("PLC Reset завершён успешно");
    }

    private bool IsInScanningPhase()
    {
        return _scanStateManager.State is ScanState.Ready or ScanState.Error;
    }

    private void ExecuteSmartReset(bool wasInScanPhase)
    {
        if (wasInScanPhase)
        {
            _logger.LogInformation("Мягкий сброс (был в шаге сканирования) — сохраняем Grid и BoilerState");
            _errorCoordinator.ForceStop();
        }
        else
        {
            _logger.LogInformation("Полный сброс (тест выполнялся) — очищаем BoilerState");
            _errorCoordinator.Reset();
        }

        _scanModeController.TransitionToReady();
    }

    private async Task HandleResetExceptionAsync(Exception ex)
    {
        switch (ex)
        {
            case OperationCanceledException when _disposed:
                _logger.LogInformation("PLC Reset отменён — disposal");
                break;
            case OperationCanceledException:
                _logger.LogInformation("PLC Reset отменён");
                _scanModeController.TransitionToReady();
                break;

            case TimeoutException:
                _logger.LogWarning("Таймаут Ask_End ({Timeout} сек)", AskEndTimeout.TotalSeconds);
                await _errorCoordinator.HandleInterruptAsync(InterruptReason.TagTimeout);
                _scanModeController.TransitionToReady();
                break;
            default:
                _logger.LogError(ex, "Неожиданная ошибка PLC Reset — полный сброс");
                _errorCoordinator.Reset();
                _scanModeController.TransitionToReady();
                break;
        }
    }

    #endregion

    #region Reset Steps

    private void SignalForceStop()
    {
        _resetMessage.SetMessage("Идёт сброс теста...");
        InvokeEventSafe(OnForceStop);
    }

    private async Task SendDataToMesAsync(CancellationToken ct)
    {
        _resetMessage.SetMessage("Передача данных...");
        _logger.LogInformation("MES/DB отправка (заглушка)");
        await Task.Delay(100, ct);  // TODO: реальная отправка
    }

    private async Task SendResetAndWaitAckAsync(CancellationToken ct)
    {
        _resetMessage.SetMessage("Сброс теста...");
        await TrySendResetSignalAsync(ct);
        await WaitForAskEndAsync(ct);
    }

    private async Task TrySendResetSignalAsync(CancellationToken ct)
    {
        try
        {
            await _plcService.WriteAsync(BaseTags.Reset, true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка записи Reset в PLC — продолжаем");
        }
    }

    private async Task WaitForAskEndAsync(CancellationToken ct)
    {
        await _tagWaiter.WaitAnyAsync(
            _tagWaiter.CreateWaitGroup<bool>()
                .WaitForTrue(BaseTags.AskEnd, () => true, "AskEnd")
                .WithTimeout(AskEndTimeout),
            ct);
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

    #endregion

    #region Synchronization

    private bool TryAcquireResetFlag()
        => Interlocked.CompareExchange(ref _isHandlingReset, 1, 0) == 0;

    private void ReleaseResetFlag()
        => Interlocked.Exchange(ref _isHandlingReset, 0);

    private void Cleanup()
    {
        _resetMessage.Clear();
        var cts = Interlocked.Exchange(ref _currentResetCts, null);
        cts?.Dispose();
        ReleaseResetFlag();
    }

    #endregion

    #region Public API

    public void CancelCurrentReset()
    {
        var cts = _currentResetCts;
        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Expected if disposed during race
        }
    }

    #endregion

    #region Disposal

    public async ValueTask DisposeAsync()
    {
        if (_disposed) { return; }
        _disposed = true;
        CancelDisposeCts();
        UnsubscribeEvents();
        await WaitForCurrentOperationAsync();
        DisposeResources();
    }

    private void CancelDisposeCts()
    {
        try { _disposeCts.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private void UnsubscribeEvents()
    {
        _resetSubscription.OnStateChanged -= HandleResetSignal;
        OnForceStop = null;
    }

    private async Task WaitForCurrentOperationAsync()
    {
        var spinWait = new SpinWait();
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (_isHandlingReset == 1 && DateTime.UtcNow < timeout)
        {
            spinWait.SpinOnce();
            await Task.Yield();
        }
    }

    private void DisposeResources()
    {
        _currentResetCts?.Dispose();
        _disposeCts.Dispose();
    }

    #endregion
}
