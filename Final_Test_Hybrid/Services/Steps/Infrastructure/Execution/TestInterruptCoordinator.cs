using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

/// <summary>
/// Handles test execution interrupts (PLC connection loss, auto mode disabled, timeouts).
/// Coordinates pause/resume and reset behaviors based on interrupt type.
/// </summary>
public class TestInterruptCoordinator : IDisposable
{
    private readonly OpcUaConnectionState _connectionState;
    private readonly AutoReadySubscription _autoReady;
    private readonly PauseTokenSource _pauseToken;
    private readonly TestExecutionCoordinator _testCoordinator;
    private readonly StepStatusReporter _statusReporter;
    private readonly BoilerState _boilerState;
    private readonly ExecutionActivityTracker _activityTracker;
    private readonly InterruptMessageState _interruptMessage;
    private readonly INotificationService _notifications;
    private readonly ILogger<TestInterruptCoordinator> _logger;
    private readonly SemaphoreSlim _interruptLock = new(1, 1);
    private int _isHandlingInterrupt;

    private static readonly Dictionary<TestInterruptReason, InterruptBehavior> Behaviors = new()
    {
        [TestInterruptReason.PlcConnectionLost] = new InterruptBehavior(
            Message: "Потеря связи с PLC",
            Action: InterruptAction.ResetAfterDelay,
            Delay: TimeSpan.FromSeconds(5)),

        [TestInterruptReason.AutoModeDisabled] = new InterruptBehavior(
            Message: "Нет автомата",
            Action: InterruptAction.PauseAndWait,
            WaitForRecovery: null),

        [TestInterruptReason.TagTimeout] = new InterruptBehavior(
            Message: "Нет ответа",
            Action: InterruptAction.ResetAfterDelay,
            Delay: null)
    };

    public event Action<TestInterruptReason, InterruptBehavior>? OnInterrupt;
    public event Action? OnReset;
    public event Action? OnRecovered;

    public TestInterruptCoordinator(
        OpcUaConnectionState connectionState,
        AutoReadySubscription autoReady,
        PauseTokenSource pauseToken,
        TestExecutionCoordinator testCoordinator,
        StepStatusReporter statusReporter,
        BoilerState boilerState,
        ExecutionActivityTracker activityTracker,
        InterruptMessageState interruptMessage,
        INotificationService notifications,
        ILogger<TestInterruptCoordinator> logger)
    {
        _connectionState = connectionState;
        _autoReady = autoReady;
        _pauseToken = pauseToken;
        _testCoordinator = testCoordinator;
        _statusReporter = statusReporter;
        _boilerState = boilerState;
        _activityTracker = activityTracker;
        _interruptMessage = interruptMessage;
        _notifications = notifications;
        _logger = logger;

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        _connectionState.ConnectionStateChanged += HandleConnectionChanged;
        _autoReady.OnChange += HandleAutoReadyChanged;
    }

    private void HandleConnectionChanged(bool isConnected)
    {
        if (isConnected || !_activityTracker.IsAnyActive)
        {
            return;
        }
        TriggerInterruptAsync(TestInterruptReason.PlcConnectionLost);
    }

    private void HandleAutoReadyChanged()
    {
        if (_autoReady.IsReady)
        {
            TriggerResumeAsync();
            return;
        }
        HandleAutoModeDisabled();
    }

    private void HandleAutoModeDisabled()
    {
        if (!_activityTracker.IsAnyActive)
        {
            return;
        }
        TriggerInterruptAsync(TestInterruptReason.AutoModeDisabled);
    }

    private void TriggerInterruptAsync(TestInterruptReason reason)
    {
        _ = HandleInterruptAsync(reason).ContinueWith(
            task => LogInterruptError(task, reason),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private void TriggerResumeAsync()
    {
        _ = TryResumeFromPauseAsync().ContinueWith(
            task => _logger.LogError(task.Exception, "Ошибка восстановления"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private void LogInterruptError(Task task, TestInterruptReason reason)
    {
        _logger.LogError(task.Exception, "Ошибка обработки прерывания: {Reason}", reason);
    }

    private async Task TryResumeFromPauseAsync()
    {
        await _interruptLock.WaitAsync();
        try
        {
            if (!_pauseToken.IsPaused)
            {
                return;
            }
            ResumeExecution();
        }
        finally
        {
            _interruptLock.Release();
        }
    }

    private void ResumeExecution()
    {
        _interruptMessage.Clear();
        _pauseToken.Resume();
        _notifications.ShowSuccess("Автомат восстановлен", "Тест продолжается");
        OnRecovered?.Invoke();
    }

    public async Task HandleInterruptAsync(TestInterruptReason reason, CancellationToken ct = default)
    {
        if (!TryAcquireInterruptFlag())
        {
            return;
        }

        await _interruptLock.WaitAsync(ct);
        try
        {
            await ProcessInterruptAsync(reason, ct);
        }
        finally
        {
            _interruptLock.Release();
            ReleaseInterruptFlag();
        }
    }

    private bool TryAcquireInterruptFlag()
    {
        return Interlocked.CompareExchange(ref _isHandlingInterrupt, 1, 0) == 0;
    }

    private void ReleaseInterruptFlag()
    {
        Interlocked.Exchange(ref _isHandlingInterrupt, 0);
    }

    private async Task ProcessInterruptAsync(TestInterruptReason reason, CancellationToken ct)
    {
        var behavior = Behaviors[reason];
        _logger.LogWarning("Прерывание: {Reason} - {Message}", reason, behavior.Message);
        _interruptMessage.SetMessage(behavior.Message);
        OnInterrupt?.Invoke(reason, behavior);
        _notifications.ShowWarning(behavior.Message, GetInterruptDetails(behavior));
        await ExecuteInterruptActionAsync(behavior, ct);
    }

    private async Task ExecuteInterruptActionAsync(InterruptBehavior behavior, CancellationToken ct)
    {
        switch (behavior.Action)
        {
            case InterruptAction.PauseAndWait:
                _pauseToken.Pause();
                break;

            case InterruptAction.ResetAfterDelay:
                await ResetAfterDelayAsync(behavior.Delay, ct);
                break;
        }
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

    private async Task ResetAfterDelayAsync(TimeSpan? delay, CancellationToken ct)
    {
        if (delay.HasValue)
        {
            await Task.Delay(delay.Value, ct);
        }
        Reset();
    }

    private void Reset()
    {
        _logger.LogInformation("Сброс теста");
        _interruptMessage.Clear();
        _pauseToken.Resume();
        _testCoordinator.Stop();
        _statusReporter.ClearAll();
        _boilerState.Clear();
        OnReset?.Invoke();
    }

    public void Dispose()
    {
        _connectionState.ConnectionStateChanged -= HandleConnectionChanged;
        _autoReady.OnChange -= HandleAutoReadyChanged;
        _interruptLock.Dispose();
    }
}
