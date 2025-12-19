using Final_Test_Hybrid.Components.Engineer.StandDatabase.Modals;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase.Recipe;

public partial class RecipesGrid
{
    [Inject] public required RecipeDownloadService RecipeDownloadService { get; set; }

    private CancellationTokenSource? _downloadCts;
    private bool _isDownloading;

    private async Task DownloadRecipesFromServer()
    {
        if (!CanStartDownload())
        {
            return;
        }
        _isDownloading = true;
        try
        {
            await ExecuteDownloadAsync(GetSelectedBoilerTypeArticle()!);
        }
        finally
        {
            _isDownloading = false;
        }
    }

    private bool CanStartDownload()
    {
        if (!CanDownload())
        {
            return false;
        }

        return ValidateArticle(GetSelectedBoilerTypeArticle());
    }

    private bool CanDownload() => _selectedBoilerTypeId.HasValue && !_isDownloading;

    private bool ValidateArticle(string? article)
    {
        if (!string.IsNullOrEmpty(article))
        {
            return true;
        }
        ShowError("Не удалось определить артикул типа котла");
        return false;

    }

    private string? GetSelectedBoilerTypeArticle()
    {
        var boilerType = FindBoilerType(_selectedBoilerTypeId!.Value);
        return boilerType?.Article;
    }

    private async Task ExecuteDownloadAsync(string article)
    {
        _downloadCts = new CancellationTokenSource();
        try
        {
            await PerformDownloadWithProgressAsync(article);
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    private async Task PerformDownloadWithProgressAsync(string article)
    {
        _ = ShowProgressDialog();
        var result = await DownloadAndProcessRecipesAsync(article, _downloadCts!.Token);
        CloseProgressDialog();
        await HandleDownloadResultAsync(result);
    }

    private Task ShowProgressDialog()
    {
        return DialogService.OpenAsync<DownloadProgressDialog>(
            "Загрузка",
            CreateProgressDialogParameters(),
            CreateProgressDialogOptions());
    }

    private Dictionary<string, object> CreateProgressDialogParameters()
    {
        return new Dictionary<string, object>
        {
            { "OnCancelRequested", EventCallback.Factory.Create(this, OnCancelDownload) }
        };
    }

    private static DialogOptions CreateProgressDialogOptions()
    {
        return new DialogOptions
        {
            Width = "600px",
            Height = "300px",
            CloseDialogOnOverlayClick = false,
            ShowClose = false
        };
    }

    private void OnCancelDownload() => _downloadCts?.Cancel();

    private void CloseProgressDialog() => DialogService.Close();

    private async Task<DownloadResult> DownloadAndProcessRecipesAsync(string article, CancellationToken ct)
    {
        var downloadResult = await RecipeDownloadService.DownloadRecipesAsync(article, ct);
        return await ConvertToDownloadResultAsync(downloadResult, ct);
    }

    private async Task<DownloadResult> ConvertToDownloadResultAsync(RecipeDownloadResult downloadResult, CancellationToken ct)
    {
        if (!downloadResult.IsSuccess)
        {
            return CreateFailResult(downloadResult.ErrorMessage);
        }

        return await ProcessDownloadedRecipesAsync(downloadResult.Recipes, ct);
    }

    private async Task<DownloadResult> ProcessDownloadedRecipesAsync(List<RecipeResponseDto> recipes, CancellationToken ct)
    {
        if (recipes.Count == 0)
        {
            return CreateFailResult("Рецепты не найдены на сервере");
        }

        return await SaveRecipesToDatabaseAsync(recipes, ct);
    }

    private async Task<DownloadResult> SaveRecipesToDatabaseAsync(List<RecipeResponseDto> recipes, CancellationToken ct)
    {
        try
        {
            var entities = MapToEntities(recipes);
            await RecipeService.ReplaceRecipesForBoilerTypeAsync(_selectedBoilerTypeId!.Value, entities, ct);
            return new DownloadResult(true, null, recipes.Count);
        }
        catch (Exception ex)
        {
            return HandleSaveException(ex);
        }
    }

    private DownloadResult HandleSaveException(Exception ex)
    {
        return ex switch
        {
            OperationCanceledException => CreateFailResult("Операция отменена"),
            InvalidOperationException invalidEx => LogAndCreateFailResult(invalidEx),
            _ => LogAndCreateFailResult(ex)
        };
    }

    private DownloadResult LogAndCreateFailResult(Exception ex)
    {
        Logger.LogError(ex, "Failed to replace recipes");
        return CreateFailResult(ex.Message);
    }

    private static DownloadResult CreateFailResult(string? message) =>
        new(false, message ?? "Ошибка загрузки");

    private static List<Models.Database.Recipe> MapToEntities(List<RecipeResponseDto> dtos)
    {
        return dtos.Select(MapToEntity).ToList();
    }

    private static Models.Database.Recipe MapToEntity(RecipeResponseDto dto)
    {
        return new Models.Database.Recipe
        {
            TagName = dto.TagName,
            Value = dto.Value,
            Address = dto.Address,
            PlcType = dto.PlcType,
            IsPlc = dto.IsPlc,
            Unit = dto.Unit,
            Description = dto.Description
        };
    }

    private Task HandleDownloadResultAsync(DownloadResult result)
    {
        return result.IsSuccess
            ? OnDownloadSuccessAsync(result.RecipeCount)
            : ShowDownloadErrorAsync(result.ErrorMessage);
    }

    private Task ShowDownloadErrorAsync(string? errorMessage)
    {
        ShowDownloadError(errorMessage);
        return Task.CompletedTask;
    }

    private async Task OnDownloadSuccessAsync(int recipeCount)
    {
        ShowSuccess($"Загружено рецептов: {recipeCount}");
        await LoadDataAsync();
    }

    private void ShowDownloadError(string? errorMessage) =>
        ShowError(errorMessage ?? "Неизвестная ошибка");

    private record DownloadResult(bool IsSuccess, string? ErrorMessage, int RecipeCount = 0);
}
