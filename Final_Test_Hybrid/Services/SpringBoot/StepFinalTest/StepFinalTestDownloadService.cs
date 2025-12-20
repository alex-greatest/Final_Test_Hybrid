using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Final_Test_Hybrid.Settings.Spring;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.SpringBoot.StepFinalTest;

public class StepFinalTestDownloadResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public List<StepFinalTestResponseDto> Items { get; init; } = [];

    public static StepFinalTestDownloadResult Success(List<StepFinalTestResponseDto> items) =>
        new() { IsSuccess = true, Items = items };

    public static StepFinalTestDownloadResult Fail(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}

public class StepFinalTestDownloadService(
    SpringBootHttpClient httpClient,
    ILogger<StepFinalTestDownloadService> logger)
{
    private const string Endpoint = "/api/steps";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<StepFinalTestDownloadResult> DownloadAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Downloading step final tests from {Endpoint}", Endpoint);
        return await ExecuteDownloadAsync(ct);
    }

    private async Task<StepFinalTestDownloadResult> ExecuteDownloadAsync(CancellationToken ct)
    {
        try
        {
            return await SendRequestAsync(ct);
        }
        catch (Exception ex)
        {
            return HandleException(ex, ct);
        }
    }

    private StepFinalTestDownloadResult HandleException(Exception ex, CancellationToken ct)
    {
        return ex switch
        {
            _ when ct.IsCancellationRequested => HandleCancellation(),
            TaskCanceledException => HandleTimeout(),
            HttpRequestException httpEx => HandleConnectionError(httpEx),
            _ => HandleUnexpectedError(ex)
        };
    }

    private StepFinalTestDownloadResult HandleCancellation()
    {
        logger.LogInformation("Step final tests download cancelled");
        return StepFinalTestDownloadResult.Fail("Операция отменена");
    }

    private StepFinalTestDownloadResult HandleTimeout()
    {
        logger.LogWarning("Step final tests download timed out");
        return StepFinalTestDownloadResult.Fail("Таймаут соединения с сервером");
    }

    private StepFinalTestDownloadResult HandleConnectionError(HttpRequestException ex)
    {
        logger.LogError(ex, "No connection to server for step final tests");
        return StepFinalTestDownloadResult.Fail("Нет соединения с сервером");
    }

    private StepFinalTestDownloadResult HandleUnexpectedError(Exception ex)
    {
        logger.LogError(ex, "Step final tests download failed");
        return StepFinalTestDownloadResult.Fail("Ошибка на стороне сервера");
    }

    private async Task<StepFinalTestDownloadResult> SendRequestAsync(CancellationToken ct)
    {
        using var response = await httpClient.GetWithResponseAsync(Endpoint, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<StepFinalTestDownloadResult> ProcessResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await HandleSuccessAsync(response, ct),
            HttpStatusCode.NotFound => HandleNotFound(),
            _ => HandleUnexpectedStatus(response.StatusCode)
        };
    }

    private async Task<StepFinalTestDownloadResult> HandleSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var items = await response.Content.ReadFromJsonAsync<List<StepFinalTestResponseDto>>(JsonOptions, ct) ?? [];
        logger.LogInformation("Downloaded {Count} step final tests", items.Count);
        return StepFinalTestDownloadResult.Success(items);
    }

    private StepFinalTestDownloadResult HandleNotFound()
    {
        logger.LogWarning("Step final tests not found on server");
        return StepFinalTestDownloadResult.Fail("Шаги финального теста не найдены");
    }

    private StepFinalTestDownloadResult HandleUnexpectedStatus(HttpStatusCode statusCode)
    {
        logger.LogError("Unexpected status code {StatusCode} for step final tests download", statusCode);
        return StepFinalTestDownloadResult.Fail("Ошибка на стороне сервера");
    }
}
