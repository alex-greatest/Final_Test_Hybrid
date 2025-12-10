using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public sealed partial class OpcUaSubscriptionService
{
    private void OnDataChange(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }
        try
        {
            OnDataChangeCore(item, e);
        }
        catch (ObjectDisposedException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnDataChange for {NodeId}", item.StartNodeId);
        }
    }

    private void OnDataChangeCore(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        var nodeId = item.StartNodeId.ToString();
        List<CallbackEntry>? callbacks;
        _callbackLock.EnterReadLock();
        try
        {
            if (!_callbackSnapshot.TryGetValue(nodeId, out callbacks))
            {
                return;
            }
        }
        finally
        {
            _callbackLock.ExitReadLock();
        }
        foreach (var notification in item.DequeueValues())
        {
            InvokeCallbacksSafe(callbacks, notification);
        }
    }

    private void InvokeCallbacksSafe(List<CallbackEntry> callbacks, DataValue value)
    {
        foreach (var entry in callbacks)
        {
            InvokeSingleCallback(entry, value);
        }
    }

    private void InvokeSingleCallback(CallbackEntry entry, DataValue value)
    {
        try
        {
            entry.Callback(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking callback for {NodeId}", entry.NodeId);
        }
    }
}
