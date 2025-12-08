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
        private readonly SemaphoreSlim _connectionChangeGate = new(1, 1);
        private readonly CancellationTokenSource _disposeCts = new();
        private bool _connectionEventsAttached;
        private bool _disposed;

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
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureConnectionEventsAttached();

            await _connectionChangeGate.WaitAsync();
            try
            {
                _callbacks[nodeId] = callback;
                var session = connectionService.Session;
                if (session is not { Connected: true })
                {
                    return;
                }
                await RemoveExistingSubscriptionAsync(nodeId);
                await CreateAndAddSubscriptionAsync(nodeId, callback, session);
            }
            finally
            {
                _connectionChangeGate.Release();
            }
        }

        private async Task CreateAndAddSubscriptionAsync(string nodeId, Action<object?> callback, Session session)
        {
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
            await _connectionChangeGate.WaitAsync();
            try
            {
                _callbacks.TryRemove(nodeId, out _);
                if (!_subscriptions.TryRemove(nodeId, out var subscription))
                {
                    return;
                }
                await RemoveSubscriptionSafelyAsync(nodeId, subscription);
            }
            finally
            {
                _connectionChangeGate.Release();
            }
        }

        private async Task RemoveSubscriptionSafelyAsync(string nodeId, Subscription subscription)
        {
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
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            logger.LogInformation("Disposing OpcUaSubscriptionService with {Count} active subscriptions", _subscriptions.Count);
            await _disposeCts.CancelAsync();
            DetachConnectionEvents();
            await _connectionChangeGate.WaitAsync();
            try
            {
                await DisposeAllSubscriptionsAsync();
            }
            finally
            {
                _connectionChangeGate.Release();
                _connectionChangeGate.Dispose();
                _disposeCts.Dispose();
            }
        }

        private async Task DisposeAllSubscriptionsAsync(bool clearCallbacks = true)
        {
            foreach (var subscription in _subscriptions.Values.ToArray())
            {
                await SafeRemoveSubscriptionAsync(subscription);
            }
            _subscriptions.Clear();
            if (clearCallbacks)
            {
                _callbacks.Clear();
            }
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
                    if (e.NotificationValue is not MonitoredItemNotification notification)
                    {
                        return;
                    }
                    if (notification.Value == null)
                    {
                        logger.LogWarning("Received null DataValue for node {NodeId}", nodeId);
                        return;
                    }
                    callback(notification.Value.Value);
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
            try
            {
                await subscription.CreateAsync();
            }
            catch
            {
                await session.RemoveSubscriptionAsync(subscription);
                subscription.Dispose();
                throw;
            }
        }

        private async Task RemoveSubscriptionFromSessionAsync(Subscription subscription)
        {
            if (subscription.Session is not { Connected: true })
            {
                return;
            }
            await subscription.DeleteAsync(true); // true = removes from session automatically
        }

        private async Task RemoveExistingSubscriptionAsync(string nodeId)
        {
            if (!_subscriptions.TryGetValue(nodeId, out var existing))
            {
                return;
            }
            try
            {
                await RemoveSubscriptionFromSessionAsync(existing);
                existing.Dispose();
                _subscriptions.TryRemove(nodeId, out _);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to remove existing subscription for node {NodeId}", nodeId);
            }
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
            => _ = OnConnectionChangedAsync(connected);

        private async Task OnConnectionChangedAsync(bool connected)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                logger.LogDebug("Connection state changed: Connected={Connected}, ActiveSubscriptions={Count}", connected, _callbacks.Count);
                await HandleConnectionChangeSafelyAsync(connected);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation during dispose
            }
            catch (ObjectDisposedException)
            {
                // Service was disposed during event handling
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in connection change handler");
            }
        }

        private async Task HandleConnectionChangeSafelyAsync(bool connected)
        {
            await _connectionChangeGate.WaitAsync(_disposeCts.Token);
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
            catch (ObjectDisposedException)
            {
                logger.LogDebug("Connection change handling aborted: service was disposed during operation");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling connection change. Connected={Connected}, SubscriptionCount={Count}", connected, _subscriptions.Count);
            }
            finally
            {
                _connectionChangeGate.Release();
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
                await RemoveExistingSubscriptionAsync(kvp.Key);
                await CreateAndAddSubscriptionAsync(kvp.Key, kvp.Value, session);
            }
        }

        private async Task DropAllSubscriptionsAsync()
        {
            await DisposeAllSubscriptionsAsync(clearCallbacks: false);
        }
    }
}