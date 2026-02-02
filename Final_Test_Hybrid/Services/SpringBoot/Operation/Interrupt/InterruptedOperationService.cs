using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Settings.Spring;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

/// <summary>
/// Сервис отправки информации о прерванной операции в MES.
/// </summary>
public class InterruptedOperationService(
    SpringBootHttpClient httpClient,
    AppSettingsService appSettings,
    DualLogger<InterruptedOperationService> logger)
{
    public async Task<SaveResult> SendAsync(
        string serialNumber,
        string adminUsername,
        string message,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return SaveResult.Fail("Операция отменена");
        }
        try
        {
            return await ExecuteSendAsync(serialNumber, adminUsername, message, ct);
        }
        catch (OperationCanceledException)
        {
            return SaveResult.Fail("Операция отменена");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка отправки в MES");
            return SaveResult.Fail("Ошибка связи с сервером");
        }
    }

    private async Task<SaveResult> ExecuteSendAsync(
        string serialNumber,
        string adminUsername,
        string message,
        CancellationToken ct)
    {
        var request = new InterruptedOperationRequest(
            SerialNumber: serialNumber,
            StationName: appSettings.NameStation,
            Message: message,
            AdminInterrupted: adminUsername);

        var response = await httpClient.PostWithResponseAsync(
            "/api/station/interrupted/operation/request", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return SaveResult.Success();
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        logger.LogError(
            "MES вернул ошибку: {StatusCode} {ReasonPhrase}. Тело: {Content}",
            (int)response.StatusCode,
            response.ReasonPhrase,
            content);

        return SaveResult.Fail("Ошибка сервера");
    }
}
