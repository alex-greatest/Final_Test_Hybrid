using System.Collections.Concurrent;
using System.Threading.Channels;
using Final_Test_Hybrid.Models.Plc;
using Final_Test_Hybrid.Models.Plc.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;


public partial class OpcUaSubscriptionService
{
    private readonly OpcUaConnectionService _connectionService;
    private readonly OpcUaSettings _settings;
    private readonly ILogger<OpcUaSubscriptionService> _logger;
    private record OpcValueChange(string NodeId, OpcValue Value);
    private readonly Channel<OpcValueChange> _channel;
    private readonly ConcurrentDictionary<string, OpcValue> _cache = new();
    private readonly ConcurrentDictionary<string, List<Action<OpcValue>>> _subscribers = new();
    private readonly object _subscribersLock = new();
    private Subscription? _subscription;
    private CancellationTokenSource? _processingCts;
    private Task? _processingTask;
    private List<string> _monitoredNodeIds = [];
    public SubscriptionState State { get; private set; } = SubscriptionState.NotInitialized;
    public event Action? SubscriptionsRestored;

    public OpcUaSubscriptionService(
        OpcUaConnectionService connectionService,
        IOptions<OpcUaSettings> settings,
        ILogger<OpcUaSubscriptionService> logger)
    {
        _connectionService = connectionService;
        _settings = settings.Value;
        _logger = logger;
        _channel = Channel.CreateUnbounded<OpcValueChange>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _connectionService.SessionRestored += OnSessionRestored;
    }

    public async Task InitializeAsync(IEnumerable<string> nodeIds, CancellationToken ct = default)
    {
        _monitoredNodeIds = nodeIds.ToList();
        if (_monitoredNodeIds.Count == 0)
        {
            _logger.LogWarning("Список узлов для мониторинга пуст");
            return;
        }
        StartProcessingTask();
        await CreateSubscriptionAsync(_connectionService.Session!, _monitoredNodeIds, ct)
            .ConfigureAwait(false);
        State = SubscriptionState.Active;
    }

    public OpcValue? GetCurrentValue(string nodeId)
    {
        _cache.TryGetValue(nodeId, out var value);
        return value;
    }

    public async Task StopAsync()
    {
        State = SubscriptionState.Stopped;
        _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
        _connectionService.SessionRestored -= OnSessionRestored;
        _channel.Writer.Complete();
        if (_processingCts != null)
        {
            await _processingCts.CancelAsync().ConfigureAwait(false);
        }
        if (_processingTask != null)
        {
            await _processingTask.ConfigureAwait(false);
        }
        _processingCts?.Dispose();
        await RemoveSubscriptionAsync().ConfigureAwait(false);
        _logger.LogInformation("OpcUaSubscriptionService остановлен");
    }

    private void OnConnectionStateChanged(bool connected)
    {
        if (connected)
        {
            return;
        }
        State = SubscriptionState.Suspended;
        _logger.LogWarning("Соединение потеряно, подписки приостановлены");
    }

    private void OnSessionRestored(ISession newSession)
    {
        _logger.LogInformation("Сессия восстановлена, пересоздаём подписки...");
        _ = RecreateSubscriptionAsync(newSession);
    }

    private async Task RecreateSubscriptionAsync(ISession session)
    {
        try
        {
            await RemoveSubscriptionAsync().ConfigureAwait(false);
            await CreateSubscriptionAsync(session, _monitoredNodeIds, CancellationToken.None)
                .ConfigureAwait(false);
            State = SubscriptionState.Active;
            _logger.LogInformation("Подписки восстановлены: {Count} узлов", _monitoredNodeIds.Count);
            SubscriptionsRestored?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при пересоздании подписок");
        }
    }
    
}
