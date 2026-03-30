using System.Collections.Concurrent;
using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Tests.TestSupport;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class PlcErrorMonitorServiceTests
{
    [Fact]
    public async Task StartMonitoringAsync_SubscribesAllNonDeferredPlcErrorsOnly()
    {
        var subscription = TestInfrastructure.CreateSubscription();
        SeedMonitoredItems(subscription);
        var errorService = new TestErrorService();
        var service = new PlcErrorMonitorService(
            subscription,
            errorService,
            TestInfrastructure.CreateLogger<PlcErrorMonitorService>());

        await service.StartMonitoringAsync();

        var callbacks = TestInfrastructure
            .GetPrivateField<Dictionary<string, List<Func<object?, Task>>>>(subscription, "_callbacks");
        var deferredTags = ErrorDefinitions.DeferredPlcErrors
            .Select(error => error.PlcTag)
            .OfType<string>()
            .ToHashSet();
        var expectedTags = ErrorDefinitions.PlcErrors
            .Where(error => ErrorDefinitions.DeferredPlcErrors.All(deferred => deferred.Code != error.Code))
            .Select(error => error.PlcTag)
            .OfType<string>()
            .ToHashSet();

        Assert.Equal(expectedTags.Count, callbacks.Count);
        Assert.Equal(expectedTags, callbacks.Keys.ToHashSet());
        Assert.DoesNotContain(deferredTags, tag => callbacks.ContainsKey(tag));
    }

    [Fact]
    public async Task NormalPlcErrorCallback_RaisesAndClearsError()
    {
        var subscription = TestInfrastructure.CreateSubscription();
        SeedMonitoredItems(subscription);
        var errorService = new TestErrorService();
        var service = new PlcErrorMonitorService(
            subscription,
            errorService,
            TestInfrastructure.CreateLogger<PlcErrorMonitorService>());

        await service.StartMonitoringAsync();

        var error = ErrorDefinitions.Message_NoMode;
        TestInfrastructure.InvokePrivate(subscription, "InvokeCallbacks", error.PlcTag!, true);
        await Task.Delay(20);
        TestInfrastructure.InvokePrivate(subscription, "InvokeCallbacks", error.PlcTag!, false);
        await Task.Delay(20);

        Assert.Contains(error.Code, errorService.RaisedPlcCodes);
        Assert.Contains(error.Code, errorService.ClearedPlcCodes);
    }

    [Fact]
    public async Task DuplicateTrueCallback_RaisesPlcErrorOnlyOnce()
    {
        var subscription = TestInfrastructure.CreateSubscription();
        SeedMonitoredItems(subscription);
        var errorService = new TestErrorService();
        var service = new PlcErrorMonitorService(
            subscription,
            errorService,
            TestInfrastructure.CreateLogger<PlcErrorMonitorService>());

        await service.StartMonitoringAsync();

        var error = ErrorDefinitions.Message_NoMode;
        TestInfrastructure.InvokePrivate(subscription, "InvokeCallbacks", error.PlcTag!, true);
        await Task.Delay(20);
        TestInfrastructure.InvokePrivate(subscription, "InvokeCallbacks", error.PlcTag!, true);
        await Task.Delay(20);

        Assert.Equal(1, errorService.RaisedPlcCodes.Count(code => code == error.Code));
    }

    [Fact]
    public async Task DuplicateFalseCallback_ClearsPlcErrorOnlyOnce()
    {
        var subscription = TestInfrastructure.CreateSubscription();
        SeedMonitoredItems(subscription);
        var errorService = new TestErrorService();
        var service = new PlcErrorMonitorService(
            subscription,
            errorService,
            TestInfrastructure.CreateLogger<PlcErrorMonitorService>());

        await service.StartMonitoringAsync();

        var error = ErrorDefinitions.Message_NoMode;
        TestInfrastructure.InvokePrivate(subscription, "InvokeCallbacks", error.PlcTag!, true);
        await Task.Delay(20);
        TestInfrastructure.InvokePrivate(subscription, "InvokeCallbacks", error.PlcTag!, false);
        await Task.Delay(20);
        TestInfrastructure.InvokePrivate(subscription, "InvokeCallbacks", error.PlcTag!, false);
        await Task.Delay(20);

        Assert.Equal(1, errorService.ClearedPlcCodes.Count(code => code == error.Code));
    }

    private static void SeedMonitoredItems(OpcUaSubscription subscription)
    {
        var monitoredItems = TestInfrastructure
            .GetPrivateField<ConcurrentDictionary<string, MonitoredItem>>(subscription, "_monitoredItems");

        foreach (var plcTag in ErrorDefinitions.PlcErrors.Select(error => error.PlcTag).OfType<string>())
        {
            monitoredItems.TryAdd(plcTag, new MonitoredItem());
        }
    }
}
