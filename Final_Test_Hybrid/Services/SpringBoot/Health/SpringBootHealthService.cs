using Final_Test_Hybrid.Settings.Spring;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.SpringBoot.Health;

public class SpringBootHealthService(
    SpringBootHttpClient httpClient,
    SpringBootConnectionState connectionState,
    IOptions<SpringBootSettings> options,
    ILogger<SpringBootHealthService> logger)
{
    private readonly int _intervalMs = options.Value.HealthCheckIntervalMs;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_intervalMs));
        _ = RunHealthCheckLoopAsync(_cts.Token);
        logger.LogInformation("Health check started with interval {IntervalMs}ms", _intervalMs);
    }

    private async Task RunHealthCheckLoopAsync(CancellationToken ct)
    {
        try
        {
            await CheckHealthAsync().ConfigureAwait(false);
            while (await _timer!.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await CheckHealthAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when Stop() is called
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health check loop failed unexpectedly");
        }
    }

    private async Task CheckHealthAsync()
    {
        try
        {
            var isHealthy = await httpClient.IsReachableAsync("actuator/health").ConfigureAwait(false);
            connectionState.SetConnected(isHealthy);
            if (!isHealthy)
            {
                logger.LogWarning("Spring Boot server is not responding");
            }
        }
        catch (Exception ex)
        {
            connectionState.SetConnected(false);
            logger.LogWarning(ex, "Health check failed");
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _timer?.Dispose();
        _cts?.Dispose();
        _timer = null;
        _cts = null;
        logger.LogInformation("Health check stopped");
    }
}
