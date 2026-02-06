using AsyncAwaitBestPractices;
using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Services.Common;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa.Subscription;

public partial class OpcUaSubscription
{
    public async Task SubscribeAsync(string nodeId, Func<object?, Task> callback, CancellationToken ct = default)
    {
        var error = await EnsureTagExistsAsync(nodeId, ct).ConfigureAwait(false);
        if (error != null)
        {
            throw new InvalidOperationException($"Не удалось подписаться на тег {nodeId}: {error.Message}");
        }
        AddCallback(nodeId, callback);
    }

    private Task<TagError?> EnsureTagExistsAsync(string nodeId, CancellationToken ct)
    {
        return _monitoredItems.ContainsKey(nodeId)
            ? Task.FromResult<TagError?>(null)
            : AddTagAsync(nodeId, ct);
    }

    private void AddCallback(string nodeId, Func<object?, Task> callback)
    {
        lock (_callbacksLock)
        {
            if (!_callbacks.TryGetValue(nodeId, out var list))
            {
                _callbacks[nodeId] = list = [];
            }
            if (!list.Contains(callback))
            {
                list.Add(callback);
            }
        }
    }

    public async Task UnsubscribeAsync(
        string nodeId,
        Func<object?, Task> callback,
        bool removeTag = false,
        CancellationToken ct = default)
    {
        var callbackRemoved = TryRemoveCallback(nodeId, callback);
        if (!callbackRemoved)
        {
            return;
        }
        await TryRemoveTagIfEmptyAsync(nodeId, removeTag, ct).ConfigureAwait(false);
        LogDiagnosticsForUnsubscribe(nodeId, removeTag, callbackRemoved);
    }

    private bool TryRemoveCallback(string nodeId, Func<object?, Task> callback)
    {
        lock (_callbacksLock)
        {
            if (!_callbacks.TryGetValue(nodeId, out var list))
            {
                return false;
            }
            var removed = list.Remove(callback);
            if (list.Count == 0)
            {
                _callbacks.Remove(nodeId);
            }
            return removed;
        }
    }

    private async Task TryRemoveTagIfEmptyAsync(string nodeId, bool removeTag, CancellationToken ct)
    {
        if (!removeTag)
        {
            return;
        }
        if (HasActiveCallbacks(nodeId))
        {
            return;
        }
        await RemoveTagAsync(nodeId, ct).ConfigureAwait(false);
    }

    private bool HasActiveCallbacks(string nodeId)
    {
        lock (_callbacksLock)
        {
            return _callbacks.TryGetValue(nodeId, out var list) && list.Count > 0;
        }
    }

    private async Task RemoveTagAsync(string nodeId, CancellationToken ct = default)
    {
        if (!_monitoredItems.TryRemove(nodeId, out var item))
        {
            return;
        }
        item.Notification -= OnNotification;
        await using (await AsyncLock.AcquireAsync(_subscriptionLock, ct).ConfigureAwait(false))
        {
            _subscription!.RemoveItem(item);
            await _subscription.ApplyChangesAsync(ct).ConfigureAwait(false);
        }
        _values.TryRemove(nodeId, out _);
        lock (_callbacksLock)
        {
            _callbacks.Remove(nodeId);
        }
        LogDiagnosticsForMonitoredChange("remove", nodeId);
    }

    public object? GetValue(string nodeId) => _values.GetValueOrDefault(nodeId);

    public T? GetValue<T>(string nodeId) =>
        _values.GetValueOrDefault(nodeId) is T typed ? typed : default;

    private Opc.Ua.Client.Subscription CreateSubscription(ISession session)
    {
        return new Opc.Ua.Client.Subscription(session.DefaultSubscription)
        {
            DisplayName = "OpcUa Subscription",
            PublishingEnabled = true,
            PublishingInterval = _settings.PublishingIntervalMs,
            KeepAliveCount = _settings.KeepAliveCount,
            LifetimeCount = _settings.LifetimeCount,
            MaxNotificationsPerPublish = _settings.MaxNotificationsPerPublish
        };
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

        if (notification.Value == null || StatusCode.IsBad(notification.Value.StatusCode))
        {
            var failedNodeId = item.StartNodeId.ToString();
            _values.TryRemove(failedNodeId, out _);
            logger.LogDebug("Игнорируем bad quality для тега {NodeId}", failedNodeId);
            return;
        }

        var nodeId = item.StartNodeId.ToString();
        var value = notification.Value?.Value;
        _values[nodeId] = value;
        InvokeCallbacks(nodeId, value);
    }

    private void InvokeCallbacks(string nodeId, object? value)
    {
        Func<object?, Task>[] callbacks;
        lock (_callbacksLock)
        {
            if (!_callbacks.TryGetValue(nodeId, out var list))
            {
                return;
            }
            callbacks = [.. list];
        }
        foreach (var callback in callbacks)
        {
            callback(value).SafeFireAndForget(ex =>
                logger.LogError(ex, "Ошибка в callback для тега {NodeId}", nodeId));
        }
    }
}
