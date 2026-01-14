using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;

/// <summary>
/// Unified error and interrupt coordinator for test execution.
/// Handles PLC connection loss, auto mode, timeouts, and error resolution.
/// Thread-safe with proper disposal patterns.
/// </summary>
public partial class ErrorCoordinator : IErrorCoordinator, IInterruptContext, IAsyncDisposable
{
    // === Dependencies ===
    private readonly ErrorCoordinatorSubscriptions _subscriptions;
    private readonly ErrorResolutionServices _resolution;
    private readonly ErrorCoordinatorState _state;
    private readonly InterruptBehaviorRegistry _behaviorRegistry;
    private readonly ILogger<ErrorCoordinator> _logger;

    // === Synchronization ===
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private int _isHandlingInterrupt;
    private int _activeOperations;
    private volatile bool _disposed;

    // === Constants ===
    private static readonly TimeSpan ResolutionTimeout = TimeSpan.FromSeconds(60);

    // === Events ===
    public event Action? OnReset;
    public event Action? OnRecovered;
    public event Action? OnInterruptChanged;

    // === State ===
    public InterruptReason? CurrentInterrupt { get; private set; }

    public ErrorCoordinator(
        ErrorCoordinatorSubscriptions subscriptions,
        ErrorResolutionServices resolution,
        ErrorCoordinatorState state,
        InterruptBehaviorRegistry behaviorRegistry,
        ILogger<ErrorCoordinator> logger)
    {
        _subscriptions = subscriptions;
        _resolution = resolution;
        _state = state;
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
        if (_disposed || isConnected || !_subscriptions.ActivityTracker.IsAnyActive) { return; }
        FireAndForgetInterrupt(InterruptReason.PlcConnectionLost);
    }

    private void HandleAutoReadyChanged()
    {
        if (_disposed) { return; }

        if (_subscriptions.AutoReady.IsReady)
        {
            FireAndForgetResume();
            return;
        }

        if (_subscriptions.ActivityTracker.IsAnyActive)
        {
            FireAndForgetInterrupt(InterruptReason.AutoModeDisabled);
        }
    }

    private void FireAndForgetInterrupt(InterruptReason reason)
    {
        _ = HandleInterruptAsync(reason, _disposeCts.Token).ContinueWith(
            t => LoggerExtensions.LogError((ILogger)_logger, (Exception?)t.Exception, "Ошибка обработки {Reason}", reason),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private void FireAndForgetResume()
    {
        _ = TryResumeFromPauseAsync(_disposeCts.Token).ContinueWith(
            t => _logger.LogError(t.Exception, "Ошибка восстановления"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    #endregion

    #region Disposal

    public async ValueTask DisposeAsync()
    {
        if (_disposed) { return; }
        _disposed = true;

        await _disposeCts.CancelAsync();
        UnsubscribeFromEvents();

        await WaitForPendingOperationsAsync();

        _disposeCts.Dispose();
        _operationLock.Dispose();
        OnReset = null;
        OnRecovered = null;
        OnInterruptChanged = null;
    }

    private async Task WaitForPendingOperationsAsync()
    {
        var spinWait = new SpinWait();
        var timeout = DateTime.UtcNow.AddSeconds(5);

        while (Volatile.Read(ref _activeOperations) > 0 && DateTime.UtcNow < timeout)
        {
            spinWait.SpinOnce();
            await Task.Yield();
        }
    }

    private void UnsubscribeFromEvents()
    {
        _subscriptions.ConnectionState.ConnectionStateChanged -= HandleConnectionChanged;
        _subscriptions.AutoReady.OnStateChanged -= HandleAutoReadyChanged;
    }

    #endregion

    #region IInterruptContext Implementation

    void IInterruptContext.Pause() => _state.PauseToken.Pause();
    void IInterruptContext.Reset() => Reset();
    IErrorService IInterruptContext.ErrorService => _resolution.ErrorService;
    INotificationService IInterruptContext.Notifications => _resolution.Notifications;

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
