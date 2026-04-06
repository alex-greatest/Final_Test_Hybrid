using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Finish;
using Final_Test_Hybrid.Settings.Spring;
using Final_Test_Hybrid.Services.Storage;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

/// <summary>
/// Сервис отправки информации о прерванной операции в MES.
/// </summary>
public class InterruptedOperationService(
    SpringBootHttpClient httpClient,
    AppSettingsService appSettings,
    FinalTestResultsSnapshotBuilder snapshotBuilder,
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
        var snapshot = TryBuildInterruptSnapshot();
        var request = new InterruptedOperationRequest
        {
            SerialNumber = serialNumber,
            StationName = appSettings.NameStation,
            Message = message,
            AdminInterrupted = adminUsername,
            Operator = snapshot?.Operator,
            Items = snapshot?.Items,
            ItemsLimited = snapshot?.ItemsLimited,
            Time = snapshot?.Time,
            Errors = snapshot?.Errors,
            Result = snapshot?.Result
        };
        LogRequestPrepared(request);

        var response = await httpClient.PostWithResponseAsync(
            "/api/operation/interrupt", request, ct);

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

    private FinalTestResultsRequest? TryBuildInterruptSnapshot()
    {
        if (snapshotBuilder.TryBuild(InterruptedTestResultCodes.Interrupted, out var snapshot, out _))
        {
            return snapshot;
        }

        logger.LogWarning("Interrupt snapshot результатов не собран, запрос будет отправлен без полей snapshot");
        return null;
    }

    private void LogRequestPrepared(InterruptedOperationRequest request)
    {
        logger.LogDebug(
            "Подготовлен interrupt request: SerialNumber={SerialNumber}, StationName={StationName}, AdminInterrupted={AdminInterrupted}, Message={Message}, HasResultSnapshot={HasResultSnapshot}",
            request.SerialNumber,
            request.StationName,
            request.AdminInterrupted,
            request.Message,
            request.Result != null
                || request.Operator != null
                || request.Items != null
                || request.ItemsLimited != null
                || request.Time != null
                || request.Errors != null);
    }
}
