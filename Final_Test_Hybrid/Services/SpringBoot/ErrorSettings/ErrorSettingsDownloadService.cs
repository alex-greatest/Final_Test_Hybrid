using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Settings.Spring;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.SpringBoot.ErrorSettings;

public class ErrorSettingsDownloadService(
    SpringBootHttpClient httpClient,
    AppSettingsService appSettingsService,
    ILogger<ErrorSettingsDownloadService> logger)
{
    private const string Endpoint = "/api/error-settings";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<ErrorSettingsDownloadResult> DownloadAsync(CancellationToken ct = default)
    {
        var url = BuildRequestUrl();
        logger.LogInformation("Downloading error settings from {Url}", url);
        return await ExecuteDownloadAsync(url, ct);
    }

    private string BuildRequestUrl()
    {
        var stationName = appSettingsService.NameStation;
        return $"{Endpoint}?stationName={Uri.EscapeDataString(stationName)}";
    }

    private async Task<ErrorSettingsDownloadResult> ExecuteDownloadAsync(string url, CancellationToken ct)
    {
        try
        {
            return await SendRequestAsync(url, ct);
        }
        catch (Exception ex)
        {
            return HandleException(ex, ct);
        }
    }

    private ErrorSettingsDownloadResult HandleException(Exception ex, CancellationToken ct)
    {
        return ex switch
        {
            _ when ct.IsCancellationRequested => HandleCancellation(),
            TaskCanceledException => HandleTimeout(),
            HttpRequestException httpEx => HandleConnectionError(httpEx),
            _ => HandleUnexpectedError(ex)
        };
    }

    private ErrorSettingsDownloadResult HandleCancellation()
    {
        logger.LogInformation("Error settings download cancelled");
        return ErrorSettingsDownloadResult.Fail("Операция отменена");
    }

    private ErrorSettingsDownloadResult HandleTimeout()
    {
        logger.LogWarning("Error settings download timed out");
        return ErrorSettingsDownloadResult.Fail("Таймаут соединения с сервером");
    }

    private ErrorSettingsDownloadResult HandleConnectionError(HttpRequestException ex)
    {
        logger.LogError(ex, "No connection to server for error settings");
        return ErrorSettingsDownloadResult.Fail("Нет соединения с сервером");
    }

    private ErrorSettingsDownloadResult HandleUnexpectedError(Exception ex)
    {
        logger.LogError(ex, "Error settings download failed");
        return ErrorSettingsDownloadResult.Fail("Ошибка на стороне сервера");
    }

    private async Task<ErrorSettingsDownloadResult> SendRequestAsync(string url, CancellationToken ct)
    {
        using var response = await httpClient.GetWithResponseAsync(url, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ErrorSettingsDownloadResult> ProcessResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await HandleSuccessAsync(response, ct),
            HttpStatusCode.NotFound => await HandleNotFoundAsync(response, ct),
            _ => HandleUnexpectedStatus(response.StatusCode)
        };
    }

    private async Task<ErrorSettingsDownloadResult> HandleSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var items = await response.Content.ReadFromJsonAsync<List<ErrorSettingsResponseDto>>(JsonOptions, ct) ?? [];
        logger.LogInformation("Downloaded {Count} error settings", items.Count);
        return ErrorSettingsDownloadResult.Success(items);
    }

    private async Task<ErrorSettingsDownloadResult> HandleNotFoundAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var errorMessage = await TryParseErrorMessageAsync(response, ct);
        logger.LogWarning("Error settings download 404: {Message}", errorMessage);
        return ErrorSettingsDownloadResult.Fail(errorMessage);
    }

    private async Task<string> TryParseErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
            return errorResponse?.Message ?? "Настройки ошибок не найдены";
        }
        catch
        {
            return "Настройки ошибок не найдены";
        }
    }

    private ErrorSettingsDownloadResult HandleUnexpectedStatus(HttpStatusCode statusCode)
    {
        logger.LogError("Unexpected status code {StatusCode} for error settings download", statusCode);
        return ErrorSettingsDownloadResult.Fail("Ошибка на стороне сервера");
    }
}
