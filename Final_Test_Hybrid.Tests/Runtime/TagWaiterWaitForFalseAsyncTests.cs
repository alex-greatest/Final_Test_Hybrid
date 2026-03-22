using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Tests.TestSupport;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class TagWaiterWaitForFalseAsyncTests
{
    [Fact]
    public async Task WaitForFalseAsync_DoesNotFinishOnResume_WhenCacheStillUnknown()
    {
        const string nodeId = "ns=2;s=AskEnd";
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        var subscription = new OpcUaSubscription(
            connectionState,
            TestInfrastructure.CreateOpcUaOptions(),
            TestInfrastructure.CreateDualLogger<OpcUaSubscription>());
        RegisterTrackedNode(subscription, nodeId);

        var waiter = new TagWaiter(
            subscription,
            connectionState,
            TestInfrastructure.CreateLogger<TagWaiter>(),
            new TestStepLoggerStub());
        var pauseToken = new PauseTokenSource();
        pauseToken.Pause();

        var waitTask = waiter.WaitForFalseAsync(nodeId, pauseToken, TimeSpan.FromSeconds(2));

        await Task.Delay(100);
        pauseToken.Resume();
        await Task.Delay(100);

        Assert.False(waitTask.IsCompleted);

        PublishValue(subscription, nodeId, false);
        await waitTask.WaitAsync(TimeSpan.FromSeconds(1));
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
