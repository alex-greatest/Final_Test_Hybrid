using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class PostAskEndDecisionLoopTests
{
    [Fact]
    public async Task WaitRepeatDecisionAfterAskEndAsync_DoesNotFinish_WhenCacheIsUnknown()
    {
        using var cts = new CancellationTokenSource();
        var subscription = TestInfrastructure.CreateSubscription();
        var coordinator = CreateCoordinator(subscription);

        var waitTask = InvokeWaitRepeatDecisionAfterAskEndAsync(coordinator, cts.Token);

        await Task.Delay(250);

        Assert.False(waitTask.IsCompleted);

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
    }

    [Fact]
    public async Task WaitRepeatDecisionAfterAskEndAsync_PrefersRepeat_WhenRepeatAndAskEndResetArriveTogether()
    {
        var subscription = TestInfrastructure.CreateSubscription();
        var values = TestInfrastructure.GetSubscriptionValues(subscription);
        values[BaseTags.ErrorRetry] = true;
        values[BaseTags.AskEnd] = false;
        var coordinator = CreateCoordinator(subscription);

        var shouldRepeat = await InvokeWaitRepeatDecisionAfterAskEndAsync(coordinator, CancellationToken.None);

        Assert.True(shouldRepeat);
    }

    [Fact]
    public async Task WaitRepeatDecisionAfterAskEndAsync_FinishesCleanup_WhenAskEndResetIsKnownWithoutRepeat()
    {
        var subscription = TestInfrastructure.CreateSubscription();
        var values = TestInfrastructure.GetSubscriptionValues(subscription);
        values[BaseTags.AskEnd] = false;
        var coordinator = CreateCoordinator(subscription);

        var shouldRepeat = await InvokeWaitRepeatDecisionAfterAskEndAsync(coordinator, CancellationToken.None);

        Assert.False(shouldRepeat);
    }

    private static PreExecutionCoordinator CreateCoordinator(OpcUaSubscription subscription)
    {
        var steps = new PreExecutionSteps(null!, null!, null!, null!, null!);
        var infra = new PreExecutionInfrastructure(
            null!,
            null!,
            subscription,
            null!,
            new PauseTokenSource(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            TestInfrastructure.CreateDualLogger<PreExecutionCoordinator>(),
            null!,
            null!);
        var coordinators = new PreExecutionCoordinators(null!, null!, null!, null!, null!, null!, null!);
        var state = new PreExecutionState(null!, null!, new ExecutionActivityTracker(), new ExecutionPhaseState(), new ExecutionFlowState());
        return new PreExecutionCoordinator(
            steps,
            infra,
            coordinators,
            state,
            new RuntimeTerminalState(TestInfrastructure.CreateDualLogger<RuntimeTerminalState>()));
    }

    private static Task<bool> InvokeWaitRepeatDecisionAfterAskEndAsync(PreExecutionCoordinator coordinator, CancellationToken ct)
    {
        return Assert.IsAssignableFrom<Task<bool>>(TestInfrastructure.InvokePrivate(coordinator, "WaitRepeatDecisionAfterAskEndAsync", ct));
    }
}
