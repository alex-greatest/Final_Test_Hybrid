using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Settings.Spring;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.SpringBoot.Recipe;

public class RecipeDownloadResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public List<RecipeResponseDto> Recipes { get; init; } = [];

    public static RecipeDownloadResult Success(List<RecipeResponseDto> recipes) =>
        new() { IsSuccess = true, Recipes = recipes };

    public static RecipeDownloadResult Fail(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}

public class RecipeDownloadService(
    SpringBootHttpClient httpClient,
    AppSettingsService appSettingsService,
    ILogger<RecipeDownloadService> logger,
    ISpringBootLogger sbLogger)
{
    private const string Endpoint = "/api/recipes";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<RecipeDownloadResult> DownloadRecipesAsync(string article, CancellationToken ct = default)
    {
        var url = BuildRequestUrl(article);
        logger.LogInformation("Downloading recipes from {Url}", url);
        sbLogger.LogInformation("Загрузка рецептов из {Url}", url);
        return await ExecuteDownloadAsync(url, article, ct);
    }

    private string BuildRequestUrl(string article)
    {
        var stationName = appSettingsService.NameStation;
        return $"{Endpoint}?stationName={Uri.EscapeDataString(stationName)}&article={Uri.EscapeDataString(article)}";
    }

    private async Task<RecipeDownloadResult> ExecuteDownloadAsync(string url, string article, CancellationToken ct)
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

    private RecipeDownloadResult HandleException(Exception ex, string article, CancellationToken ct)
    {
        return ex switch
        {
            _ when ct.IsCancellationRequested => HandleCancellation(),
            TaskCanceledException => HandleTimeout(article),
            HttpRequestException httpEx => HandleConnectionError(httpEx, article),
            _ => HandleUnexpectedError(ex, article)
        };
    }

    private RecipeDownloadResult HandleCancellation()
    {
        logger.LogInformation("Recipe download cancelled");
        sbLogger.LogInformation("Загрузка рецептов отменена");
        return RecipeDownloadResult.Fail("Операция отменена");
    }

    private RecipeDownloadResult HandleTimeout(string article)
    {
        logger.LogWarning("Recipe download timed out for article {Article}", article);
        sbLogger.LogWarning("Таймаут загрузки рецептов для артикула {Article}", article);
        return RecipeDownloadResult.Fail("Таймаут соединения с сервером");
    }

    private RecipeDownloadResult HandleConnectionError(HttpRequestException ex, string article)
    {
        logger.LogError(ex, "No connection to server for article {Article}", article);
        sbLogger.LogError(ex, "Нет соединения с сервером для артикула {Article}", article);
        return RecipeDownloadResult.Fail("Нет соединения с сервером");
    }

    private RecipeDownloadResult HandleUnexpectedError(Exception ex, string article)
    {
        logger.LogError(ex, "Recipe download failed for article {Article}", article);
        sbLogger.LogError(ex, "Ошибка загрузки рецептов для артикула {Article}", article);
        return RecipeDownloadResult.Fail("Ошибка на стороне сервера");
    }

    private async Task<RecipeDownloadResult> SendRequestAsync(string url, CancellationToken ct)
    {
        using var response = await httpClient.GetWithResponseAsync(url, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<RecipeDownloadResult> ProcessResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await HandleSuccessAsync(response, ct),
            HttpStatusCode.NotFound => await HandleNotFoundAsync(response, ct),
            _ => HandleUnexpectedStatus(response.StatusCode)
        };
    }

    private async Task<RecipeDownloadResult> HandleSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var recipes = await response.Content.ReadFromJsonAsync<List<RecipeResponseDto>>(JsonOptions, ct) ?? [];
        logger.LogInformation("Downloaded {Count} recipes", recipes.Count);
        sbLogger.LogInformation("Загружено {Count} рецептов", recipes.Count);
        return RecipeDownloadResult.Success(recipes);
    }

    private async Task<RecipeDownloadResult> HandleNotFoundAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var errorMessage = await TryParseErrorMessageAsync(response, ct);
        logger.LogWarning("Recipe download 404: {Message}", errorMessage);
        sbLogger.LogWarning("Рецепты не найдены: {Message}", errorMessage);
        return RecipeDownloadResult.Fail(errorMessage);
    }

    private async Task<string> TryParseErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
            return errorResponse?.Message ?? "Рецепты не найдены";
        }
        catch
        {
            return "Рецепты не найдены";
        }
    }

    private RecipeDownloadResult HandleUnexpectedStatus(HttpStatusCode statusCode)
    {
        logger.LogError("Unexpected status code {StatusCode} for recipe download", statusCode);
        sbLogger.LogError(null, "Неожиданный код статуса {StatusCode} при загрузке рецептов", statusCode);
        return RecipeDownloadResult.Fail("Ошибка на стороне сервера");
    }
}
