using System.Collections.Concurrent;
using System.Threading.Channels;
using Final_Test_Hybrid.Models.Plc;
using Final_Test_Hybrid.Models.Plc.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

    public OpcUaSubscriptionService(
        OpcUaConnectionService connectionService,
        IOptions<OpcUaSettings> settings,
        ILogger<OpcUaSubscriptionService> logger, OpcUaSettings settings1)
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
    }

    public async Task<SubscriptionResult> InitializeAsync(IEnumerable<string> nodeIds, CancellationToken ct = default)
    {
        _monitoredNodeIds = nodeIds.ToList();
        if (_monitoredNodeIds.Count == 0)
        {
            _logger.LogWarning("Список узлов для мониторинга пуст");
            return new SubscriptionResult([], []);
        }
        StartProcessingTask();
        var result = await CreateSubscriptionWithRetryAsync(ct).ConfigureAwait(false);
        State = SubscriptionState.Active;
        return result;
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
    
    private void OnConnectionForWait(bool connected)
    {
        if (!connected)
        {
            return;
        }
        _connectionTcs?.TrySetResult(true);
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
            OnConnectionRestored();
            return;
        }
        OnConnectionLost();
    }

    private void OnConnectionRestored()
    {
        if (State != SubscriptionState.Suspended)
        {
            return;
        }
        State = SubscriptionState.Active;
        _logger.LogInformation("Соединение восстановлено, подписки активны");
    }

    private void OnConnectionLost()
    {
        State = SubscriptionState.Suspended;
        _logger.LogWarning("Соединение потеряно, подписки приостановлены");
    }

    private async Task<SubscriptionResult> CreateSubscriptionWithRetryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await TryCreateSubscriptionAsync(ct).ConfigureAwait(false);
            if (result != null)
            {
                return result;
            }
        }

        ct.ThrowIfCancellationRequested();
        return new SubscriptionResult([], []);
    }

    private async Task<SubscriptionResult?> TryCreateSubscriptionAsync(CancellationToken ct)
    {
        await WaitForConnectionAsync(ct).ConfigureAwait(false);

        var session = _connectionService.Session;
        if (session?.Connected != true)
        {
            return null;
        }

        return await TryCreateSubscriptionCoreAsync(session, ct).ConfigureAwait(false);
    }

    private async Task<SubscriptionResult?> TryCreateSubscriptionCoreAsync(ISession session, CancellationToken ct)
    {
        try
        {
            return await CreateSubscriptionAsync(session, _monitoredNodeIds, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && IsTransientError(ex))
        {
            await HandleTransientErrorAsync(ex).ConfigureAwait(false);
            return null;
        }
    }

    private async Task HandleTransientErrorAsync(Exception ex)
    {
        _logger.LogWarning(ex, "Сетевая ошибка при создании подписки, повторяем...");
        await CleanupFailedSubscriptionAsync().ConfigureAwait(false);
    }

    private static bool IsTransientError(Exception ex)
    {
        return ex is not OperationCanceledException;
    }

    private async Task CleanupFailedSubscriptionAsync()
    {
        if (_subscription == null)
        {
            return;
        }
        try
        {
            await RemoveSubscriptionAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ошибка очистки подписки (игнорируем)");
        }

        _subscription = null;
    }
}
