using Final_Test_Hybrid.Models.Plc.Settings;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa.Subscription;

public class OpcUaSubscription(IOptions<OpcUaSettings> settingsOptions)
{
    private readonly OpcUaSubscriptionSettings _settings = settingsOptions.Value.Subscription;
    private readonly Dictionary<string, List<Action<object?>>> _callbacks = new();
    private readonly Dictionary<string, MonitoredItem> _monitoredItems = new();
    private Opc.Ua.Client.Subscription? _subscription;

    public async Task CreateAsync(ISession session, CancellationToken ct = default)
    {
        _subscription = new Opc.Ua.Client.Subscription(session.DefaultSubscription)
        {
            DisplayName = "OpcUa Subscription",
            PublishingEnabled = true,
            PublishingInterval = _settings.PublishingIntervalMs,
            KeepAliveCount = 10,
            LifetimeCount = 100,
            MaxNotificationsPerPublish = _settings.MaxNotificationsPerPublish
        };
        session.AddSubscription(_subscription);
        await _subscription.CreateAsync(ct).ConfigureAwait(false);
    }

    public async Task SubscribeAsync(string nodeId, Action<object?> callback, CancellationToken ct = default)
    {
        if (_subscription == null)
        {
            throw new InvalidOperationException("Subscription not created. Call CreateAsync first.");
        }
        if (_callbacks.TryGetValue(nodeId, out var list))
        {
            list.Add(callback);
            return;
        }
        _callbacks[nodeId] = [callback];
        var item = CreateMonitoredItem(nodeId);
        _monitoredItems[nodeId] = item;
        _subscription.AddItem(item);
        await _subscription.ApplyChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UnsubscribeAsync(string nodeId, Action<object?> callback, CancellationToken ct = default)
    {
        if (!_callbacks.TryGetValue(nodeId, out var list))
        {
            return;
        }
        list.Remove(callback);
        if (list.Count > 0)
        {
            return;
        }
        _callbacks.Remove(nodeId);
        await RemoveMonitoredItemAsync(nodeId, ct).ConfigureAwait(false);
    }

    private async Task RemoveMonitoredItemAsync(string nodeId, CancellationToken ct)
    {
        if (!_monitoredItems.Remove(nodeId, out var item))
        {
            return;
        }
        item.Notification -= OnNotification;
        _subscription!.RemoveItem(item);
        await _subscription.ApplyChangesAsync(ct).ConfigureAwait(false);
    }

    private MonitoredItem CreateMonitoredItem(string nodeId)
    {
        var item = new MonitoredItem(_subscription!.DefaultItem)
        {
            StartNodeId = new NodeId(nodeId),
            AttributeId = Attributes.Value,
            DisplayName = nodeId,
            SamplingInterval = _settings.SamplingIntervalMs,
            QueueSize = (uint)_settings.QueueSize,
            DiscardOldest = true
        };
        item.Notification += OnNotification;
        return item;
    }

    private void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        if (e.NotificationValue is not MonitoredItemNotification notification)
        {
            return;
        }
        var nodeId = item.StartNodeId.ToString();
        if (!_callbacks.TryGetValue(nodeId, out var list))
        {
            return;
        }
        var value = notification.Value?.Value;
        foreach (var callback in list)
        {
            callback(value);
        }
    }
}
