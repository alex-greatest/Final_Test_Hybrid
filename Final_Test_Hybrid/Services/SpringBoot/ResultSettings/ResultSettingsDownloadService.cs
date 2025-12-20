using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Settings.Spring;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.SpringBoot.ResultSettings;

public class ResultSettingsDownloadResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public List<ResultSettingsResponseDto> Items { get; init; } = [];

    public static ResultSettingsDownloadResult Success(List<ResultSettingsResponseDto> items) =>
        new() { IsSuccess = true, Items = items };

    public static ResultSettingsDownloadResult Fail(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}

public class ResultSettingsDownloadService(
    SpringBootHttpClient httpClient,
    AppSettingsService appSettingsService,
    ILogger<ResultSettingsDownloadService> logger,
    ISpringBootLogger sbLogger)
{
    private const string Endpoint = "/api/result-settings";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<ResultSettingsDownloadResult> DownloadAsync(string article, CancellationToken ct = default)
    {
        var url = BuildRequestUrl(article);
        logger.LogInformation("Downloading result settings from {Url}", url);
        sbLogger.LogInformation("Загрузка настроек результата из {Url}", url);
        return await ExecuteDownloadAsync(url, article, ct);
    }

    private string BuildRequestUrl(string article)
    {
        var stationName = appSettingsService.NameStation;
        return $"{Endpoint}?stationName={Uri.EscapeDataString(stationName)}&article={Uri.EscapeDataString(article)}";
    }

    private async Task<ResultSettingsDownloadResult> ExecuteDownloadAsync(string url, string article, CancellationToken ct)
    {
        try
        {
            return await SendRequestAsync(url, ct);
        }
        catch (Exception ex)
        {
            return HandleException(ex, article, ct);
        }
    }

    private ResultSettingsDownloadResult HandleException(Exception ex, string article, CancellationToken ct)
    {
        return ex switch
        {
            _ when ct.IsCancellationRequested => HandleCancellation(),
            TaskCanceledException => HandleTimeout(article),
            HttpRequestException httpEx => HandleConnectionError(httpEx, article),
            _ => HandleUnexpectedError(ex, article)
        };
    }

    private ResultSettingsDownloadResult HandleCancellation()
    {
        logger.LogInformation("Result settings download cancelled");
        sbLogger.LogInformation("Загрузка настроек результата отменена");
        return ResultSettingsDownloadResult.Fail("Операция отменена");
    }

    private ResultSettingsDownloadResult HandleTimeout(string article)
    {
        logger.LogWarning("Result settings download timed out for article {Article}", article);
        sbLogger.LogWarning("Таймаут загрузки настроек результата для артикула {Article}", article);
        return ResultSettingsDownloadResult.Fail("Таймаут соединения с сервером");
    }

    private ResultSettingsDownloadResult HandleConnectionError(HttpRequestException ex, string article)
    {
        logger.LogError(ex, "No connection to server for article {Article}", article);
        sbLogger.LogError(ex, "Нет соединения с сервером для артикула {Article}", article);
        return ResultSettingsDownloadResult.Fail("Нет соединения с сервером");
    }

    private ResultSettingsDownloadResult HandleUnexpectedError(Exception ex, string article)
    {
        logger.LogError(ex, "Result settings download failed for article {Article}", article);
        sbLogger.LogError(ex, "Ошибка загрузки настроек результата для артикула {Article}", article);
        return ResultSettingsDownloadResult.Fail("Ошибка на стороне сервера");
    }

    private async Task<ResultSettingsDownloadResult> SendRequestAsync(string url, CancellationToken ct)
    {
        using var response = await httpClient.GetWithResponseAsync(url, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ResultSettingsDownloadResult> ProcessResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await HandleSuccessAsync(response, ct),
            HttpStatusCode.NotFound => await HandleNotFoundAsync(response, ct),
            _ => HandleUnexpectedStatus(response.StatusCode)
        };
    }

    private async Task<ResultSettingsDownloadResult> HandleSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var items = await response.Content.ReadFromJsonAsync<List<ResultSettingsResponseDto>>(JsonOptions, ct) ?? [];
        logger.LogInformation("Downloaded {Count} result settings", items.Count);
        sbLogger.LogInformation("Загружено {Count} настроек результата", items.Count);
        return ResultSettingsDownloadResult.Success(items);
    }

    private async Task<ResultSettingsDownloadResult> HandleNotFoundAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var errorMessage = await TryParseErrorMessageAsync(response, ct);
        logger.LogWarning("Result settings download 404: {Message}", errorMessage);
        sbLogger.LogWarning("Настройки результата не найдены: {Message}", errorMessage);
        return ResultSettingsDownloadResult.Fail(errorMessage);
    }

    private async Task<string> TryParseErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
            return errorResponse?.Message ?? "Настройки результатов не найдены";
        }
        catch
        {
            return "Настройки результатов не найдены";
        }
    }

    private ResultSettingsDownloadResult HandleUnexpectedStatus(HttpStatusCode statusCode)
    {
        logger.LogError("Unexpected status code {StatusCode} for result settings download", statusCode);
        return ResultSettingsDownloadResult.Fail("Ошибка на стороне сервера");
    }
}
