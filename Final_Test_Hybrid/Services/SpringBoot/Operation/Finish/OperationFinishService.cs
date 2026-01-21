using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Storage;
using Final_Test_Hybrid.Settings.Spring;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Finish;

/// <summary>
/// Сервис для завершения операции тестирования в MES.
/// Отправляет результаты теста на POST /api/operation/finish.
/// </summary>
public class OperationFinishService(
    SpringBootHttpClient httpClient,
    OrderState orderState,
    DualLogger<OperationFinishService> logger)
{
    private const string FinishEndpoint = "/api/operation/finish";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Отправляет результаты теста в MES.
    /// </summary>
    /// <param name="request">Запрос с результатами теста.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат сохранения.</returns>
    public async Task<SaveResult> FinishOperationAsync(FinalTestResultsRequest request, CancellationToken ct)
    {
        logger.LogInformation(
            "Отправка результатов теста для {SerialNumber}, результат={Result}",
            request.SerialNumber,
            request.Result);

        try
        {
            using var response = await httpClient.PostWithResponseAsync(FinishEndpoint, request, ct);
            return await ProcessResponseAsync(response, request.SerialNumber, ct);
        }
        catch (Exception ex)
        {
            return HandleException(ex, request.SerialNumber, ct);
        }
    }

    private async Task<SaveResult> ProcessResponseAsync(
        HttpResponseMessage response,
        string serialNumber,
        CancellationToken ct)
    {
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await HandleSuccessAsync(response, serialNumber, ct),
            HttpStatusCode.NotFound => await HandleNotFoundAsync(response, ct),
            _ => await HandleErrorResponseAsync(response, ct)
        };
    }

    private async Task<SaveResult> HandleSuccessAsync(
        HttpResponseMessage response,
        string serialNumber,
        CancellationToken ct)
    {
        var data = await response.Content.ReadFromJsonAsync<BoilerMadeInformationResponse>(JsonOptions, ct);
        if (data == null)
        {
            logger.LogWarning("Пустой ответ от MES для {SerialNumber}", serialNumber);
            return SaveResult.Success();
        }

        orderState.SetData(data.OrderNumber, data.AmountBoilerOrder, data.AmountBoilerMadeOrder);

        logger.LogInformation(
            "Результаты теста отправлены в MES: OrderNumber={OrderNumber}, Made={Made}/{Total}",
            data.OrderNumber,
            data.AmountBoilerMadeOrder,
            data.AmountBoilerOrder);

        return SaveResult.Success();
    }

    private async Task<SaveResult> HandleNotFoundAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var errorMessage = await TryParseErrorMessageAsync(response, ct);
        logger.LogWarning("MES: операция не найдена - {Error}", errorMessage);
        return SaveResult.Fail(errorMessage);
    }

    private async Task<SaveResult> HandleErrorResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var errorMessage = await TryParseErrorMessageAsync(response, ct);
        logger.LogError("Ошибка MES со статусом {StatusCode}: {Error}", response.StatusCode, errorMessage);
        return SaveResult.Fail(errorMessage);
    }

    private async Task<string> TryParseErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<OperationErrorResponse>(JsonOptions, ct);
            if (!string.IsNullOrEmpty(errorResponse?.Error))
            {
                return errorResponse.Error;
            }
            return !string.IsNullOrEmpty(errorResponse?.Message) ? errorResponse.Message : "Неизвестная ошибка";
        }
        catch
        {
            return "Ошибка сервера";
        }
    }

    private SaveResult HandleException(Exception ex, string serialNumber, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            HandleCancellation();
        }

        return ex switch
        {
            TaskCanceledException => HandleTimeout(serialNumber),
            HttpRequestException httpEx => HandleConnectionError(httpEx, serialNumber),
            _ => HandleUnexpectedError(ex, serialNumber)
        };
    }

    /// <summary>
    /// Пробрасывает OperationCanceledException для единой семантики отмены с DatabaseTestResultStorage.
    /// </summary>
    private void HandleCancellation()
    {
        logger.LogInformation("Отправка результатов отменена");
        throw new OperationCanceledException();
    }

    private SaveResult HandleTimeout(string serialNumber)
    {
        logger.LogWarning("Таймаут отправки результатов для {SerialNumber}", serialNumber);
        return SaveResult.Fail("Таймаут соединения");
    }

    private SaveResult HandleConnectionError(HttpRequestException ex, string serialNumber)
    {
        logger.LogError(ex, "Нет соединения с MES для {SerialNumber}", serialNumber);
        return SaveResult.Fail("Нет соединения с сервером");
    }

    private SaveResult HandleUnexpectedError(Exception ex, string serialNumber)
    {
        logger.LogError(ex, "Ошибка отправки результатов для {SerialNumber}", serialNumber);
        return SaveResult.Fail("Неизвестная ошибка");
    }
}
