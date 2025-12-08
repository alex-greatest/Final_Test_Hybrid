using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa
{
    public class OpcUaSubscriptionService(
        IOpcUaConnectionService connectionService,
        IOptions<OpcUaSettings> settings,
        ILogger<OpcUaSubscriptionService> logger) : IOpcUaSubscriptionService
    {
        private readonly OpcUaSettings _settings = settings.Value;
        private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();

        [Obsolete("Obsolete")]
        public async Task SubscribeAsync(string nodeId, Action<object?> callback)
        {
            var session = connectionService.Session;
            if (session is not { Connected: true })
            {
                return;
            }
            if (_subscriptions.ContainsKey(nodeId))
            {
                await UnsubscribeAsync(nodeId);
            }
            try
            {
                var subscription = CreateSubscription(session);
                var monitoredItem = CreateMonitoredItem(nodeId, callback, subscription);
                await AddSubscriptionToSessionAsync(session, subscription, monitoredItem);
                _subscriptions[nodeId] = subscription;
                logger.LogInformation("Subscribed to node {NodeId}", nodeId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to subscribe to node {NodeId}", nodeId);
            }
        }

        [Obsolete("Obsolete")]
        public async Task UnsubscribeAsync(string nodeId)
        {
            if (!_subscriptions.TryRemove(nodeId, out var subscription))
            {
                return;
            }
            try
            {
                await RemoveSubscriptionFromSessionAsync(subscription);
                subscription.Dispose();
                logger.LogInformation("Unsubscribed from node {NodeId}", nodeId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to unsubscribe from node {NodeId}", nodeId);
            }
        }

        [Obsolete("Obsolete")]
        public async ValueTask DisposeAsync()
        {
            foreach (var subscription in _subscriptions.Values)
            {
                try
                {
                    await RemoveSubscriptionFromSessionAsync(subscription);
                    subscription.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error disposing subscription");
                }
            }
            _subscriptions.Clear();
            GC.SuppressFinalize(this);
        }

        private Subscription CreateSubscription(Session session)
        {
            return new Subscription(session.DefaultSubscription)
            {
                PublishingInterval = _settings.PublishingIntervalMs
            };
        }

        private MonitoredItem CreateMonitoredItem(string nodeId, Action<object?> callback, Subscription subscription)
        {
            var monitoredItem = new MonitoredItem(subscription.DefaultItem)
            {
                StartNodeId = new NodeId(nodeId),
                AttributeId = Attributes.Value
            };
            monitoredItem.Notification += (item, e) =>
            {
                var value = ((MonitoredItemNotification)e.NotificationValue).Value.Value;
                callback(value);
            };
            return monitoredItem;
        }

        private async Task AddSubscriptionToSessionAsync(Session session, Subscription subscription, MonitoredItem monitoredItem)
        {
            subscription.AddItem(monitoredItem);
            session.AddSubscription(subscription);
            await subscription.CreateAsync();
        }

        [Obsolete("Obsolete")]
        private async Task RemoveSubscriptionFromSessionAsync(Subscription subscription)
        {
            var session = connectionService.Session;
            if (session is not { Connected: true })
            {
                return;
            }
            await subscription.DeleteAsync(true);
            session.RemoveSubscription(subscription);
        }
    }
}
