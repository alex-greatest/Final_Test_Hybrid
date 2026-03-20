using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class OpcUaSubscriptionTryGetValueTests
{
    [Fact]
    public void TryGetValue_ReturnsFalse_WhenCacheIsUnknown()
    {
        var subscription = CreateSubscription();

        var found = subscription.TryGetValue<bool>("ns=2;s=UnknownTag", out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetValue_ReturnsTypedValue_WhenCacheContainsRealValue()
    {
        const string nodeId = "ns=2;s=KnownTag";
        var subscription = CreateSubscription();
        var values = TestInfrastructure.GetSubscriptionValues(subscription);
        values[nodeId] = true;

        var found = subscription.TryGetValue<bool>(nodeId, out var value);

        Assert.True(found);
        Assert.True(value);
    }

    private static OpcUaSubscription CreateSubscription()
    {
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        return new OpcUaSubscription(
            connectionState,
            TestInfrastructure.CreateOpcUaOptions(),
            TestInfrastructure.CreateDualLogger<OpcUaSubscription>());
    }
}
