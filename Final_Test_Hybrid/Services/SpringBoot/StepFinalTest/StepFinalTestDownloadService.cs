using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Settings.Spring;

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
    DualLogger<StepFinalTestDownloadService> logger)
{
    private const string Endpoint = "/api/steps";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<StepFinalTestDownloadResult> DownloadAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Загрузка шагов финального теста из {Endpoint}", Endpoint);
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
        logger.LogInformation("Загрузка шагов финального теста отменена");
        return StepFinalTestDownloadResult.Fail("Операция отменена");
    }

    private StepFinalTestDownloadResult HandleTimeout()
    {
        logger.LogWarning("Таймаут загрузки шагов финального теста");
        return StepFinalTestDownloadResult.Fail("Нет ответа от сервера");
    }

    private StepFinalTestDownloadResult HandleConnectionError(HttpRequestException ex)
    {
        logger.LogError(ex, "Нет соединения с сервером для шагов финального теста");
        return StepFinalTestDownloadResult.Fail("Нет соединения с сервером");
    }

    private StepFinalTestDownloadResult HandleUnexpectedError(Exception ex)
    {
        logger.LogError(ex, "Ошибка загрузки шагов финального теста");
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
        logger.LogInformation("Загружено {Count} шагов финального теста", items.Count);
        return StepFinalTestDownloadResult.Success(items);
    }

    private StepFinalTestDownloadResult HandleNotFound()
    {
        logger.LogWarning("Шаги финального теста не найдены на сервере");
        return StepFinalTestDownloadResult.Fail("Шаги финального теста не найдены");
    }

    private StepFinalTestDownloadResult HandleUnexpectedStatus(HttpStatusCode statusCode)
    {
        logger.LogError("Неожиданный код статуса {StatusCode} при загрузке шагов финального теста", statusCode);
        return StepFinalTestDownloadResult.Fail("Ошибка на стороне сервера");
    }
}
