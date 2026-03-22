using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Tests.TestSupport;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class TagWaiterWaitAnyAsyncTests
{
    [Fact]
    public async Task WaitAnyAsync_ReturnsImmediately_WhenErrorSignalAlreadyTrueInCache()
    {
        const string errorTag = "ns=3;s=\"DB_VI\".\"Block_Test\".\"Error\"";
        const string endTag = "ns=3;s=\"DB_VI\".\"Block_Test\".\"End\"";
        var waiter = CreateWaiter(out var subscription);
        RegisterTrackedNode(subscription, errorTag);
        RegisterTrackedNode(subscription, endTag);
        var values = TestInfrastructure.GetSubscriptionValues(subscription);
        values[errorTag] = true;

        var result = await waiter.WaitAnyAsync(
            waiter.CreateWaitGroup<string>()
                .WaitForTrue(endTag, () => "end", "End")
                .WaitForTrue(errorTag, () => "error", "Error"),
            CancellationToken.None);

        Assert.Equal(1, result.WinnerIndex);
        Assert.Equal("error", result.Result);
    }

    [Fact]
    public async Task WaitAnyAsync_ReturnsImmediately_WhenEndSignalAlreadyTrueInCache()
    {
        const string errorTag = "ns=3;s=\"DB_VI\".\"Block_Test\".\"Error\"";
        const string endTag = "ns=3;s=\"DB_VI\".\"Block_Test\".\"End\"";
        var waiter = CreateWaiter(out var subscription);
        RegisterTrackedNode(subscription, errorTag);
        RegisterTrackedNode(subscription, endTag);
        var values = TestInfrastructure.GetSubscriptionValues(subscription);
        values[endTag] = true;

        var result = await waiter.WaitAnyAsync(
            waiter.CreateWaitGroup<string>()
                .WaitForTrue(endTag, () => "end", "End")
                .WaitForTrue(errorTag, () => "error", "Error"),
            CancellationToken.None);

        Assert.Equal(0, result.WinnerIndex);
        Assert.Equal("end", result.Result);
    }

    [Fact]
    public async Task WaitAnyAsync_DoesNotFinish_WhenCacheIsUnknownUntilPublish()
    {
        const string errorTag = "ns=3;s=\"DB_VI\".\"Block_Test\".\"Error\"";
        const string endTag = "ns=3;s=\"DB_VI\".\"Block_Test\".\"End\"";
        var waiter = CreateWaiter(out var subscription);
        RegisterTrackedNode(subscription, errorTag);
        RegisterTrackedNode(subscription, endTag);

        var waitTask = waiter.WaitAnyAsync(
            waiter.CreateWaitGroup<string>()
                .WaitForTrue(endTag, () => "end", "End")
                .WaitForTrue(errorTag, () => "error", "Error"),
            CancellationToken.None);

        await Task.Delay(100);
        Assert.False(waitTask.IsCompleted);

        PublishValue(subscription, errorTag, true);
        var result = await waitTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("error", result.Result);
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

    private static void PublishValue(OpcUaSubscription subscription, string nodeId, bool value)
    {
        var nextSequence = GetNextSequence(subscription);
        TestInfrastructure.SetSubscriptionValue(subscription, nodeId, value, nextSequence);
        TestInfrastructure.InvokePrivate(subscription, "InvokeCallbacks", nodeId, value);
    }

    private static ulong GetNextSequence(OpcUaSubscription subscription)
    {
        var nextSequence = 1UL;
        foreach (var sequence in TestInfrastructure.GetSubscriptionValueSequences(subscription).Values)
        {
            nextSequence = Math.Max(nextSequence, sequence + 1);
        }

        return nextSequence;
    }
}
