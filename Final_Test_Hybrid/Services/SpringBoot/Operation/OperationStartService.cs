using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Settings.Spring;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation;

public class OperationStartService(
    SpringBootHttpClient httpClient,
    AppSettingsService appSettingsService,
    ILogger<OperationStartService> logger,
    ISpringBootLogger sbLogger)
{
    private const string StartEndpoint = "/api/operation/start";
    private const string ReworkEndpoint = "/api/operation/rework";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<OperationStartResult> StartOperationAsync(
        string serialNumber,
        string operatorName,
        string admin = "",
        CancellationToken ct = default)
    {
        var request = BuildRequest(serialNumber, operatorName, admin);
        logger.LogInformation("Starting operation for {SerialNumber}", serialNumber);
        sbLogger.LogInformation("Старт операции для {SerialNumber}", serialNumber);
        return await ExecuteStartAsync(request, ct);
    }

    public async Task<OperationStartResult> ReworkAsync(
        string serialNumber,
        string operatorName,
        string admin,
        CancellationToken ct = default)
    {
        var request = BuildRequest(serialNumber, operatorName, admin);
        logger.LogInformation("Rework request for {SerialNumber} by admin {Admin}", serialNumber, admin);
        sbLogger.LogInformation("Запрос на доработку для {SerialNumber} от админа {Admin}", serialNumber, admin);
        return await ExecuteReworkAsync(request, ct);
    }

    private OperationStartRequest BuildRequest(string serialNumber, string operatorName, string admin)
    {
        return new OperationStartRequest
        {
            SerialNumber = serialNumber,
            StationName = appSettingsService.NameStation,
            Operator = operatorName,
            Admin = admin
        };
    }

    private async Task<OperationStartResult> ExecuteStartAsync(OperationStartRequest request, CancellationToken ct)
    {
        try
        {
            return await SendStartRequestAsync(request, ct);
        }
        catch (Exception ex)
        {
            return HandleException(ex, request.SerialNumber, ct);
        }
    }

    private async Task<OperationStartResult> ExecuteReworkAsync(OperationStartRequest request, CancellationToken ct)
    {
        try
        {
            return await SendReworkRequestAsync(request, ct);
        }
        catch (Exception ex)
        {
            return HandleException(ex, request.SerialNumber, ct);
        }
    }

    private async Task<OperationStartResult> SendStartRequestAsync(OperationStartRequest request, CancellationToken ct)
    {
        using var response = await httpClient.PostWithResponseAsync(StartEndpoint, request, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<OperationStartResult> SendReworkRequestAsync(OperationStartRequest request, CancellationToken ct)
    {
        using var response = await httpClient.PostWithResponseAsync(ReworkEndpoint, request, ct);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            logger.LogInformation("Rework approved for {SerialNumber}", request.SerialNumber);
            sbLogger.LogInformation("Доработка одобрена для {SerialNumber}", request.SerialNumber);
            return OperationStartResult.Success(new OperationStartResponse());
        }
        return await HandleErrorResponseAsync(response, ct);
    }

    private async Task<OperationStartResult> ProcessResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await HandleSuccessAsync(response, ct),
            HttpStatusCode.Forbidden => await HandleForbiddenAsync(response, ct),
            HttpStatusCode.NotFound => await HandleNotFoundAsync(response, ct),
            _ => await HandleErrorResponseAsync(response, ct)
        };
    }

    private async Task<OperationStartResult> HandleSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var data = await response.Content.ReadFromJsonAsync<OperationStartResponse>(JsonOptions, ct);
        if (data == null)
        {
            return OperationStartResult.Fail("Пустой ответ от сервера");
        }
        logger.LogInformation("Operation started successfully, {RecipeCount} recipes loaded",
            data.Recipes.Count);
        sbLogger.LogInformation("Операция запущена успешно, загружено {RecipeCount} рецептов",
            data.Recipes.Count);
        return OperationStartResult.Success(data);
    }

    private async Task<OperationStartResult> HandleForbiddenAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var errorMessage = await TryParseErrorAsync(response, ct);
        logger.LogWarning("Operation forbidden: {Error}", errorMessage);
        sbLogger.LogWarning("Операция запрещена: {Error}", errorMessage);
        return OperationStartResult.NeedRework(errorMessage);
    }

    private async Task<OperationStartResult> HandleNotFoundAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var errorMessage = await TryParseErrorAsync(response, ct);
        logger.LogWarning("Operation not found: {Error}", errorMessage);
        sbLogger.LogWarning("Операция не найдена: {Error}", errorMessage);
        return OperationStartResult.Fail(errorMessage);
    }

    private async Task<OperationStartResult> HandleErrorResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var errorMessage = await TryParseErrorAsync(response, ct);
        logger.LogError("Operation failed with status {StatusCode}: {Error}",
            response.StatusCode, errorMessage);
        sbLogger.LogError(null, "Ошибка операции со статусом {StatusCode}: {Error}",
            response.StatusCode, errorMessage);
        return OperationStartResult.Fail(errorMessage);
    }

    private async Task<string> TryParseErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<OperationErrorResponse>(JsonOptions, ct);
            if (!string.IsNullOrEmpty(errorResponse?.Error))
            {
                return errorResponse.Error;
            }
            if (!string.IsNullOrEmpty(errorResponse?.Message))
            {
                return errorResponse.Message;
            }
            return "Неизвестная ошибка";
        }
        catch
        {
            return "Ошибка на стороне сервера";
        }
    }

    private OperationStartResult HandleException(Exception ex, string serialNumber, CancellationToken ct)
    {
        return ex switch
        {
            _ when ct.IsCancellationRequested => HandleCancellation(),
            TaskCanceledException => HandleTimeout(serialNumber),
            HttpRequestException httpEx => HandleConnectionError(httpEx, serialNumber),
            _ => HandleUnexpectedError(ex, serialNumber)
        };
    }

    private OperationStartResult HandleCancellation()
    {
        logger.LogInformation("Operation cancelled");
        sbLogger.LogInformation("Операция отменена");
        return OperationStartResult.Fail("Операция отменена");
    }

    private OperationStartResult HandleTimeout(string serialNumber)
    {
        logger.LogWarning("Operation timed out for {SerialNumber}", serialNumber);
        sbLogger.LogWarning("Таймаут операции для {SerialNumber}", serialNumber);
        return OperationStartResult.Fail("Таймаут соединения с сервером");
    }

    private OperationStartResult HandleConnectionError(HttpRequestException ex, string serialNumber)
    {
        logger.LogError(ex, "No connection to server for {SerialNumber}", serialNumber);
        sbLogger.LogError(ex, "Нет соединения с сервером для {SerialNumber}", serialNumber);
        return OperationStartResult.Fail("Нет соединения с сервером");
    }

    private OperationStartResult HandleUnexpectedError(Exception ex, string serialNumber)
    {
        logger.LogError(ex, "Operation failed for {SerialNumber}", serialNumber);
        sbLogger.LogError(ex, "Ошибка операции для {SerialNumber}", serialNumber);
        return OperationStartResult.Fail("Ошибка на стороне сервера");
    }
}
