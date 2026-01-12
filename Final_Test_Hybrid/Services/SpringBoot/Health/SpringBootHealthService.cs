using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Settings.Spring;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.SpringBoot.Health;

public class SpringBootHealthService(
    SpringBootHttpClient httpClient,
    SpringBootConnectionState connectionState,
    IOptions<SpringBootSettings> options,
    DualLogger<SpringBootHealthService> logger)
{
    private readonly int _intervalMs = options.Value.HealthCheckIntervalMs;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_intervalMs));
        _ = RunHealthCheckLoopAsync(_cts.Token);
        logger.LogInformation("Проверка состояния запущена с интервалом {IntervalMs}мс", _intervalMs);
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
            logger.LogError(ex, "Цикл проверки состояния неожиданно завершился с ошибкой");
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
                logger.LogWarning("Сервер Spring Boot не отвечает");
            }
        }
        catch (Exception ex)
        {
            connectionState.SetConnected(false);
            logger.LogWarning("Проверка состояния не удалась: {Message}", ex.Message);
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _timer?.Dispose();
        _cts?.Dispose();
        _timer = null;
        _cts = null;
        logger.LogInformation("Проверка состояния остановлена");
    }
}
