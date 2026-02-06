using System.Collections.Concurrent;
using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Settings.OpcUa;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa.Subscription;

/// <summary>
/// Runtime-кэш значений и callbacks OPC тегов.
/// После reconnect runtime monitored items пересоздаются в новом Session без накопления дублей.
/// </summary>
public partial class OpcUaSubscription(
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

    public void InvalidateValuesCache() => _values.Clear();

    public async Task CreateAsync(ISession session, CancellationToken ct = default)
    {
        _subscription = CreateSubscription(session);
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
        return await RunAddTagAsync(nodeId, ct).ConfigureAwait(false);
    }

    private async Task<TagError?> RunAddTagAsync(string nodeId, CancellationToken ct = default)
    {
        var item = CreateMonitoredItem(nodeId);
        if (!_monitoredItems.TryAdd(nodeId, item))
        {
            item.Notification -= OnNotification;
            return null;
        }
        try
        {
            await ApplyItemToSubscriptionAsync(item, ct).ConfigureAwait(false);
        }
        catch
        {
            RollbackFailedAddTag(nodeId, item);
            throw;
        }
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

    private void RollbackFailedAddTag(string nodeId, MonitoredItem item)
    {
        _monitoredItems.TryRemove(nodeId, out _);
        _values.TryRemove(nodeId, out _);
        item.Notification -= OnNotification;
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

    private List<TagError> CollectErrors(List<MonitoredItem> items)
    {
        return items
            .Select(item => ProcessAddResult(item, item.StartNodeId.ToString()))
            .Where(error => error != null)
            .ToList()!;
    }
}

