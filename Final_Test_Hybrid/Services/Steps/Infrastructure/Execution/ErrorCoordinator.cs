using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Errors;
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
public partial class ErrorCoordinator : IAsyncDisposable
{
    // === Dependencies ===
    private readonly OpcUaConnectionState _connectionState;
    private readonly AutoReadySubscription _autoReady;
    private readonly PauseTokenSource _pauseToken;
    private readonly TagWaiter _tagWaiter;
    private readonly OpcUaTagService _plcService;
    private readonly ExecutionStateManager _stateManager;
    private readonly StepStatusReporter _statusReporter;
    private readonly BoilerState _boilerState;
    private readonly ExecutionActivityTracker _activityTracker;
    private readonly InterruptMessageState _interruptMessage;
    private readonly INotificationService _notifications;
    private readonly IErrorService _errorService;
    private readonly ILogger<ErrorCoordinator> _logger;

    // === Synchronization ===
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private int _isHandlingInterrupt;
    private volatile bool _disposed;

    // === Constants ===
    private static readonly TimeSpan ResolutionTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PlcReconnectDelay = TimeSpan.FromSeconds(5);

    // === Interrupt Behaviors ===
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
            Message: "Нет ответа от PLC",
            Action: InterruptAction.ResetAfterDelay)
    };

    // === Events ===
    public event Action? OnReset;
    public event Action? OnRecovered;

    public ErrorCoordinator(
        OpcUaConnectionState connectionState,
        AutoReadySubscription autoReady,
        PauseTokenSource pauseToken,
        TagWaiter tagWaiter,
        OpcUaTagService plcService,
        ExecutionStateManager stateManager,
        StepStatusReporter statusReporter,
        BoilerState boilerState,
        ExecutionActivityTracker activityTracker,
        InterruptMessageState interruptMessage,
        INotificationService notifications,
        IErrorService errorService,
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
        _errorService = errorService;
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
        if (_disposed || isConnected || !_activityTracker.IsAnyActive) { return; }
        FireAndForgetInterrupt(InterruptReason.PlcConnectionLost);
    }

    private void HandleAutoReadyChanged()
    {
        if (_disposed) { return; }

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
    }

    private async Task WaitForPendingOperationsAsync()
    {
        var spinWait = new SpinWait();
        var timeout = DateTime.UtcNow.AddSeconds(5);

        while (_isHandlingInterrupt == 1 && DateTime.UtcNow < timeout)
        {
            spinWait.SpinOnce();
            await Task.Yield();
        }
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
