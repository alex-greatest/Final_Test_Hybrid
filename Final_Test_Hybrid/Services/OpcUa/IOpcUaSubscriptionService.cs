using Opc.Ua;

namespace Final_Test_Hybrid.Services.OpcUa;

public interface IOpcUaSubscriptionService : IAsyncDisposable
{
    /// <summary>
    /// Indicates whether the service has been initialized with OPC UA server.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Subscribe to value changes of a single node.
    /// Callback is invoked on OPC UA SDK thread (not UI thread).
    /// Before InitializeAsync() is called, subscriptions are queued.
    /// After InitializeAsync(), new subscriptions are created immediately.
    /// </summary>
    /// <param name="nodeId">OPC UA node identifier (e.g., "ns=2;s=Temperature")</param>
    /// <param name="onValueChanged">Callback invoked when value changes</param>
    /// <returns>IDisposable token for unsubscribing</returns>
    IDisposable Subscribe(string nodeId, Action<DataValue> onValueChanged);

    /// <summary>
    /// Subscribe to value changes of multiple nodes with a single callback.
    /// </summary>
    /// <param name="nodeIds">Collection of OPC UA node identifiers</param>
    /// <param name="onValueChanged">Callback invoked with nodeId and new value</param>
    /// <returns>IDisposable token for unsubscribing from all nodes</returns>
    IDisposable Subscribe(IEnumerable<string> nodeIds, Action<string, DataValue> onValueChanged);

    /// <summary>
    /// Initialize all queued subscriptions with OPC UA server.
    /// Creates OPC UA Subscription and MonitoredItems for all registered nodes in a single call.
    /// This is more efficient than creating items one by one (1 round-trip vs N round-trips).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
