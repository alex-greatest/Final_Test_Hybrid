using Final_Test_Hybrid.Models.Shift;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Settings.Shift;
using Final_Test_Hybrid.Settings.Spring;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Shift;

public class ShiftService(
    SpringBootHttpClient httpClient,
    ShiftState shiftState,
    AppSettingsService appSettingsService,
    IOptions<ShiftSettings> options,
    ILogger<ShiftService> logger)
{
    private readonly ShiftSettings _settings = options.Value;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    public void Start()
    {
        appSettingsService.UseMesChanged += OnUseMesChanged;
        if (appSettingsService.UseMes)
        {
            StartPolling();
        }
        logger.LogInformation("ShiftService started, UseMes={UseMes}", appSettingsService.UseMes);
    }

    private void OnUseMesChanged(bool useMes)
    {
        if (useMes)
        {
            // Переключение false → true: запускаем таймер (работа с MES)
            StartPolling();
            logger.LogInformation("UseMes changed to true, MES polling started");
        }
        else
        {
            // Переключение true → false: останавливаем таймер, очищаем состояние
            StopPolling();
            shiftState.SetShiftNumber(null);
            logger.LogInformation("UseMes changed to false, polling stopped, shift cleared");
        }
    }

    private void StartPolling()
    {
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.PollingIntervalMs));
        _ = RunPollingLoopAsync(_cts.Token);
        logger.LogInformation("Shift polling started with interval {IntervalMs}ms", _settings.PollingIntervalMs);
    }

    private void StopPolling()
    {
        _cts?.Cancel();
        _timer?.Dispose();
        _cts?.Dispose();
        _timer = null;
        _cts = null;
        logger.LogInformation("Shift polling stopped");
    }

    private async Task RunPollingLoopAsync(CancellationToken ct)
    {
        try
        {
            await FetchShiftAsync(ct).ConfigureAwait(false);
            while (await _timer!.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await FetchShiftAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when StopPolling() is called
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Shift polling loop failed unexpectedly");
        }
    }

    private async Task FetchShiftAsync(CancellationToken ct)
    {
        try
        {
            var endpoint = $"{_settings.Endpoint}?nameStation={Uri.EscapeDataString(appSettingsService.NameStation)}";
            var response = await httpClient.GetAsync<ShiftResponse>(endpoint, ct).ConfigureAwait(false);
            shiftState.SetShiftNumber(response?.ShiftNumber);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch shift number");
        }
    }

    public void Stop()
    {
        appSettingsService.UseMesChanged -= OnUseMesChanged;
        StopPolling();
        logger.LogInformation("ShiftService stopped");
    }
}
