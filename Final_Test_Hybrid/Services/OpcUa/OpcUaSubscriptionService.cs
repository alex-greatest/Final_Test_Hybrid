using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa
{
    public class OpcUaSubscriptionService(IOpcUaConnectionService connectionService) : IOpcUaSubscriptionService
    {
        private readonly Dictionary<string, Subscription> _subscriptions = [];

        public async Task SubscribeAsync(string nodeId, Action<object?> callback)
        {
            if (!IsSessionConnected())
            {
                return;
            }
            var subscription = CreateSubscription();
            var monitoredItem = CreateMonitoredItem(nodeId, callback, subscription);
            await AddSubscriptionToSessionAsync(subscription, monitoredItem);
            _subscriptions[nodeId] = subscription;
        }

        public async Task UnsubscribeAsync(string nodeId)
        {
            if (!TryGetSubscription(nodeId, out var subscription))
            {
                return;
            }
            await RemoveSubscriptionFromSessionAsync(subscription);
            subscription.Dispose();
            _subscriptions.Remove(nodeId);
        }

        private bool IsSessionConnected()
        {
            var session = connectionService.Session;
            return session != null && session.Connected;
        }

        private Subscription CreateSubscription()
        {
            var session = connectionService.Session;
            return new Subscription(session.DefaultSubscription)
            {
                PublishingInterval = 1000
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

        private async Task AddSubscriptionToSessionAsync(Subscription subscription, MonitoredItem monitoredItem)
        {
            var session = connectionService.Session;
            subscription.AddItem(monitoredItem);
            session.AddSubscription(subscription);
            await subscription.CreateAsync();
        }

        private bool TryGetSubscription(string nodeId, out Subscription? subscription)
        {
            return _subscriptions.TryGetValue(nodeId, out subscription);
        }

        private async Task RemoveSubscriptionFromSessionAsync(Subscription subscription)
        {
            if (!IsSessionConnected())
            {
                return;
            }
            var session = connectionService.Session;
            await subscription.DeleteAsync(true);
            session.RemoveSubscription(subscription);
        }
    }
}
