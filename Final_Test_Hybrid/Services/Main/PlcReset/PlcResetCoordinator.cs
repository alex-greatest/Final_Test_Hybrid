using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;

namespace Final_Test_Hybrid.Services.Main.PlcReset;

using Models.Plc.Tags;
using OpcUa;
using Microsoft.Extensions.Logging;

/// <summary>
/// Координатор сброса теста по сигналу PLC.
/// Обрабатывает Req_Reset, отправляет данные, ждёт Ask_End.
/// </summary>
public sealed class PlcResetCoordinator : IAsyncDisposable
{
    private readonly ResetSubscription _resetSubscription;
    private readonly ErrorCoordinator _errorCoordinator;
    private readonly TagWaiter _tagWaiter;
    private readonly OpcUaTagService _plcService;
    private readonly ILogger<PlcResetCoordinator> _logger;
    private readonly CancellationTokenSource _disposeCts = new();
    private CancellationTokenSource? _currentResetCts;
    private int _isHandlingReset;
    private volatile bool _disposed;
    private static readonly TimeSpan AskEndTimeout = TimeSpan.FromSeconds(60);
    public bool IsActive { get; private set; }
    public event Action? OnActiveChanged;
    public event Action? OnForceStop;
    public event Func<bool>? OnResetStarting;
    public event Action? OnResetCompleted;

    public PlcResetCoordinator(
        ResetSubscription resetSubscription,
        ErrorCoordinator errorCoordinator,
        TagWaiter tagWaiter,
        OpcUaTagService plcService,
        ILogger<PlcResetCoordinator> logger)
    {
        _resetSubscription = resetSubscription;
        _errorCoordinator = errorCoordinator;
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
        IsActive = true;
        OnActiveChanged?.Invoke();
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

        var wasInScanPhase = InvokeEventSafeWithResult(OnResetStarting) ?? false;
        _logger.LogInformation("Состояние до сброса: InScanPhase: {InScanPhase}", wasInScanPhase);

        SignalForceStop();
        await SendDataToMesAsync(ct);
        await SendResetAndWaitAckAsync(ct);

        ExecuteSmartReset(wasInScanPhase);
        _logger.LogInformation("PLC Reset завершён успешно");
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

        InvokeEventSafe(OnResetCompleted);
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
                InvokeEventSafe(OnResetCompleted);
                break;

            case TimeoutException:
                _logger.LogWarning("Таймаут Ask_End ({Timeout} сек)", AskEndTimeout.TotalSeconds);
                await _errorCoordinator.HandleInterruptAsync(InterruptReason.TagTimeout);
                InvokeEventSafe(OnResetCompleted);
                break;
            default:
                _logger.LogError(ex, "Неожиданная ошибка PLC Reset — полный сброс");
                _errorCoordinator.Reset();
                InvokeEventSafe(OnResetCompleted);
                break;
        }
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
        await Task.Delay(100, ct);  // TODO: реальная отправка
    }

    private async Task SendResetAndWaitAckAsync(CancellationToken ct)
    {
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

    private bool? InvokeEventSafeWithResult(Func<bool>? handler)
    {
        try { return handler?.Invoke(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике события");
            return null;
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
        IsActive = false;
        OnActiveChanged?.Invoke();
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
        OnResetStarting = null;
        OnResetCompleted = null;
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
