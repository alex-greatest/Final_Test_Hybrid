using Final_Test_Hybrid.Models.Plc;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa;

public partial class OpcUaSubscriptionService
{
    public void Subscribe(string nodeId, Action<OpcValue> callback)
    {
        lock (_subscribersLock)
        {
            if (!_subscribers.TryGetValue(nodeId, out var list))
            {
                list = [];
                _subscribers[nodeId] = list;
            }
            list.Add(callback);
        }
    }

    public void Unsubscribe(string nodeId, Action<OpcValue> callback)
    {
        lock (_subscribersLock)
        {
            if (!_subscribers.TryGetValue(nodeId, out var list))
            {
                return;
            }
            list.Remove(callback);
            if (list.Count == 0)
            {
                _subscribers.TryRemove(nodeId, out _);
            }
        }
    }

    private void NotifySubscribers(string nodeId, OpcValue value)
    {
        List<Action<OpcValue>> callbacks;
        lock (_subscribersLock)
        {
            if (!_subscribers.TryGetValue(nodeId, out var list))
            {
                return;
            }
            callbacks = list.ToList();
        }
        foreach (var callback in callbacks)
        {
            try
            {
                callback(value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в callback подписчика для {NodeId}", nodeId);
            }
        }
    }
}
