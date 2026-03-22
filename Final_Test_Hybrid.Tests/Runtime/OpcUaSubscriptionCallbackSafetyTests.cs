using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Tests.TestSupport;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class OpcUaSubscriptionCallbackSafetyTests
{
    [Fact]
    public async Task SubscribeAsync_EmitCachedValueImmediately_DoesNotThrow_WhenCallbackThrowsSynchronously()
    {
        const string nodeId = "ns=3;s=\"DB_VI\".\"Block_Test\".\"End\"";
        var subscription = CreateSubscription();
        RegisterTrackedNode(subscription, nodeId);
        TestInfrastructure.SetSubscriptionValue(subscription, nodeId, true, updateSequence: 1);

        Func<object?, Task> callback = _ => throw new InvalidOperationException("boom");

        var exception = await Record.ExceptionAsync(() =>
            subscription.SubscribeAsync(
                nodeId,
                callback,
                CancellationToken.None,
                emitCachedValueImmediately: true));

        Assert.Null(exception);
    }

    [Fact]
    public async Task InvokeCallbacks_DoesNotThrow_WhenCallbacksThrowSynchronously()
    {
        const string nodeId = "ns=3;s=\"DB_VI\".\"Block_Test\".\"Error\"";
        var subscription = CreateSubscription();
        RegisterTrackedNode(subscription, nodeId);

        Func<object?, Task> valueCallback = _ => throw new InvalidOperationException("boom-value");
        Func<SubscriptionValueEntry, Task> entryCallback = _ => throw new InvalidOperationException("boom-entry");

        await subscription.SubscribeAsync(nodeId, valueCallback, CancellationToken.None);
        await TestInfrastructure.InvokePrivateAsync(
            subscription,
            "SubscribeEntryAsync",
            nodeId,
            entryCallback,
            CancellationToken.None,
            false);

        TestInfrastructure.SetSubscriptionValue(subscription, nodeId, true, updateSequence: 1);
        var exception = Record.Exception(() =>
            TestInfrastructure.InvokePrivate(subscription, "InvokeCallbacks", nodeId, true));

        Assert.Null(exception);
    }

    private static OpcUaSubscription CreateSubscription()
    {
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        return new OpcUaSubscription(
            connectionState,
            TestInfrastructure.CreateOpcUaOptions(),
            TestInfrastructure.CreateDualLogger<OpcUaSubscription>());
    }

    private static void RegisterTrackedNode(OpcUaSubscription subscription, string nodeId)
    {
        var monitoredItems = TestInfrastructure.GetPrivateField<ConcurrentDictionary<string, MonitoredItem>>(subscription, "_monitoredItems");
        monitoredItems[nodeId] = (MonitoredItem)RuntimeHelpers.GetUninitializedObject(typeof(MonitoredItem));
    }
}
