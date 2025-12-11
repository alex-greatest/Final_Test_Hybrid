using Final_Test_Hybrid.Models.Plc.Settings;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa.Subscription;

public class OpcUaSubscription(IOptions<OpcUaSettings> settingsOptions)
{
    private readonly OpcUaSubscriptionSettings _settings = settingsOptions.Value.Subscription;
    private Opc.Ua.Client.Subscription? _subscription;
    public event Action<string, object?>? DataChanged;

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

    public async Task AddMonitoredItemsAsync(IEnumerable<string> nodeIds, CancellationToken ct = default)
    {
        if (_subscription == null)
        {
            throw new InvalidOperationException("Subscription not created. Call CreateAsync first.");
        }
        var items = nodeIds.Select(CreateMonitoredItem).ToList();
        _subscription.AddItems(items);
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
        DataChanged?.Invoke(item.StartNodeId.ToString(), notification.Value?.Value);
    }
}