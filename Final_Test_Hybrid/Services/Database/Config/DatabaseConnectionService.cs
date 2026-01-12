using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Settings.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Database.Config;

public class DatabaseConnectionService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    DatabaseConnectionState connectionState,
    IOptions<DatabaseSettings> options,
    DualLogger<DatabaseConnectionService> logger)
{
    private readonly int _intervalMs = options.Value.HealthCheckIntervalMs;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_intervalMs));
        _ = RunHealthCheckLoopAsync(_cts.Token);
        logger.LogInformation("Проверка подключения к БД запущена с интервалом {IntervalMs} мс", _intervalMs);
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
            // Ожидаемое поведение при вызове Stop()
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Цикл проверки подключения к БД неожиданно завершился с ошибкой");
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
                logger.LogWarning("База данных не отвечает");
            }
        }
        catch (Exception ex)
        {
            connectionState.SetConnected(false);
            logger.LogWarning("Проверка подключения к БД не удалась: {Message}", ex.Message);
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _timer?.Dispose();
        _cts?.Dispose();
        _timer = null;
        _cts = null;
        logger.LogInformation("Проверка подключения к БД остановлена");
    }
}
