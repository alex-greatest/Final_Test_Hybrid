namespace Final_Test_Hybrid.Services.OpcUa.Subscription;

public readonly record struct OpcUaCallbackStats(int NodeCount, int TotalCount);

public sealed record OpcUaSubscriptionDiagnosticsSnapshot(
    int MonitoredItemsCount,
    int CallbackNodeCount,
    int CallbackTotalCount,
    IReadOnlyList<string> NodeIds);

public partial class OpcUaSubscription
{
    public int GetMonitoredItemCount() => _monitoredItems.Count;

    public OpcUaCallbackStats GetCallbackStats()
    {
        lock (_callbacksLock)
        {
            var totalCount = _callbacks.Values.Sum(list => list.Count);
            return new OpcUaCallbackStats(_callbacks.Count, totalCount);
        }
    }

    public IReadOnlyList<string> GetMonitoredNodeIdsSnapshot()
    {
        return [.. _monitoredItems.Keys.OrderBy(nodeId => nodeId, StringComparer.OrdinalIgnoreCase)];
    }

    public OpcUaSubscriptionDiagnosticsSnapshot GetDiagnosticsSnapshot()
    {
        var callbackStats = GetCallbackStats();
        return new OpcUaSubscriptionDiagnosticsSnapshot(
            MonitoredItemsCount: _monitoredItems.Count,
            CallbackNodeCount: callbackStats.NodeCount,
            CallbackTotalCount: callbackStats.TotalCount,
            NodeIds: GetMonitoredNodeIdsSnapshot());
    }

    private void LogDiagnosticsForMonitoredChange(string operation, string nodeId)
    {
        if (!_diagnosticsSettings.Enabled)
        {
            return;
        }

        var callbackStats = GetCallbackStats();
        logger.LogInformation(
            "OPC monitored {Operation}: node={NodeId}, monitored={Monitored}, callbackNodes={CallbackNodes}, callbackTotal={CallbackTotal}",
            operation,
            nodeId,
            _monitoredItems.Count,
            callbackStats.NodeCount,
            callbackStats.TotalCount);
    }

    private void LogDiagnosticsForUnsubscribe(string nodeId, bool removeTag, bool callbackRemoved)
    {
        if (!_diagnosticsSettings.Enabled)
        {
            return;
        }

        var callbackStats = GetCallbackStats();
        logger.LogDebug(
            "OPC unsubscribe: node={NodeId}, callbackRemoved={CallbackRemoved}, removeTag={RemoveTag}, monitored={Monitored}, callbackNodes={CallbackNodes}, callbackTotal={CallbackTotal}",
            nodeId,
            callbackRemoved,
            removeTag,
            _monitoredItems.Count,
            callbackStats.NodeCount,
            callbackStats.TotalCount);
    }
}
