using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

#pragma warning disable CS0618 // Sync OPC UA methods are intentional for Blazor sync lifecycle

namespace Final_Test_Hybrid.Services.OpcUa;

public sealed partial class OpcUaSubscriptionService
{

    private void OnSessionRecreated()
    {
        if (IsDisposed)
        {
            return;
        }
        _ = RecreateAllSubscriptionsAsync().ContinueWith(
            t => _logger.LogError(t.Exception, "Error recreating subscriptions"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private async Task RecreateAllSubscriptionsAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (IsDisposed || !IsInitialized)
            {
                return;
            }
            await RecreateAllSubscriptionsCoreAsync();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task RecreateAllSubscriptionsCoreAsync()
    {
        if (_subscriptions.Count == 0)
        {
            return;
        }
        await DisposeOldSubscriptionAsync();
        await CreateNewSubscriptionAsync();
        _logger.LogInformation("Recreated {Count} subscriptions after session recreation", _subscriptions.Count);
    }

    private async Task DisposeOldSubscriptionAsync()
    {
        if (_opcSubscription is null)
        {
            return;
        }
        foreach (var entry in _subscriptions.Values)
        {
            if (entry.MonitoredItem is not null)
            {
                entry.MonitoredItem.Notification -= OnDataChange;
            }
        }
        try
        {
            await _connectionService.ExecuteWithSessionAsync(session =>
            {
                session.RemoveSubscription(_opcSubscription);
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing old subscription from session");
        }
        _opcSubscription = null;
    }

    private async Task CreateNewSubscriptionAsync()
    {
        await _connectionService.ExecuteWithSessionAsync(session =>
        {
            CreateSubscriptionOnSession(session);
            RecreateAllMonitoredItems();
            _opcSubscription!.ApplyChanges();
            return Task.CompletedTask;
        });
    }

    private void CreateSubscriptionOnSession(ISession session)
    {
        _opcSubscription = new Subscription(session.DefaultSubscription)
        {
            PublishingInterval = _settings.PublishingIntervalMs,
            LifetimeCount = 1000,
            KeepAliveCount = 10,
            MaxNotificationsPerPublish = 1000,
            PublishingEnabled = true
        };
        session.AddSubscription(_opcSubscription);
        _opcSubscription.Create();
    }

    private void RecreateAllMonitoredItems()
    {
        foreach (var entry in _subscriptions.Values)
        {
            RecreateMonitoredItem(entry);
        }
    }

    private void RecreateMonitoredItem(SubscriptionEntry entry)
    {
        var item = new MonitoredItem(_opcSubscription!.DefaultItem)
        {
            StartNodeId = entry.NodeId,
            AttributeId = Attributes.Value,
            SamplingInterval = _settings.SamplingIntervalMs,
            QueueSize = _settings.QueueSize,
            DiscardOldest = true
        };
        item.Notification += OnDataChange;
        _opcSubscription.AddItem(item);
        entry.MonitoredItem = item;
    }
}
