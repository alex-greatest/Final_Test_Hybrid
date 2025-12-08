using System.Collections.Concurrent;
using Final_Test_Hybrid.Services.OpcUa.Interface;
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
        private readonly ConcurrentDictionary<string, Action<object?>> _callbacks = new();
        private readonly Lock _connectionEventsLock = new();
        private bool _connectionEventsAttached;

        public OpcUaSubscriptionService(
            IOpcUaConnectionService connectionService,
            IOptions<OpcUaSettings> settings,
            ILogger<OpcUaSubscriptionService> logger,
            bool subscribeToConnection = true)
            : this(connectionService, settings, logger)
        {
            if (subscribeToConnection)
            {
                connectionService.ConnectionChanged += OnConnectionChanged;
                _connectionEventsAttached = true;
            }
        }

        public async Task SubscribeAsync(string nodeId, Action<object?> callback)
        {
            EnsureConnectionEventsAttached();
            _callbacks[nodeId] = callback;
            var session = connectionService.Session;
            if (session is not { Connected: true })
            {
                return;
            }
            await RemoveExistingSubscriptionAsync(nodeId);
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

        public async Task UnsubscribeAsync(string nodeId)
        {
            _callbacks.TryRemove(nodeId, out _);
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

        public async ValueTask DisposeAsync()
        {
            DetachConnectionEvents();
            foreach (var subscription in _subscriptions.Values.ToArray())
            {
                await SafeRemoveSubscriptionAsync(subscription);
            }
            _subscriptions.Clear();
            _callbacks.Clear();
        }

        private async Task SafeRemoveSubscriptionAsync(Subscription subscription)
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
                try
                {
                    var value = ((MonitoredItemNotification)e.NotificationValue).Value.Value;
                    callback(value);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Callback error for node {NodeId}", nodeId);
                }
            };
            return monitoredItem;
        }

        private async Task AddSubscriptionToSessionAsync(Session session, Subscription subscription, MonitoredItem monitoredItem)
        {
            subscription.AddItem(monitoredItem);
            session.AddSubscription(subscription);
            await subscription.CreateAsync();
        }

        private async Task RemoveSubscriptionFromSessionAsync(Subscription subscription)
        {
            if (subscription.Session is not { Connected: true } session)
            {
                return;
            }
            await subscription.DeleteAsync(true);
            await session.RemoveSubscriptionAsync(subscription);
        }

        private async Task RemoveExistingSubscriptionAsync(string nodeId)
        {
            if (!_subscriptions.TryRemove(nodeId, out var existing))
            {
                return;
            }
            await RemoveSubscriptionFromSessionAsync(existing);
            existing.Dispose();
        }

        private void EnsureConnectionEventsAttached()
        {
            lock (_connectionEventsLock)
            {
                if (_connectionEventsAttached)
                {
                    return;
                }
                connectionService.ConnectionChanged += OnConnectionChanged;
                _connectionEventsAttached = true;
            }
        }

        private void DetachConnectionEvents()
        {
            lock (_connectionEventsLock)
            {
                if (!_connectionEventsAttached)
                {
                    return;
                }
                connectionService.ConnectionChanged -= OnConnectionChanged;
                _connectionEventsAttached = false;
            }
        }

        private void OnConnectionChanged(object? sender, bool connected)
        {
            _ = HandleConnectionChangeAsync(connected);
        }

        private async Task HandleConnectionChangeAsync(bool connected)
        {
            try
            {
                if (connected)
                {
                    await ResubscribeAllAsync();
                }
                else
                {
                    await DropAllSubscriptionsAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling connection change (connected={Connected})", connected);
            }
        }

        private async Task ResubscribeAllAsync()
        {
            if (connectionService.Session is not { Connected: true } session)
            {
                return;
            }
            foreach (var kvp in _callbacks.ToArray())
            {
                await SubscribeInternalAsync(kvp.Key, kvp.Value, session);
            }
        }

        private async Task SubscribeInternalAsync(string nodeId, Action<object?> callback, Session session)
        {
            await RemoveExistingSubscriptionAsync(nodeId);
            var subscription = CreateSubscription(session);
            var monitoredItem = CreateMonitoredItem(nodeId, callback, subscription);
            await AddSubscriptionToSessionAsync(session, subscription, monitoredItem);
            _subscriptions[nodeId] = subscription;
            logger.LogInformation("Subscribed to node {NodeId}", nodeId);
        }

        private async Task DropAllSubscriptionsAsync()
        {
            foreach (var subscription in _subscriptions.Values.ToArray())
            {
                await SafeRemoveSubscriptionAsync(subscription);
            }
            _subscriptions.Clear();
        }
    }
}