using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.OpcUa;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorHandling;

/// <summary>
/// Monitors PLC error handling signals (Retry, Skip).
/// </summary>
public class ErrorPlcMonitor(OpcUaTagService opcUa, ILogger<ErrorPlcMonitor> logger) : IDisposable
{
    private const int PollingIntervalMs = 100;
    private readonly Lock _lock = new();
    private CancellationTokenSource? _monitoringCts;
    private bool _disposed;

    public event Action<bool, bool>? OnSignalsChanged;

    public void StartMonitoring()
    {
        lock (_lock)
        {
            if (_disposed || _monitoringCts != null)
            {
                return;
            }

            _monitoringCts = new CancellationTokenSource();
            _ = MonitorLoopAsync(_monitoringCts.Token);
        }
    }

    public void StopMonitoring()
    {
        lock (_lock)
        {
            if (_monitoringCts == null)
            {
                return;
            }

            CancelAndDisposeMonitoring();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CancelAndDisposeMonitoring();
        }
    }

    private void CancelAndDisposeMonitoring()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("ErrorPlcMonitor started");
        try
        {
            await ExecutePollingLoop(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in PLC monitor loop");
        }
        logger.LogDebug("ErrorPlcMonitor stopped");
    }

    private async Task ExecutePollingLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await PollAndNotifyAsync(cancellationToken);
            await Task.Delay(PollingIntervalMs, cancellationToken);
        }
    }

    private async Task PollAndNotifyAsync(CancellationToken cancellationToken)
    {
        var signals = await ReadAllSignalsAsync(cancellationToken);
        OnSignalsChanged?.Invoke(signals.Retry, signals.Skip);
    }

    private async Task<PlcErrorSignals> ReadAllSignalsAsync(CancellationToken cancellationToken)
    {
        var retryTask = ReadBoolTagAsync(BaseTags.ErrorRetry, cancellationToken);
        var skipTask = ReadBoolTagAsync(BaseTags.ErrorSkip, cancellationToken);

        await Task.WhenAll(retryTask, skipTask);

        return new PlcErrorSignals(
            Retry: await retryTask,
            Skip: await skipTask);
    }

    private async Task<bool> ReadBoolTagAsync(string tagId, CancellationToken cancellationToken)
    {
        var result = await opcUa.ReadAsync<bool>(tagId, cancellationToken);
        if (result.Error == null)
        {
            return result.Value;
        }
        logger.LogWarning("Failed to read {TagId}: {Error}", tagId, result.Error);
        return false;
    }

    private record PlcErrorSignals(bool Retry, bool Skip);
}
