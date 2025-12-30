using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorHandling;

/// <summary>
/// Monitors PLC error handling signals (Retry, Skip) via OPC UA subscriptions.
/// Initialized once at application startup.
/// </summary>
public class ErrorPlcMonitor(OpcUaSubscription subscription, ILogger<ErrorPlcMonitor> logger)
{
    public event Action<bool, bool>? OnSignalsChanged;

    /// <summary>
    /// Subscribes to error handling tags. Called once at application startup.
    /// Throws if subscription fails — application should crash.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await subscription.SubscribeAsync(BaseTags.ErrorRetry, OnRetryChanged, ct);
        await subscription.SubscribeAsync(BaseTags.ErrorSkip, OnSkipChanged, ct);
        logger.LogInformation("Подписка на теги ошибок создана (ErrorRetry, ErrorSkip)");
    }

    private Task OnRetryChanged(object? value)
    {
        logger.LogInformation("ErrorRetry = {Value}", value);
        if (value is not true) return Task.CompletedTask;
        OnSignalsChanged?.Invoke(true, false);
        return Task.CompletedTask;
    }

    private Task OnSkipChanged(object? value)
    {
        logger.LogInformation("ErrorSkip = {Value}", value);
        if (value is not true) return Task.CompletedTask;
        OnSignalsChanged?.Invoke(false, true);
        return Task.CompletedTask;
    }
}
