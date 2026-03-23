using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class ErrorCoordinatorOwnershipTests
{
    [Fact]
    public async Task ConnectionLoss_DuringTerminalHandshake_RaisesPlcConnectionLost()
    {
        var executed = new TaskCompletionSource<InterruptReason>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = CreateContext(
        [
            new InterruptBehaviorStub(InterruptReason.PlcConnectionLost, (_, _) => Task.CompletedTask, executed)
        ]);

        context.RuntimeTerminalState.SetCompletionActive(true);
        context.ConnectionState.SetConnected(true, "test");
        context.ConnectionState.SetConnected(false, "test");

        var reason = await executed.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(InterruptReason.PlcConnectionLost, reason);
        Assert.Equal(InterruptReason.PlcConnectionLost, context.Coordinator.CurrentInterrupt);
    }

    [Fact]
    public async Task ConnectionLoss_DuringPostAskEnd_RaisesPlcConnectionLost()
    {
        var executed = new TaskCompletionSource<InterruptReason>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = CreateContext(
        [
            new InterruptBehaviorStub(InterruptReason.PlcConnectionLost, (_, _) => Task.CompletedTask, executed)
        ]);

        context.RuntimeTerminalState.SetPostAskEndActive(true);
        context.ConnectionState.SetConnected(true, "test");
        context.ConnectionState.SetConnected(false, "test");

        var reason = await executed.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(InterruptReason.PlcConnectionLost, reason);
        Assert.Equal(InterruptReason.PlcConnectionLost, context.Coordinator.CurrentInterrupt);
    }

    [Fact]
    public async Task AutoReadyOff_DuringTerminalHandshake_DoesNotRaiseAutoModeDisabled()
    {
        var executed = new TaskCompletionSource<InterruptReason>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = CreateContext(
        [
            new InterruptBehaviorStub(InterruptReason.AutoModeDisabled, (_, _) => Task.CompletedTask, executed)
        ]);

        context.RuntimeTerminalState.SetPostAskEndActive(true);
        await SetAutoReadyAsync(context.AutoReady, false);
        await Task.Delay(100);

        Assert.False(executed.Task.IsCompleted);
        Assert.Null(context.Coordinator.CurrentInterrupt);
        Assert.False(context.PauseToken.IsPaused);
    }

    [Fact]
    public async Task AutoReadyOn_ResumesOnlyAutoModeDisabled()
    {
        var context = CreateContext(
        [
            new InterruptBehaviorStub(InterruptReason.AutoModeDisabled, (interruptContext, _) =>
            {
                interruptContext.Pause();
                return Task.CompletedTask;
            })
        ]);
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Coordinator.OnRecovered += () => recovered.TrySetResult();

        await context.Coordinator.HandleInterruptAsync(InterruptReason.AutoModeDisabled);
        Assert.True(context.PauseToken.IsPaused);

        await SetAutoReadyAsync(context.AutoReady, true);
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(context.PauseToken.IsPaused);
        Assert.Null(context.Coordinator.CurrentInterrupt);
        Assert.Contains(ErrorDefinitions.OpcConnectionLost.Code, context.ErrorService.ClearedCodes);
        Assert.Contains(ErrorDefinitions.TagReadTimeout.Code, context.ErrorService.ClearedCodes);
    }

    [Fact]
    public async Task AutoReadyOn_RepeatedTrueAfterRecovery_DoesNotRepeatRecoverySideEffects()
    {
        var context = CreateContext(
        [
            new InterruptBehaviorStub(InterruptReason.AutoModeDisabled, (interruptContext, _) =>
            {
                interruptContext.Pause();
                return Task.CompletedTask;
            })
        ]);
        var recoveredCounter = new InvocationCounter();
        context.Coordinator.OnRecovered += recoveredCounter.Increment;

        await context.Coordinator.HandleInterruptAsync(InterruptReason.AutoModeDisabled);
        Assert.True(context.PauseToken.IsPaused);

        await SetAutoReadyAsync(context.AutoReady, true);
        await WaitForRecoveredCountAsync(recoveredCounter);

        await SetAutoReadyAsync(context.AutoReady, true);
        await Task.Delay(100);

        Assert.Equal(1, recoveredCounter.Value);
        Assert.Equal(1, context.ErrorService.ClearedCodes.Count(code => code == ErrorDefinitions.OpcConnectionLost.Code));
        Assert.Equal(1, context.ErrorService.ClearedCodes.Count(code => code == ErrorDefinitions.TagReadTimeout.Code));
    }

    [Fact]
    public async Task AutoReadyOff_RepeatedFalseDuringAutoModeDisabled_DoesNotRepeatInterruptHandling()
    {
        var executionCounter = new InvocationCounter();
        var executed = new TaskCompletionSource<InterruptReason>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = CreateContext(
        [
            new InterruptBehaviorStub(InterruptReason.AutoModeDisabled, (interruptContext, _) =>
            {
                executionCounter.Increment();
                interruptContext.Pause();
                return Task.CompletedTask;
            }, executed)
        ]);
        context.ActivityTracker.SetPreExecutionActive(true);

        await SetAutoReadyAsync(context.AutoReady, false);
        await executed.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await SetAutoReadyAsync(context.AutoReady, false);
        await Task.Delay(100);

        Assert.Equal(1, executionCounter.Value);
        Assert.True(context.PauseToken.IsPaused);
        Assert.Equal(InterruptReason.AutoModeDisabled, context.Coordinator.CurrentInterrupt);
    }

    [Fact]
    public async Task AutoReadyOn_DoesNotResumeBoilerLock()
    {
        var context = CreateContext(
        [
            new InterruptBehaviorStub(InterruptReason.BoilerLock, (interruptContext, _) =>
            {
                interruptContext.Pause();
                return Task.CompletedTask;
            })
        ]);

        await context.Coordinator.HandleInterruptAsync(InterruptReason.BoilerLock);
        Assert.True(context.PauseToken.IsPaused);

        await SetAutoReadyAsync(context.AutoReady, true);
        await Task.Delay(100);

        Assert.True(context.PauseToken.IsPaused);
        Assert.Equal(InterruptReason.BoilerLock, context.Coordinator.CurrentInterrupt);
    }

    private static async Task WaitForRecoveredCountAsync(InvocationCounter counter)
    {
        var deadline = DateTime.UtcNow.AddSeconds(1);
        while (DateTime.UtcNow < deadline)
        {
            if (counter.Value >= 1)
            {
                return;
            }
            await Task.Delay(10);
        }
        throw new TimeoutException("OnRecovered не был вызван.");
    }

    private static async Task SetAutoReadyAsync(AutoReadySubscription autoReady, bool isReady)
    {
        await TestInfrastructure.InvokePrivateAsync(autoReady, "OnValueChanged", isReady);
    }

    private static TestContext CreateContext(IEnumerable<IInterruptBehavior> behaviors)
    {
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        var subscription = new OpcUaSubscription(
            connectionState,
            TestInfrastructure.CreateOpcUaOptions(),
            TestInfrastructure.CreateDualLogger<OpcUaSubscription>());
        var autoReady = new AutoReadySubscription(
            subscription,
            connectionState,
            TestInfrastructure.CreateHeartbeatHealthMonitor(),
            TestInfrastructure.CreateDualLogger<AutoReadySubscription>());
        var activityTracker = new ExecutionActivityTracker();
        var runtimeTerminalState = new RuntimeTerminalState(TestInfrastructure.CreateDualLogger<RuntimeTerminalState>());
        var errorService = new TestErrorService();
        var pauseToken = new PauseTokenSource();
        var coordinator = new ErrorCoordinator(
            new ErrorCoordinatorSubscriptions(connectionState, autoReady, activityTracker, runtimeTerminalState),
            new ErrorResolutionServices(null!, null!, errorService, new TestNotificationService()),
            pauseToken,
            new InterruptBehaviorRegistry(behaviors),
            TestInfrastructure.CreateDualLogger<ErrorCoordinator>());

        return new TestContext(coordinator, autoReady, connectionState, activityTracker, runtimeTerminalState, pauseToken, errorService);
    }

    private sealed record TestContext(
        ErrorCoordinator Coordinator,
        AutoReadySubscription AutoReady,
        OpcUaConnectionState ConnectionState,
        ExecutionActivityTracker ActivityTracker,
        RuntimeTerminalState RuntimeTerminalState,
        PauseTokenSource PauseToken,
        TestErrorService ErrorService);

    private sealed class InvocationCounter
    {
        private int _value;

        public int Value => Volatile.Read(ref _value);

        public void Increment()
        {
            Interlocked.Increment(ref _value);
        }
    }
}
