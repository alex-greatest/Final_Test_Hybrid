using Final_Test_Hybrid.Models.Plc.Settings;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public partial class OpcUaSubscriptionService
{
    private async Task CreateSubscriptionAsync(ISession session, List<string> nodeIds, CancellationToken ct)
    {
        if (!IsSessionAvailable(session))
        {
            _logger.LogWarning("Невозможно создать подписку: сессия недоступна");
            return;
        }
        await CreateSubscribe(session, nodeIds, ct);
    }

    private async Task CreateSubscribe(ISession session, List<string> nodeIds, CancellationToken ct)
    {
        var subscriptionSettings = _settings.Subscription;
        _subscription = new Subscription(session.DefaultSubscription)
        {
            PublishingEnabled = true,
            PublishingInterval = subscriptionSettings.PublishingIntervalMs,
            KeepAliveCount = 10,
            LifetimeCount = 100,
            MaxNotificationsPerPublish = 0
        };
        session.AddSubscription(_subscription);
        await _subscription.CreateAsync(ct).ConfigureAwait(false);
        _logger.LogInformation(
            "OPC UA Subscription создана: PublishingInterval={Interval}ms",
            subscriptionSettings.PublishingIntervalMs);
        await AddMonitoredItemsAsync(nodeIds, subscriptionSettings, ct).ConfigureAwait(false);
    }

    private async Task AddMonitoredItemsAsync(List<string> nodeIds, OpcUaSubscriptionSettings subscriptionSettings, CancellationToken ct)
    {
        var monitoredItems = new List<MonitoredItem>();
        foreach (var nodeId in nodeIds)
        {
            var item = new MonitoredItem(_subscription!.DefaultItem)
            {
                StartNodeId = new NodeId(nodeId),
                AttributeId = Attributes.Value,
                MonitoringMode = MonitoringMode.Reporting,
                SamplingInterval = subscriptionSettings.SamplingIntervalMs,
                QueueSize = (uint)subscriptionSettings.QueueSize,
                DiscardOldest = true
            };
            item.Notification += OnMonitoredItemNotification;
            monitoredItems.Add(item);
            _logger.LogInformation("Подписка на узел {NodeId} создана", nodeId);
        }
        _subscription!.AddItems(monitoredItems);
        await _subscription.ApplyChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Добавлено {Count} MonitoredItems", monitoredItems.Count);
    }

    private async Task RemoveSubscriptionAsync()
    {
        if (_subscription == null)
        {
            return;
        }
        try
        {
            UnsubscribeFromNotifications();
            await DeleteFromServerAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении подписки");
        }
        finally
        {
            DisposeSubscription();
        }
    }

    private void UnsubscribeFromNotifications()
    {
        foreach (var item in _subscription!.MonitoredItems)
        {
            item.Notification -= OnMonitoredItemNotification;
        }
    }

    private async Task DeleteFromServerAsync()
    {
        if (!IsSessionAvailable(_connectionService.Session))
        {
            return;
        }
        await _connectionService.Session!.RemoveSubscriptionAsync(_subscription!).ConfigureAwait(false);
        await _subscription!.DeleteAsync(true).ConfigureAwait(false);
    }

    private void DisposeSubscription()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private static bool IsSessionAvailable(ISession? session)
    {
        return session is { Connected: true };
    }
}
