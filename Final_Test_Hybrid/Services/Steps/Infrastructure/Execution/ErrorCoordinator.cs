using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

/// <summary>
/// Unified error and interrupt coordinator for test execution.
/// Handles PLC connection loss, auto mode, timeouts, and error resolution.
/// Thread-safe with proper disposal patterns.
/// </summary>
public class ErrorCoordinator : IDisposable
{
    private readonly OpcUaConnectionState _connectionState;
    private readonly AutoReadySubscription _autoReady;
    private readonly PauseTokenSource _pauseToken;
    private readonly PausableTagWaiter _tagWaiter;
    private readonly PausableOpcUaTagService _plcService;
    private readonly ExecutionStateManager _stateManager;
    private readonly StepStatusReporter _statusReporter;
    private readonly BoilerState _boilerState;
    private readonly ExecutionActivityTracker _activityTracker;
    private readonly InterruptMessageState _interruptMessage;
    private readonly INotificationService _notifications;
    private readonly ILogger<ErrorCoordinator> _logger;

    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private int _isHandlingInterrupt;
    private volatile bool _disposed;

    private static readonly TimeSpan ResolutionTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PlcReconnectDelay = TimeSpan.FromSeconds(5);

    private static readonly Dictionary<InterruptReason, InterruptBehavior> InterruptBehaviors = new()
    {
        [InterruptReason.PlcConnectionLost] = new InterruptBehavior(
            Message: "Потеря связи с PLC",
            Action: InterruptAction.ResetAfterDelay,
            Delay: PlcReconnectDelay),

        [InterruptReason.AutoModeDisabled] = new InterruptBehavior(
            Message: "Нет автомата",
            Action: InterruptAction.PauseAndWait),

        [InterruptReason.TagTimeout] = new InterruptBehavior(
            Message: "Нет ответа",
            Action: InterruptAction.ResetAfterDelay)
    };

    public event Action? OnReset;
    public event Action? OnRecovered;

    public ErrorCoordinator(
        OpcUaConnectionState connectionState,
        AutoReadySubscription autoReady,
        PauseTokenSource pauseToken,
        PausableTagWaiter tagWaiter,
        PausableOpcUaTagService plcService,
        ExecutionStateManager stateManager,
        StepStatusReporter statusReporter,
        BoilerState boilerState,
        ExecutionActivityTracker activityTracker,
        InterruptMessageState interruptMessage,
        INotificationService notifications,
        ILogger<ErrorCoordinator> logger)
    {
        _connectionState = connectionState;
        _autoReady = autoReady;
        _pauseToken = pauseToken;
        _tagWaiter = tagWaiter;
        _plcService = plcService;
        _stateManager = stateManager;
        _statusReporter = statusReporter;
        _boilerState = boilerState;
        _activityTracker = activityTracker;
        _interruptMessage = interruptMessage;
        _notifications = notifications;
        _logger = logger;

        SubscribeToEvents();
    }

    #region Event Subscriptions

    private void SubscribeToEvents()
    {
        _connectionState.ConnectionStateChanged += HandleConnectionChanged;
        _autoReady.OnStateChanged += HandleAutoReadyChanged;
    }

    private void HandleConnectionChanged(bool isConnected)
    {
        if (_disposed || isConnected || !_activityTracker.IsAnyActive)
        {
            return;
        }

        FireAndForgetInterrupt(InterruptReason.PlcConnectionLost);
    }

    private void HandleAutoReadyChanged()
    {
        if (_disposed)
        {
            return;
        }

        if (_autoReady.IsReady)
        {
            FireAndForgetResume();
            return;
        }

        if (_activityTracker.IsAnyActive)
        {
            FireAndForgetInterrupt(InterruptReason.AutoModeDisabled);
        }
    }

    private void FireAndForgetInterrupt(InterruptReason reason)
    {
        _ = HandleInterruptAsync(reason, _disposeCts.Token).ContinueWith(
            t => _logger.LogError(t.Exception, "Ошибка обработки {Reason}", reason),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private void FireAndForgetResume()
    {
        _ = TryResumeFromPauseAsync(_disposeCts.Token).ContinueWith(
            t => _logger.LogError(t.Exception, "Ошибка восстановления"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    #endregion

    #region Synchronization Primitives

    private bool TryAcquireInterruptFlag(InterruptReason reason)
    {
        var acquired = Interlocked.CompareExchange(ref _isHandlingInterrupt, 1, 0) == 0;

        if (!acquired)
        {
            _logger.LogWarning("Прерывание {Reason} проигнорировано — уже обрабатывается другое", reason);
        }

        return acquired;
    }

    private void ReleaseInterruptFlag()
    {
        Interlocked.Exchange(ref _isHandlingInterrupt, 0);
    }

    private async Task<bool> TryAcquireLockAsync(CancellationToken ct)
    {
        try
        {
            await _operationLock.WaitAsync(ct);
            return !_disposed;
        }
        catch (Exception) when (ct.IsCancellationRequested || _disposed)
        {
            return false;
        }
    }

    private void ReleaseLockSafe()
    {
        try
        {
            if (!_disposed)
            {
                _operationLock.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            // Expected during shutdown - semaphore already disposed
        }
    }

    #endregion

    #region Interrupt Handling

    public async Task HandleInterruptAsync(InterruptReason reason, CancellationToken ct = default)
    {
        if (_disposed || !TryAcquireInterruptFlag(reason))
        {
            return;
        }

        if (!await TryAcquireLockAsync(ct))
        {
            ReleaseInterruptFlag();
            return;
        }

        try
        {
            await ProcessInterruptAsync(reason, ct);
        }
        finally
        {
            ReleaseLockSafe();
            ReleaseInterruptFlag();
        }
    }

    private async Task ProcessInterruptAsync(InterruptReason reason, CancellationToken ct)
    {
        if (!InterruptBehaviors.TryGetValue(reason, out var behavior))
        {
            _logger.LogError("Неизвестная причина прерывания: {Reason}", reason);
            return;
        }

        LogInterrupt(reason, behavior);
        NotifyInterrupt(behavior);

        await ExecuteInterruptActionAsync(behavior, ct);
    }

    private void LogInterrupt(InterruptReason reason, InterruptBehavior behavior)
    {
        _logger.LogWarning("Прерывание: {Reason} — {Message}", reason, behavior.Message);
    }

    private void NotifyInterrupt(InterruptBehavior behavior)
    {
        _interruptMessage.SetMessage(behavior.Message);
        _notifications.ShowWarning(behavior.Message, GetInterruptDetails(behavior));
    }

    private static string GetInterruptDetails(InterruptBehavior behavior)
    {
        return behavior.Action switch
        {
            InterruptAction.PauseAndWait => "Ожидание восстановления...",
            InterruptAction.ResetAfterDelay when behavior.Delay.HasValue =>
                $"Сброс через {behavior.Delay.Value.TotalSeconds:0} сек",
            InterruptAction.ResetAfterDelay => "Сброс теста",
            _ => string.Empty
        };
    }

    private async Task ExecuteInterruptActionAsync(InterruptBehavior behavior, CancellationToken ct)
    {
        switch (behavior.Action)
        {
            case InterruptAction.PauseAndWait:
                _pauseToken.Pause();
                break;

            case InterruptAction.ResetAfterDelay:
                await DelayThenResetAsync(behavior.Delay, ct);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(behavior), behavior.Action, "Неизвестное действие");
        }
    }

    private async Task DelayThenResetAsync(TimeSpan? delay, CancellationToken ct)
    {
        if (delay.HasValue)
        {
            await Task.Delay(delay.Value, ct);
        }
        Reset();
    }

    #endregion

    #region Error Resolution

    public async Task<ErrorResolution> WaitForResolutionAsync(CancellationToken ct)
    {
        _logger.LogInformation("Ожидание решения оператора (таймаут {Timeout} сек)...",
            ResolutionTimeout.TotalSeconds);
        try
        {
            return await WaitForOperatorSignalAsync(ct);
        }
        catch (TimeoutException)
        {
            return HandleResolutionTimeout();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Ожидание решения отменено");
            throw;
        }
    }

    private async Task<ErrorResolution> WaitForOperatorSignalAsync(CancellationToken ct)
    {
        var waitResult = await _tagWaiter.WaitAnyAsync(
            _tagWaiter.CreateWaitGroup<ErrorResolution>()
                .WaitForTrue(BaseTags.ErrorRetry, () => ErrorResolution.Retry, "Retry")
                .WaitForTrue(BaseTags.ErrorSkip, () => ErrorResolution.Skip, "Skip")
                .WithTimeout(ResolutionTimeout),
            ct);

        var resolution = waitResult.Result;
        _logger.LogInformation("Получен сигнал: {Resolution}", resolution);
        return resolution;
    }

    private ErrorResolution HandleResolutionTimeout()
    {
        _logger.LogWarning("Таймаут ожидания ответа оператора ({Timeout} сек)",
            ResolutionTimeout.TotalSeconds);

        return ErrorResolution.Timeout;
    }

    public async Task SendAskRepeatAsync(CancellationToken ct)
    {
        _logger.LogInformation("Отправка AskRepeat в PLC");
        var result = await _plcService.WriteAsync(BaseTags.AskRepeat, true, ct);
        if (result.Error != null)
        {
            _logger.LogError("Ошибка записи AskRepeat: {Error}", result.Error);
        }
    }

    #endregion

    #region Reset & Recovery

    public void Reset()
    {
        _logger.LogInformation("=== ПОЛНЫЙ СБРОС ===");
        ClearAllState();
        InvokeEventSafe(OnReset, "OnReset");
    }

    private void ClearAllState()
    {
        _interruptMessage.Clear();
        _pauseToken.Resume();
        _stateManager.ClearErrors();
        _stateManager.TransitionTo(ExecutionState.Failed);
        _statusReporter.ClearAll();
        _boilerState.Clear();
    }

    private async Task TryResumeFromPauseAsync(CancellationToken ct)
    {
        if (!await TryAcquireLockAsync(ct))
        {
            return;
        }
        try
        {
            ResumeIfPaused();
        }
        finally
        {
            ReleaseLockSafe();
        }
    }

    private void ResumeIfPaused()
    {
        if (!_pauseToken.IsPaused)
        {
            return;
        }
        ResumeExecution();
    }

    private void ResumeExecution()
    {
        _interruptMessage.Clear();
        _pauseToken.Resume();
        _notifications.ShowSuccess("Автомат восстановлен", "Тест продолжается");
        InvokeEventSafe(OnRecovered, "OnRecovered");
    }

    private void InvokeEventSafe(Action? handler, string eventName)
    {
        try
        {
            handler?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике {EventName}", eventName);
        }
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        UnsubscribeFromEvents();
        _operationLock.Dispose();
    }

    private void UnsubscribeFromEvents()
    {
        _connectionState.ConnectionStateChanged -= HandleConnectionChanged;
        _autoReady.OnStateChanged -= HandleAutoReadyChanged;
    }

    #endregion
}

/// <summary>
/// Reason for test execution interrupt.
/// </summary>
public enum InterruptReason
{
    PlcConnectionLost,
    AutoModeDisabled,
    TagTimeout
}