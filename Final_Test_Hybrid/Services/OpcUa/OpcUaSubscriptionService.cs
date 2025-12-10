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
    private TaskCompletionSource<bool>? _connectionTcs;
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
        await WaitForConnectionAsync(ct).ConfigureAwait(false);
        StartProcessingTask();
        await CreateSubscriptionAsync(_connectionService.Session!, _monitoredNodeIds, ct)
            .ConfigureAwait(false);
        State = SubscriptionState.Active;
    }
    
    private async Task WaitForConnectionAsync(CancellationToken ct)
    {
        if (_connectionService.IsConnected)
        {
            return;
        }
        await WaitForConnectionWithCleanupAsync(ct).ConfigureAwait(false);
    }
    
    private async Task WaitForConnectionWithCleanupAsync(CancellationToken ct)
    {
        StartConnectionWait();
        try
        {
            await AwaitConnectionSignalAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            StopConnectionWait();
        }
    }
    
    private async Task AwaitConnectionSignalAsync(CancellationToken ct)
    {
        if (_connectionService.IsConnected)
        {
            return;
        }
        _logger.LogInformation("Ожидание подключения к OPC UA серверу...");
        await _connectionTcs!.Task.WaitAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Подключение установлено");
    }
    
    private void StartConnectionWait()
    {
        _connectionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _connectionService.ConnectionStateChanged += OnConnectionForWait;
    }

    private void StopConnectionWait()
    {
        _connectionService.ConnectionStateChanged -= OnConnectionForWait;
        _connectionTcs = null;
    }
    
    public OpcValue? GetCurrentValue(string nodeId)
    {
        _cache.TryGetValue(nodeId, out var value);
        return value;
    }

    public async Task StopAsync()
    {
        State = SubscriptionState.Stopped;
        UnsubscribeFromConnectionEvents();
        _channel.Writer.Complete();
        await StopProcessingTaskAsync().ConfigureAwait(false);
        await RemoveSubscriptionAsync().ConfigureAwait(false);
        _logger.LogInformation("OpcUaSubscriptionService остановлен");
    }

    private void UnsubscribeFromConnectionEvents()
    {
        _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
        _connectionService.SessionRestored -= OnSessionRestored;
    }

    private async Task StopProcessingTaskAsync()
    {
        if (_processingCts != null)
        {
            await _processingCts.CancelAsync().ConfigureAwait(false);
        }
        await WaitForProcessingTaskAsync().ConfigureAwait(false);
        _processingCts?.Dispose();
    }

    private async Task WaitForProcessingTaskAsync()
    {
        if (_processingTask != null)
        {
            await _processingTask.ConfigureAwait(false);
        }
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

    private void OnConnectionForWait(bool connected)
    {
        if (!connected)
        {
            return;
        }
        _connectionTcs?.TrySetResult(true);
    }
}
