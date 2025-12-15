using Final_Test_Hybrid.Settings.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Database;

public class DatabaseConnectionService(
    IServiceProvider serviceProvider,
    DatabaseConnectionState connectionState,
    IOptions<DatabaseSettings> options,
    ILogger<DatabaseConnectionService> logger)
{
    private readonly int _intervalMs = options.Value.HealthCheckIntervalMs;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_intervalMs));
        _ = RunHealthCheckLoopAsync(_cts.Token);
        logger.LogInformation("Database health check started with interval {IntervalMs}ms", _intervalMs);
    }

    private async Task RunHealthCheckLoopAsync(CancellationToken ct)
    {
        try
        {
            await CheckConnectionAsync().ConfigureAwait(false);
            while (await _timer!.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await CheckConnectionAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when Stop() is called
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database health check loop failed unexpectedly");
        }
    }

    private async Task CheckConnectionAsync()
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync().ConfigureAwait(false);
            connectionState.SetConnected(canConnect);
            if (!canConnect)
            {
                logger.LogWarning("Database is not responding");
            }
        }
        catch (Exception ex)
        {
            connectionState.SetConnected(false);
            logger.LogWarning(ex, "Database health check failed");
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _timer?.Dispose();
        _cts?.Dispose();
        _timer = null;
        _cts = null;
        logger.LogInformation("Database health check stopped");
    }
}
