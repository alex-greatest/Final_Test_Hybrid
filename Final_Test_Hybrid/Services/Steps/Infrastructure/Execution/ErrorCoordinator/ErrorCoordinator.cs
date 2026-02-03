using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;

/// <summary>
/// Options for WaitForResolutionAsync method.
/// </summary>
public record WaitForResolutionOptions(
    string? BlockEndTag = null,
    string? BlockErrorTag = null,
    bool EnableSkip = true,
    TimeSpan? Timeout = null);

/// <summary>
/// Unified error and interrupt coordinator for test execution.
/// Handles PLC connection loss, auto mode, timeouts, and error resolution.
/// Thread-safe with proper disposal patterns.
/// </summary>
public sealed partial class ErrorCoordinator : IErrorCoordinator, IInterruptContext, IAsyncDisposable
{
    private readonly ErrorCoordinatorSubscriptions _subscriptions;
    private readonly ErrorResolutionServices _resolution;
    private readonly PauseTokenSource _pauseToken;
    private readonly InterruptBehaviorRegistry _behaviorRegistry;
    private readonly DualLogger<ErrorCoordinator> _logger;
    private readonly SemaphoreSlim _interruptLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private volatile bool _disposed;

    public event Action? OnReset;
    public event Action? OnRecovered;
    public event Action? OnInterruptChanged;

    public InterruptReason? CurrentInterrupt { get; private set; }

    public ErrorCoordinator(
        ErrorCoordinatorSubscriptions subscriptions,
        ErrorResolutionServices resolution,
        PauseTokenSource pauseToken,
        InterruptBehaviorRegistry behaviorRegistry,
        DualLogger<ErrorCoordinator> logger)
    {
        _subscriptions = subscriptions;
        _resolution = resolution;
        _pauseToken = pauseToken;
        _behaviorRegistry = behaviorRegistry;
        _logger = logger;

        SubscribeToEvents();
    }

    #region Event Subscriptions

    private void SubscribeToEvents()
    {
        _subscriptions.ConnectionState.ConnectionStateChanged += HandleConnectionChanged;
        _subscriptions.AutoReady.OnStateChanged += HandleAutoReadyChanged;
    }

    private void HandleConnectionChanged(bool isConnected)
    {
        var isActive = _subscriptions.ActivityTracker.IsAnyActive;
        if (_disposed || isConnected || !isActive) { return; }
        FireAndForgetInterrupt(InterruptReason.PlcConnectionLost);
    }

    private void HandleAutoReadyChanged()
    {
        if (_disposed) { return; }

        var isReady = _subscriptions.AutoReady.IsReady;
        var isActive = _subscriptions.ActivityTracker.IsAnyActive;

        if (isReady)
        {
            _logger.LogInformation("AutoReady ON → resume");
            FireAndForgetResume();
            return;
        }

        _logger.LogInformation("AutoReady OFF → pause");
        if (isActive)
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

    #region IInterruptContext Implementation

    void IInterruptContext.Pause() => _pauseToken.Pause();
    void IInterruptContext.Reset() => Reset();
    IErrorService IInterruptContext.ErrorService => _resolution.ErrorService;
    INotificationService IInterruptContext.Notifications => _resolution.Notifications;

    #endregion

    #region Disposal

    public async ValueTask DisposeAsync()
    {
        if (_disposed) { return; }
        _disposed = true;
        await _disposeCts.CancelAsync();
        UnsubscribeFromEvents();
        _disposeCts.Dispose();
        _interruptLock.Dispose();
        OnReset = null;
        OnRecovered = null;
        OnInterruptChanged = null;
    }

    private void UnsubscribeFromEvents()
    {
        _subscriptions.ConnectionState.ConnectionStateChanged -= HandleConnectionChanged;
        _subscriptions.AutoReady.OnStateChanged -= HandleAutoReadyChanged;
    }

    #endregion

    #region Helpers

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
