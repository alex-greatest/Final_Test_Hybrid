using System.Collections.Concurrent;
using AsyncAwaitBestPractices;
using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Settings.OpcUa;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa.Subscription;

public readonly record struct OpcUaSubscriptionDebugSnapshot(
    bool HasInternalSubscription,
    int MonitoredItemsCount,
    int NodeIdsWithCallbacksCount,
    int TotalCallbacksCount,
    DateTimeOffset? LastNotificationAtUtc);

public class OpcUaSubscription(
    OpcUaConnectionState connectionState,
    IOptions<OpcUaSettings> settingsOptions,
    DualLogger<OpcUaSubscription> logger)
{
    private readonly OpcUaSubscriptionSettings _settings = settingsOptions.Value.Subscription;
    private readonly ConcurrentDictionary<string, object?> _values = new();
    private readonly Dictionary<string, List<Func<object?, Task>>> _callbacks = new();
    private readonly Lock _callbacksLock = new();
    private readonly ConcurrentDictionary<string, MonitoredItem> _monitoredItems = new();
    private readonly SemaphoreSlim _subscriptionLock = new(1, 1);
    private Opc.Ua.Client.Subscription? _subscription;
    private long _lastNotificationUnixTimeMilliseconds;

    public async Task CreateAsync(ISession session, CancellationToken ct = default)
    {
        _subscription = new Opc.Ua.Client.Subscription(session.DefaultSubscription)
        {
            DisplayName = "OpcUa Subscription",
            PublishingEnabled = true,
            PublishingInterval = _settings.PublishingIntervalMs,
            KeepAliveCount = _settings.KeepAliveCount,
            LifetimeCount = _settings.LifetimeCount,
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
            item.Notification -= OnNotification;
            return null;
        }
        await ApplyItemToSubscriptionAsync(item, ct).ConfigureAwait(false);
        return ProcessAddResult(item, nodeId);
    }

    private async Task ApplyItemToSubscriptionAsync(MonitoredItem item, CancellationToken ct)
    {
        await using (await AsyncLock.AcquireAsync(_subscriptionLock, ct).ConfigureAwait(false))
        {
            _subscription!.AddItem(item);
            await _subscription.ApplyChangesAsync(ct).ConfigureAwait(false);
        }
    }

    private TagError? ProcessAddResult(MonitoredItem item, string nodeId)
    {
        if (!ServiceResult.IsBad(item.Status.Error))
        {
            logger.LogInformation("Тег {NodeId} добавлен в подписку", nodeId);
            return null;
        }
        _monitoredItems.TryRemove(nodeId, out _);
        item.Notification -= OnNotification;
        var message = OpcUaErrorMapper.ToHumanReadable(item.Status.Error.StatusCode);
        logger.LogError("Не удалось добавить тег {NodeId}: {Error}", nodeId, message);
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
                item.Notification -= OnNotification;
                continue;
            }
            _subscription!.AddItem(item);
            newItems.Add(item);
        }
        return newItems;
    }

    private List<TagError> CollectErrors(List<MonitoredItem> items) =>
        items
            .Select(item => ProcessAddResult(item, item.StartNodeId.ToString()))
            .Where(error => error != null)
            .ToList()!;

    public OpcUaSubscriptionDebugSnapshot GetDebugSnapshot()
    {
        var nodeIdsWithCallbacksCount = 0;
        var totalCallbacksCount = 0;

        lock (_callbacksLock)
        {
            foreach (var callbacks in _callbacks.Values)
            {
                var count = callbacks.Count;
                if (count <= 0)
                {
                    continue;
                }

                nodeIdsWithCallbacksCount++;
                totalCallbacksCount += count;
            }
        }

        var lastNotificationMs = Interlocked.Read(ref _lastNotificationUnixTimeMilliseconds);
        DateTimeOffset? lastNotificationAtUtc = lastNotificationMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(lastNotificationMs)
            : null;

        return new OpcUaSubscriptionDebugSnapshot(
            HasInternalSubscription: _subscription != null,
            MonitoredItemsCount: _monitoredItems.Count,
            NodeIdsWithCallbacksCount: nodeIdsWithCallbacksCount,
            TotalCallbacksCount: totalCallbacksCount,
            LastNotificationAtUtc: lastNotificationAtUtc);
    }

    public async Task SubscribeAsync(
        string nodeId,
        Func<object?, Task> callback,
        CancellationToken ct = default,
        bool emitCachedValueImmediately = true)
    {
        var error = await EnsureTagExistsAsync(nodeId, ct).ConfigureAwait(false);
        if (error != null)
        {
            throw new InvalidOperationException($"Не удалось подписаться на тег {nodeId}: {error.Message}");
        }
        AddCallback(nodeId, callback);

        // Push cached value to late subscribers (e.g., dialogs opened after the main screen).
        if (emitCachedValueImmediately && _values.TryGetValue(nodeId, out var cachedValue))
        {
            callback(cachedValue).SafeFireAndForget(ex =>
                logger.LogError(ex, "Ошибка в callback для тега {NodeId}", nodeId));
        }
    }

    private Task<TagError?> EnsureTagExistsAsync(string nodeId, CancellationToken ct) =>
        _monitoredItems.ContainsKey(nodeId)
            ? Task.FromResult<TagError?>(null)
            : AddTagAsync(nodeId, ct);

    private void AddCallback(string nodeId, Func<object?, Task> callback)
    {
        lock (_callbacksLock)
        {
            if (!_callbacks.TryGetValue(nodeId, out var list))
            {
                _callbacks[nodeId] = list = [];
            }
            list.Add(callback);
            logger.LogInformation("AddCallback: {NodeId}, total callbacks: {Count}", nodeId, list.Count);
        }
    }

    public async Task UnsubscribeAsync(
        string nodeId,
        Func<object?, Task> callback,
        bool removeTag = false,
        CancellationToken ct = default)
    {
        if (!TryRemoveCallback(nodeId, callback))
        {
            return;
        }
        await TryRemoveTagIfEmptyAsync(nodeId, removeTag, ct).ConfigureAwait(false);
    }

    private bool TryRemoveCallback(string nodeId, Func<object?, Task> callback)
    {
        lock (_callbacksLock)
        {
            if (!_callbacks.TryGetValue(nodeId, out var list))
            {
                return false;
            }
            list.Remove(callback);
            return true;
        }
    }

    private async Task TryRemoveTagIfEmptyAsync(string nodeId, bool removeTag, CancellationToken ct)
    {
        if (!removeTag)
        {
            return;
        }
        bool shouldRemove;
        lock (_callbacksLock)
        {
            shouldRemove = _callbacks.TryGetValue(nodeId, out var list) && list.Count == 0;
            if (shouldRemove)
            {
                _callbacks.Remove(nodeId);
            }
        }
        if (shouldRemove)
        {
            await RemoveTagAsync(nodeId, ct).ConfigureAwait(false);
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
    }

    public object? GetValue(string nodeId) => _values.GetValueOrDefault(nodeId);

    public T? GetValue<T>(string nodeId) =>
        _values.GetValueOrDefault(nodeId) is T typed ? typed : default;

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
        Interlocked.Exchange(ref _lastNotificationUnixTimeMilliseconds, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var nodeId = item.StartNodeId.ToString();
        var value = notification.Value?.Value;
        StoreAndLogValue(nodeId, value);
        InvokeCallbacks(nodeId, value);
    }

    private void StoreAndLogValue(string nodeId, object? value)
    {
        _values[nodeId] = value;
        logger.LogInformation("Тег {NodeId} = {Value}", nodeId, value);
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
