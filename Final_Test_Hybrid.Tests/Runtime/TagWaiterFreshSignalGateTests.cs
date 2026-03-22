using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Tests.TestSupport;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class TagWaiterFreshSignalGateTests
{
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Block_Test\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Block_Test\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Block_Test\".\"Error\"";

    [Fact]
    public async Task WaitAnyAsync_IgnoresStaleCachedError_AndAcceptsFreshEnd()
    {
        var waiter = CreateWaiter(out var subscription);
        RegisterTrackedNode(subscription, EndTag);
        RegisterTrackedNode(subscription, ErrorTag);
        TestInfrastructure.SetSubscriptionValue(subscription, ErrorTag, true, updateSequence: 1);

        using var _ = ExecutionFreshSignalContext.Enter(StartTag, EndTag, ErrorTag);
        ExecutionFreshSignalContext.MarkAttemptStarted(StartTag, true, barrier: 1);

        var waitTask = waiter.WaitAnyAsync(
            waiter.CreateWaitGroup<string>()
                .WaitForTrue(EndTag, () => "end", "End")
                .WaitForTrue(ErrorTag, () => "error", "Error"),
            CancellationToken.None);

        await Task.Delay(100);
        Assert.False(waitTask.IsCompleted);

        PublishValue(subscription, EndTag, true, updateSequence: 2);
        var result = await waitTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("end", result.Result);
    }

    [Fact]
    public async Task WaitAnyAsync_ReturnsImmediately_WhenFreshEndIsAlreadyCachedAfterBarrier()
    {
        var waiter = CreateWaiter(out var subscription);
        RegisterTrackedNode(subscription, EndTag);
        RegisterTrackedNode(subscription, ErrorTag);

        using var _ = ExecutionFreshSignalContext.Enter(StartTag, EndTag, ErrorTag);
        ExecutionFreshSignalContext.MarkAttemptStarted(StartTag, true, barrier: 1);
        TestInfrastructure.SetSubscriptionValue(subscription, EndTag, true, updateSequence: 2);

        var result = await waiter.WaitAnyAsync(
            waiter.CreateWaitGroup<string>()
                .WaitForTrue(EndTag, () => "end", "End")
                .WaitForTrue(ErrorTag, () => "error", "Error"),
            CancellationToken.None);

        Assert.Equal("end", result.Result);
    }

    private static TagWaiter CreateWaiter(out OpcUaSubscription subscription)
    {
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        subscription = new OpcUaSubscription(
            connectionState,
            TestInfrastructure.CreateOpcUaOptions(),
            TestInfrastructure.CreateDualLogger<OpcUaSubscription>());
        return new TagWaiter(
            subscription,
            connectionState,
            TestInfrastructure.CreateLogger<TagWaiter>(),
            new TestStepLoggerStub());
    }

    private static void RegisterTrackedNode(OpcUaSubscription subscription, string nodeId)
    {
        var monitoredItems = TestInfrastructure.GetPrivateField<ConcurrentDictionary<string, MonitoredItem>>(subscription, "_monitoredItems");
        monitoredItems[nodeId] = (MonitoredItem)RuntimeHelpers.GetUninitializedObject(typeof(MonitoredItem));
    }

    private static void PublishValue(OpcUaSubscription subscription, string nodeId, bool value, ulong updateSequence)
    {
        TestInfrastructure.SetSubscriptionValue(subscription, nodeId, value, updateSequence);
        TestInfrastructure.InvokePrivate(subscription, "InvokeCallbacks", nodeId, value);
    }
}
