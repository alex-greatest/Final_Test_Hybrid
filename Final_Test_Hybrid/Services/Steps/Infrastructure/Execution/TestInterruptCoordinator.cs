using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class TestInterruptCoordinator : IDisposable
{
    private readonly OpcUaConnectionState _connectionState;
    private readonly AutoReadySubscription _autoReady;
    private readonly PauseTokenSource _pauseToken;
    private readonly TestExecutionCoordinator _testCoordinator;
    private readonly StepStatusReporter _statusReporter;
    private readonly BoilerState _boilerState;
    private readonly INotificationService _notifications;
    private readonly ILogger<TestInterruptCoordinator> _logger;
    private int _isHandlingInterrupt;

    private readonly Dictionary<TestInterruptReason, InterruptBehavior> _behaviors = new()
    {
        [TestInterruptReason.PlcConnectionLost] = new(
            Message: "Потеря связи с PLC",
            Action: InterruptAction.ResetAfterDelay,
            Delay: TimeSpan.FromSeconds(5)),

        [TestInterruptReason.AutoModeDisabled] = new(
            Message: "Нет автомата",
            Action: InterruptAction.PauseAndWait,
            WaitForRecovery: null),

        [TestInterruptReason.TagTimeout] = new(
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
        INotificationService notifications,
        ILogger<TestInterruptCoordinator> logger)
    {
        _connectionState = connectionState;
        _autoReady = autoReady;
        _pauseToken = pauseToken;
        _testCoordinator = testCoordinator;
        _statusReporter = statusReporter;
        _boilerState = boilerState;
        _notifications = notifications;
        _logger = logger;

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        _connectionState.ConnectionStateChanged += HandleConnectionChanged;
        _autoReady.OnChange += HandleAutoReadyChanged;
    }

    private void HandleConnectionChanged(bool connected)
    {
        if (connected || !_testCoordinator.IsRunning)
        {
            return;
        }
        HandleInterruptAsync(TestInterruptReason.PlcConnectionLost)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Ошибка обработки потери связи");
                }
            });
    }

    private void HandleAutoReadyChanged()
    {
        if (!_autoReady.IsReady && _testCoordinator.IsRunning)
        {
            TriggerAutoModeDisabledInterrupt();
            return;
        }
        TryResumeFromAutoReady();
    }

    private void TriggerAutoModeDisabledInterrupt()
    {
        HandleInterruptAsync(TestInterruptReason.AutoModeDisabled)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Ошибка обработки потери автомата");
                }
            });
    }

    private void TryResumeFromAutoReady()
    {
        if (!_autoReady.IsReady || !_pauseToken.IsPaused)
        {
            return;
        }
        _pauseToken.Resume();
        _notifications.ShowSuccess("Автомат восстановлен", "Тест продолжается");
        OnRecovered?.Invoke();
    }

    public async Task HandleInterruptAsync(TestInterruptReason reason, CancellationToken ct = default)
    {
        if (Interlocked.CompareExchange(ref _isHandlingInterrupt, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var behavior = _behaviors[reason];
            _logger.LogWarning("Прерывание: {Reason} - {Message}", reason, behavior.Message);

            OnInterrupt?.Invoke(reason, behavior);
            _notifications.ShowWarning(behavior.Message, GetInterruptDetails(behavior));

            await ExecuteInterruptActionAsync(behavior, ct);
        }
        finally
        {
            Interlocked.Exchange(ref _isHandlingInterrupt, 0);
        }
    }

    private async Task ExecuteInterruptActionAsync(InterruptBehavior behavior, CancellationToken ct)
    {
        switch (behavior.Action)
        {
            case InterruptAction.PauseAndWait:
                _pauseToken.Pause();
                break;

            case InterruptAction.ResetAfterDelay:
                await ResetAfterDelayAsync(behavior, ct);
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

    private async Task ResetAfterDelayAsync(InterruptBehavior behavior, CancellationToken ct)
    {
        if (behavior.Delay.HasValue)
        {
            await Task.Delay(behavior.Delay.Value, ct);
        }
        Reset();
    }

    private void Reset()
    {
        _logger.LogInformation("Сброс теста");
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
    }
}
