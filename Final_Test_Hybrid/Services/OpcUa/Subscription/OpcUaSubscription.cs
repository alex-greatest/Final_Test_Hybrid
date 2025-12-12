using System.Collections.Concurrent;
using Final_Test_Hybrid.Models.Plc.Settings;
using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Services.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa.Subscription;

public class OpcUaSubscription(
    OpcUaConnectionState connectionState,
    IOptions<OpcUaSettings> settingsOptions,
    ILogger<OpcUaSubscription> logger)
{
    private readonly OpcUaSubscriptionSettings _settings = settingsOptions.Value.Subscription;
    private readonly ConcurrentDictionary<string, object?> _values = new();
    private readonly ConcurrentDictionary<string, List<Func<object?, Task>>> _callbacks = new();
    private readonly ConcurrentDictionary<string, MonitoredItem> _monitoredItems = new();
    private readonly SemaphoreSlim _subscriptionLock = new(1, 1);
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
        logger.LogInformation("Подписка OPC UA создана");
    }

    public async Task<TagError?> AddTagAsync(string nodeId, CancellationToken ct = default)
    {
        await connectionState.WaitForConnectionAsync(ct).ConfigureAwait(false);
        if (_monitoredItems.ContainsKey(nodeId))
        {
            return null;
        }
        return await RunAddTagAsync(nodeId, ct);
    }

    private async Task<TagError?> RunAddTagAsync(string nodeId, CancellationToken ct = default)
    {
        var item = CreateMonitoredItem(nodeId);
        if (!_monitoredItems.TryAdd(nodeId, item))
        {
            return null;
        }
        await using (await AsyncLock.AcquireAsync(_subscriptionLock, ct).ConfigureAwait(false))
        {
            _subscription!.AddItem(item);
            await _subscription.ApplyChangesAsync(ct).ConfigureAwait(false);
        }
        if (!ServiceResult.IsBad(item.Status.Error))
        {
            logger.LogInformation("Тег {NodeId} добавлен в подписку", nodeId);
            return null;
        }
        _monitoredItems.TryRemove(nodeId, out _);
        var message = OpcUaErrorMapper.ToHumanReadable(item.Status.Error.StatusCode);
        logger.LogWarning("Не удалось добавить тег {NodeId}: {Error}", nodeId, message);
        return new TagError(nodeId, message);
    }

    public async Task<IReadOnlyList<TagError>> AddTagsAsync(IEnumerable<string> nodeIds, CancellationToken ct = default)
    {
        List<MonitoredItem> newItems;
        await using (await AsyncLock.AcquireAsync(_subscriptionLock, ct).ConfigureAwait(false))
        {
            newItems = CreateNewMonitoredItems(nodeIds);
            if (newItems.Count == 0)
            {
                return [];
            }
            await _subscription!.ApplyChangesAsync(ct).ConfigureAwait(false);
        }
        return CollectErrors(newItems);
    }

    private List<MonitoredItem> CreateNewMonitoredItems(IEnumerable<string> nodeIds)
    {
        var newItems = new List<MonitoredItem>();
        foreach (var nodeId in nodeIds)
        {
            var item = CreateMonitoredItem(nodeId);
            if (!_monitoredItems.TryAdd(nodeId, item))
            {
                continue;
            }
            _subscription!.AddItem(item);
            newItems.Add(item);
        }
        return newItems;
    }

    private List<TagError> CollectErrors(List<MonitoredItem> items)
    {
        var errors = new List<TagError>();
        foreach (var item in items)
        {
            var nodeId = item.StartNodeId.ToString();
            if (!ServiceResult.IsBad(item.Status.Error))
            {
                logger.LogInformation("Тег {NodeId} добавлен в подписку", nodeId);
                continue;
            }
            _monitoredItems.TryRemove(nodeId, out _);
            var message = OpcUaErrorMapper.ToHumanReadable(item.Status.Error.StatusCode);
            logger.LogWarning("Не удалось добавить тег {NodeId}: {Error}", nodeId, message);
            errors.Add(new TagError(nodeId, message));
        }
        return errors;
    }

    public async Task SubscribeAsync(string nodeId, Func<object?, Task> callback, CancellationToken ct = default)
    {
        var error = await EnsureTagExistsAsync(nodeId, ct).ConfigureAwait(false);
        if (error != null)
        {
            return;
        }
        AddCallback(nodeId, callback);
    }

    private async Task<TagError?> EnsureTagExistsAsync(string nodeId, CancellationToken ct)
    {
        if (_monitoredItems.ContainsKey(nodeId))
        {
            return null;
        }
        return await AddTagAsync(nodeId, ct).ConfigureAwait(false);
    }

    private void AddCallback(string nodeId, Func<object?, Task> callback)
    {
        if (!_callbacks.TryGetValue(nodeId, out var list))
        {
            _callbacks[nodeId] = list = [];
        }
        list.Add(callback);
    }

    public async Task UnsubscribeAsync(
        string nodeId,
        Func<object?, Task> callback,
        bool removeTag = false,
        CancellationToken ct = default)
    {
        if (!_callbacks.TryGetValue(nodeId, out var list))
        {
            return;
        }
        list.Remove(callback);
        if (list.Count > 0 || !removeTag)
        {
            return;
        }
        _callbacks.TryRemove(nodeId, out _);
        await RemoveTagAsync(nodeId, ct).ConfigureAwait(false);
    }

    public async Task RemoveTagAsync(string nodeId, CancellationToken ct = default)
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
        _callbacks.TryRemove(nodeId, out _);
    }

    public object? GetValue(string nodeId) => _values.GetValueOrDefault(nodeId);

    public T? GetValue<T>(string nodeId)
    {
        var value = _values.GetValueOrDefault(nodeId);
        return value is T typed ? typed : default;
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
        var value = notification.Value?.Value;
        _values[nodeId] = value;
        logger.LogDebug("Тег {NodeId} = {Value}", nodeId, value);
        if (!_callbacks.TryGetValue(nodeId, out var list))
        {
            return;
        }
        foreach (var callback in list)
        {
            _ = callback(value);
        }
    }
}
