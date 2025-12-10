using Final_Test_Hybrid.Models.Plc;
using Final_Test_Hybrid.Models.Plc.Settings;
using Final_Test_Hybrid.Services.OpcUa.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public sealed class OpcUaSubscriptionService : IAsyncDisposable
{
    private readonly OpcUaConnectionService _connectionService;
    private readonly ConnectionAwaiter _connectionAwaiter;
    private readonly SubscriptionFactory _subscriptionFactory;
    private readonly NotificationHub _hub;
    private readonly ILogger<OpcUaSubscriptionService> _logger;
    private List<string> _monitoredNodeIds = [];
    private SubscriptionState State { get; set; } = SubscriptionState.NotInitialized;

    public OpcUaSubscriptionService(
        OpcUaConnectionService connectionService,
        IOptions<OpcUaSettings> settings,
        ILogger<OpcUaSubscriptionService> logger,
        ILoggerFactory loggerFactory)
    {
        _connectionService = connectionService;
        _logger = logger;
        _hub = new NotificationHub(loggerFactory.CreateLogger<NotificationHub>());
        _connectionAwaiter = new ConnectionAwaiter(connectionService, loggerFactory.CreateLogger<ConnectionAwaiter>());
        _subscriptionFactory = new SubscriptionFactory(settings.Value, loggerFactory.CreateLogger<SubscriptionFactory>());
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public async Task InitializeAsync(IEnumerable<string> nodeIds, CancellationToken ct = default)
    {
        _monitoredNodeIds = nodeIds.ToList();
        if (_monitoredNodeIds.Count == 0)
        {
            _logger.LogWarning("Список узлов для мониторинга пуст");
            return;
        }
        _hub.Start();
        await CreateWithRetryAsync(ct).ConfigureAwait(false);
        State = SubscriptionState.Active;
    }

    public void Subscribe(string nodeId, Action<OpcValue> callback)
    {
        _hub.Subscribe(nodeId, callback);
    }

    public void Unsubscribe(string nodeId, Action<OpcValue> callback)
    {
        _hub.Unsubscribe(nodeId, callback);
    }

    public OpcValue? GetCurrentValue(string nodeId)
    {
        return _hub.GetValue(nodeId);
    }

    public async Task StopAsync()
    {
        State = SubscriptionState.Stopped;
        _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
        await _hub.DisposeAsync().ConfigureAwait(false);
        await _subscriptionFactory.RemoveAsync(_connectionService.Session).ConfigureAwait(false);
        _logger.LogInformation("OpcUaSubscriptionService остановлен");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task CreateWithRetryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await TryCreateOnceAsync(ct).ConfigureAwait(false);
            if (result != null)
            {
                return;
            }
        }
        ct.ThrowIfCancellationRequested();
    }

    private async Task<SubscriptionResult?> TryCreateOnceAsync(CancellationToken ct)
    {
        await _connectionAwaiter.WaitAsync(ct).ConfigureAwait(false);
        var session = _connectionService.Session;
        if (session?.Connected != true)
        {
            return null;
        }
        return await CreateSubscription(session, ct);
    }

    private async Task<SubscriptionResult?> CreateSubscription(ISession session, CancellationToken ct)
    {
        try
        {
            return await _subscriptionFactory.CreateAsync(
                session,
                _monitoredNodeIds,
                _hub.Enqueue,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Ошибка при создании подписки, повторяем...");
            await _subscriptionFactory.RemoveAsync(session).ConfigureAwait(false);
            return null;
        }
    }

    private void OnConnectionStateChanged(bool connected)
    {
        if (connected)
        {
            HandleConnectionRestored();
            return;
        }
        HandleConnectionLost();
    }

    private void HandleConnectionRestored()
    {
        if (State != SubscriptionState.Suspended)
        {
            return;
        }
        State = SubscriptionState.Active;
        _logger.LogInformation("Соединение восстановлено, подписки активны");
    }

    private void HandleConnectionLost()
    {
        State = SubscriptionState.Suspended;
        _logger.LogWarning("Соединение потеряно, подписки приостановлены");
    }
}
