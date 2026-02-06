using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Settings.OpcUa;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.OpcUa.Subscription;

/// <summary>
/// Периодически логирует состояние runtime OPC-подписок для диагностики роста monitored items.
/// </summary>
public sealed class OpcUaSubscriptionDiagnosticsService(
    OpcUaSubscription subscription,
    IOptions<OpcUaSettings> settingsOptions,
    DualLogger<OpcUaSubscriptionDiagnosticsService> logger) : IAsyncDisposable
{
    private readonly OpcUaSubscriptionDiagnosticsSettings _settings = settingsOptions.Value.SubscriptionDiagnostics;
    private readonly Lock _stateLock = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _started;

    public Task StartAsync()
    {
        if (!_settings.Enabled)
        {
            logger.LogDebug("Диагностика OPC подписок отключена настройкой OpcUa:SubscriptionDiagnostics:Enabled");
            return Task.CompletedTask;
        }

        if (!TryStartLoop())
        {
            return Task.CompletedTask;
        }

        logger.LogInformation(
            "Диагностика OPC подписок запущена. Интервал: {IntervalSec} сек, лимит новых nodeId в логе: {Top}",
            _settings.SnapshotIntervalSec,
            _settings.LogTopNodeIds);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        var state = StopLoop();
        if (state is null)
        {
            return;
        }

        var loopState = state.Value;
        loopState.Cts.Cancel();
        await WaitLoopAsync(loopState).ConfigureAwait(false);
        logger.LogInformation("Диагностика OPC подписок остановлена");
    }

    private bool TryStartLoop()
    {
        lock (_stateLock)
        {
            if (_started)
            {
                return false;
            }

            _started = true;
            _cts = new CancellationTokenSource();
            _loopTask = RunLoopAsync(_cts.Token);
            return true;
        }
    }

    private DiagnosticLoopState? StopLoop()
    {
        lock (_stateLock)
        {
            if (!_started || _cts == null)
            {
                return null;
            }

            _started = false;
            var state = new DiagnosticLoopState(_cts, _loopTask);
            _cts = null;
            _loopTask = null;
            return state;
        }
    }

    private async Task WaitLoopAsync(DiagnosticLoopState state)
    {
        try
        {
            if (state.LoopTask != null)
            {
                await state.LoopTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // штатная остановка
        }
        finally
        {
            state.Cts.Dispose();
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_settings.SnapshotIntervalSec));
        var previous = subscription.GetDiagnosticsSnapshot();
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            var current = subscription.GetDiagnosticsSnapshot();
            LogSnapshot(previous, current);
            previous = current;
        }
    }

    private void LogSnapshot(OpcUaSubscriptionDiagnosticsSnapshot previous, OpcUaSubscriptionDiagnosticsSnapshot current)
    {
        var delta = current.MonitoredItemsCount - previous.MonitoredItemsCount;
        logger.LogInformation(
            "OPC snapshot: monitored={Monitored} (delta {Delta}), callbackNodes={CallbackNodes}, callbackTotal={CallbackTotal}",
            current.MonitoredItemsCount,
            delta,
            current.CallbackNodeCount,
            current.CallbackTotalCount);

        if (delta <= 0)
        {
            return;
        }

        var addedNodeIds = current.NodeIds
            .Except(previous.NodeIds, StringComparer.OrdinalIgnoreCase)
            .Take(_settings.LogTopNodeIds)
            .ToArray();
        if (addedNodeIds.Length == 0)
        {
            return;
        }

        logger.LogWarning("Обнаружены новые monitored items ({Count}): {NodeIds}", addedNodeIds.Length, string.Join(", ", addedNodeIds));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private readonly record struct DiagnosticLoopState(CancellationTokenSource Cts, Task? LoopTask);
}
