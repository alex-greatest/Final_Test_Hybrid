using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class CompletionDecisionLoopTests
{
    [Fact]
    public async Task WaitCompletionDecisionAsync_DoesNotFinish_WhenCacheIsUnknown()
    {
        using var cts = new CancellationTokenSource();
        var subscription = TestInfrastructure.CreateSubscription();
        var coordinator = CreateCoordinator(subscription);

        var waitTask = InvokeWaitCompletionDecisionAsync(coordinator, cts.Token);

        await Task.Delay(250);

        Assert.False(waitTask.IsCompleted);

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
    }

    [Fact]
    public async Task WaitCompletionDecisionAsync_PrefersRepeat_WhenRepeatAndEndResetArriveTogether()
    {
        var subscription = TestInfrastructure.CreateSubscription();
        var values = TestInfrastructure.GetSubscriptionValues(subscription);
        values[BaseTags.ErrorRetry] = true;
        values[BaseTags.ErrorSkip] = false;
        var coordinator = CreateCoordinator(subscription);

        var shouldRepeat = await InvokeWaitCompletionDecisionAsync(coordinator, CancellationToken.None);

        Assert.True(shouldRepeat);
    }

    [Fact]
    public async Task WaitCompletionDecisionAsync_Finishes_WhenEndResetIsKnownWithoutRepeat()
    {
        var subscription = TestInfrastructure.CreateSubscription();
        var values = TestInfrastructure.GetSubscriptionValues(subscription);
        values[BaseTags.ErrorSkip] = false;
        var coordinator = CreateCoordinator(subscription);

        var shouldRepeat = await InvokeWaitCompletionDecisionAsync(coordinator, CancellationToken.None);

        Assert.False(shouldRepeat);
    }

    private static TestCompletionCoordinator CreateCoordinator(OpcUaSubscription subscription)
    {
        var deps = new TestCompletionDependencies(null!, null!, subscription, null!, null!);
        return new TestCompletionCoordinator(
            deps,
            TestInfrastructure.CreateDualLogger<TestCompletionCoordinator>(),
            new RuntimeTerminalState(TestInfrastructure.CreateDualLogger<RuntimeTerminalState>()));
    }

    private static Task<bool> InvokeWaitCompletionDecisionAsync(TestCompletionCoordinator coordinator, CancellationToken ct)
    {
        return Assert.IsAssignableFrom<Task<bool>>(TestInfrastructure.InvokePrivate(coordinator, "WaitCompletionDecisionAsync", ct));
    }
}
