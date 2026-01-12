using Final_Test_Hybrid.Models.Shift;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Settings.Spring;
using Final_Test_Hybrid.Settings.Spring.Shift;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.SpringBoot.Shift;

public class ShiftService(
    SpringBootHttpClient httpClient,
    ShiftState shiftState,
    AppSettingsService appSettingsService,
    IOptions<ShiftSettings> options,
    DualLogger<ShiftService> logger)
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
        logger.LogInformation("Сервис смен запущен, UseMes={UseMes}", appSettingsService.UseMes);
    }

    private void OnUseMesChanged(bool useMes)
    {
        if (useMes)
        {
            StartPolling();
            logger.LogInformation("UseMes изменён на true, опрос MES запущен");
        }
        else
        {
            StopPolling();
            shiftState.SetShiftNumber(null);
            logger.LogInformation("UseMes изменён на false, опрос остановлен, смена очищена");
        }
    }

    private void StartPolling()
    {
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.PollingIntervalMs));
        _ = RunPollingLoopAsync(_cts.Token);
        logger.LogInformation("Опрос смен запущен с интервалом {IntervalMs}мс", _settings.PollingIntervalMs);
    }

    private void StopPolling()
    {
        _cts?.Cancel();
        _timer?.Dispose();
        _cts?.Dispose();
        _timer = null;
        _cts = null;
        logger.LogInformation("Опрос смен остановлен");
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
            logger.LogError(ex, "Цикл опроса смен неожиданно завершился с ошибкой");
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
            logger.LogWarning("Ошибка получения номера смены: {Message}", ex.Message);
            shiftState.SetShiftNumber(0);
        }
    }

    public void Stop()
    {
        appSettingsService.UseMesChanged -= OnUseMesChanged;
        StopPolling();
        logger.LogInformation("Сервис смен остановлен");
    }
}
