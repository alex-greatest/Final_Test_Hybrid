using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

#pragma warning disable CS0618 // Sync OPC UA methods are intentional for Blazor sync lifecycle

namespace Final_Test_Hybrid.Services.OpcUa;

public sealed partial class OpcUaSubscriptionService : IOpcUaSubscriptionService
{
    private readonly IOpcUaConnectionService _connectionService;
    private readonly ILogger<OpcUaSubscriptionService> _logger;
    private readonly OpcUaSubscriptionSettings _settings;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly Dictionary<string, SubscriptionEntry> _subscriptions = new();
    private readonly ReaderWriterLockSlim _callbackLock = new();
    private Dictionary<string, List<CallbackEntry>> _callbackSnapshot = new();
    private Subscription? _opcSubscription;
    private int _disposeState;
    private bool IsDisposed => Volatile.Read(ref _disposeState) != 0;
    public bool IsInitialized { get; private set; }

    public OpcUaSubscriptionService(
        IOpcUaConnectionService connectionService,
        IOptions<OpcUaSubscriptionSettings> settings,
        ILogger<OpcUaSubscriptionService> logger)
    {
        _connectionService = connectionService;
        _settings = settings.Value;
        _logger = logger;
        _connectionService.SessionRecreated += OnSessionRecreated;
    }

    public IDisposable Subscribe(string nodeId, Action<DataValue> onValueChanged)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _stateLock.Wait();
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return SubscribeCore(nodeId, onValueChanged);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public IDisposable Subscribe(IEnumerable<string> nodeIds, Action<string, DataValue> onValueChanged)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _stateLock.Wait();
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            var tokens = nodeIds.Select(nodeId => (IDisposable)SubscribeCore(nodeId, dv => onValueChanged(nodeId, dv))).ToList();
            return new CompositeDisposable(tokens);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private CallbackEntry SubscribeCore(string nodeId, Action<DataValue> onValueChanged)
    {
        var callbackEntry = new CallbackEntry
        {
            Id = Guid.NewGuid(),
            Callback = onValueChanged,
            NodeId = nodeId,
            Owner = this
        };
        if (!_subscriptions.TryGetValue(nodeId, out var entry))
        {
            entry = new SubscriptionEntry { NodeId = nodeId };
            _subscriptions[nodeId] = entry;
            if (IsInitialized)
            {
                CreateMonitoredItemForEntry(entry);
                _opcSubscription?.ApplyChanges();
            }
        }
        entry.Callbacks.Add(callbackEntry);
        UpdateCallbackSnapshot();
        return callbackEntry;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (IsInitialized)
            {
                return;
            }
            await CreateOpcSubscriptionAsync(cancellationToken);
            IsInitialized = true;
            _logger.LogInformation("Initialized {Count} OPC UA subscriptions", _subscriptions.Count);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task CreateOpcSubscriptionAsync(CancellationToken cancellationToken)
    {
        if (_subscriptions.Count == 0)
        {
            return;
        }
        await _connectionService.ExecuteWithSessionAsync(session =>
        {
            _opcSubscription = new Subscription(session.DefaultSubscription)
            {
                PublishingInterval = _settings.PublishingIntervalMs,
                LifetimeCount = 1000,
                KeepAliveCount = 10,
                MaxNotificationsPerPublish = 1000,
                PublishingEnabled = true
            };
            session.AddSubscription(_opcSubscription);
            _opcSubscription.Create();
            foreach (var entry in _subscriptions.Values)
            {
                CreateMonitoredItemForEntry(entry);
            }
            _opcSubscription.ApplyChanges();
            _logger.LogInformation("Created OPC UA Subscription with {Count} items", _subscriptions.Count);
            return Task.CompletedTask;
        }, cancellationToken);
    }

    private void CreateMonitoredItemForEntry(SubscriptionEntry entry)
    {
        if (_opcSubscription is null)
        {
            return;
        }
        var item = new MonitoredItem(_opcSubscription.DefaultItem)
        {
            StartNodeId = entry.NodeId,
            AttributeId = Attributes.Value,
            SamplingInterval = _settings.SamplingIntervalMs,
            QueueSize = _settings.QueueSize,
            DiscardOldest = true
        };
        item.Notification += OnDataChange;
        _opcSubscription.AddItem(item);
        entry.MonitoredItem = item;
        _logger.LogDebug("Created MonitoredItem for {NodeId}", entry.NodeId);
    }

    private void RemoveCallback(string nodeId, Guid callbackId)
    {
        _stateLock.Wait();
        try
        {
            RemoveCallbackCore(nodeId, callbackId);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private void RemoveCallbackCore(string nodeId, Guid callbackId)
    {
        if (!_subscriptions.TryGetValue(nodeId, out var entry))
        {
            return;
        }
        entry.Callbacks.RemoveAll(c => c.Id == callbackId);
        if (entry.Callbacks.Count == 0)
        {
            RemoveMonitoredItem(entry);
            _subscriptions.Remove(nodeId);
        }
        UpdateCallbackSnapshot();
    }

    private void RemoveMonitoredItem(SubscriptionEntry entry)
    {
        if (_opcSubscription is null || entry.MonitoredItem is null)
        {
            return;
        }
        entry.MonitoredItem.Notification -= OnDataChange;
        _opcSubscription.RemoveItem(entry.MonitoredItem);
        _opcSubscription.ApplyChanges();
        _logger.LogDebug("Removed MonitoredItem for {NodeId}", entry.NodeId);
    }

    private void UpdateCallbackSnapshot()
    {
        var snapshot = new Dictionary<string, List<CallbackEntry>>();
        foreach (var kvp in _subscriptions)
        {
            snapshot[kvp.Key] = kvp.Value.Callbacks.ToList();
        }
        _callbackLock.EnterWriteLock();
        try
        {
            _callbackSnapshot = snapshot;
        }
        finally
        {
            _callbackLock.ExitWriteLock();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }
        _connectionService.SessionRecreated -= OnSessionRecreated;
        await _stateLock.WaitAsync();
        try
        {
            ClearCallbackSnapshot();
            await DisposeOpcSubscriptionAsync();
            _subscriptions.Clear();
        }
        finally
        {
            _stateLock.Release();
        }
        await Task.Delay(50);
        _stateLock.Dispose();
        _callbackLock.Dispose();
        _logger.LogInformation("OpcUaSubscriptionService disposed");
    }

    private void ClearCallbackSnapshot()
    {
        _callbackLock.EnterWriteLock();
        try
        {
            _callbackSnapshot = new Dictionary<string, List<CallbackEntry>>();
        }
        finally
        {
            _callbackLock.ExitWriteLock();
        }
    }

    private async Task DisposeOpcSubscriptionAsync()
    {
        if (_opcSubscription is null)
        {
            return;
        }
        try
        {
            await _connectionService.ExecuteWithSessionAsync(session =>
            {
                session.RemoveSubscription(_opcSubscription);
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing OPC subscription");
        }
        _opcSubscription = null;
    }

    private sealed class SubscriptionEntry
    {
        public required string NodeId { get; init; }
        public MonitoredItem? MonitoredItem { get; set; }
        public List<CallbackEntry> Callbacks { get; } = [];
    }

    private sealed class CallbackEntry : IDisposable
    {
        public required Guid Id { get; init; }
        public required Action<DataValue> Callback { get; init; }
        public required string NodeId { get; init; }
        public required OpcUaSubscriptionService Owner { get; init; }
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            Owner.RemoveCallback(NodeId, Id);
        }
    }

    private sealed class CompositeDisposable(List<IDisposable> disposables) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            foreach (var d in disposables)
            {
                d.Dispose();
            }
        }
    }
}
