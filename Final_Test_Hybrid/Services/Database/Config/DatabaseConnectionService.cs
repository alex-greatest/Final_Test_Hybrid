using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Settings.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Database.Config;

public class DatabaseConnectionService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    DatabaseConnectionState connectionState,
    IOptions<DatabaseSettings> options,
    ILogger<DatabaseConnectionService> logger,
    IDatabaseLogger dbLogger)
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
        dbLogger.LogInformation("Проверка подключения к БД запущена с интервалом {IntervalMs}мс", _intervalMs);
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
            dbLogger.LogError(ex, "Цикл проверки подключения к БД неожиданно завершился с ошибкой");
        }
    }

    private async Task CheckConnectionAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var canConnect = await dbContext.Database.CanConnectAsync().ConfigureAwait(false);
            connectionState.SetConnected(canConnect);
            if (!canConnect)
            {
                logger.LogWarning("Database is not responding");
                dbLogger.LogWarning("База данных не отвечает");
            }
        }
        catch (Exception ex)
        {
            connectionState.SetConnected(false);
            logger.LogWarning(ex, "Database health check failed");
            dbLogger.LogWarning("Проверка подключения к БД не удалась: {Message}", ex.Message);
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
        dbLogger.LogInformation("Проверка подключения к БД остановлена");
    }
}
