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
            TestInfrastructure.CreateLogger<AutoReadySubscription>());
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

        return new TestContext(coordinator, autoReady, connectionState, runtimeTerminalState, pauseToken, errorService);
    }

    private sealed record TestContext(
        ErrorCoordinator Coordinator,
        AutoReadySubscription AutoReady,
        OpcUaConnectionState ConnectionState,
        RuntimeTerminalState RuntimeTerminalState,
        PauseTokenSource PauseToken,
        TestErrorService ErrorService);
}
