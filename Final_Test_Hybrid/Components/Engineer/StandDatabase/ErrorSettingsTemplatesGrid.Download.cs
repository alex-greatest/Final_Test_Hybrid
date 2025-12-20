using Final_Test_Hybrid.Components.Engineer.StandDatabase.Modals;
using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.SpringBoot.ErrorSettings;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase;

public partial class ErrorSettingsTemplatesGrid : IAsyncDisposable
{
    [Inject]
    public required ErrorSettingsDownloadService ErrorSettingsDownloadService { get; set; }
    [Inject]
    public required StepFinalTestService StepFinalTestService { get; set; }
    private CancellationTokenSource? _downloadCts;
    private bool _isDownloading;

    private async Task DownloadFromServer()
    {
        if (_isDownloading)
        {
            return;
        }
        _isDownloading = true;
        try
        {
            await ExecuteDownloadAsync();
        }
        finally
        {
            _isDownloading = false;
        }
    }

    private async Task ExecuteDownloadAsync()
    {
        if (_downloadCts != null)
        {
            await _downloadCts.CancelAsync();
            _downloadCts.Dispose();
        }
        _downloadCts = new CancellationTokenSource();
        try
        {
            await PerformDownloadWithProgressAsync();
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    private async Task PerformDownloadWithProgressAsync()
    {
        _ = ShowProgressDialog();
        DownloadResultInfo result;
        try
        {
            result = await DownloadAndProcessAsync(_downloadCts!.Token);
        }
        finally
        {
            CloseProgressDialog();
        }
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
            { "Message", "Загрузка настроек ошибок..." },
            { "OnCancelRequested", EventCallback.Factory.Create(this, OnCancelDownload) }
        };
    }

    private static DialogOptions CreateProgressDialogOptions()
    {
        return new DialogOptions
        {
            Width = "600px",
            Height = "350px",
            CloseDialogOnOverlayClick = false,
            ShowClose = false
        };
    }

    private void OnCancelDownload() => _downloadCts?.Cancel();

    private void CloseProgressDialog() => DialogService.Close();

    private async Task<DownloadResultInfo> DownloadAndProcessAsync(CancellationToken ct)
    {
        var downloadResult = await ErrorSettingsDownloadService.DownloadAsync(ct);
        return await ConvertToDownloadResultAsync(downloadResult, ct);
    }

    private async Task<DownloadResultInfo> ConvertToDownloadResultAsync(
        ErrorSettingsDownloadResult downloadResult,
        CancellationToken ct)
    {
        if (!downloadResult.IsSuccess)
        {
            return CreateFailResult(downloadResult.ErrorMessage);
        }
        return await ProcessDownloadedItemsAsync(downloadResult.Items, ct);
    }

    private async Task<DownloadResultInfo> ProcessDownloadedItemsAsync(
        List<ErrorSettingsResponseDto> items,
        CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return CreateFailResult("Настройки ошибок не найдены на сервере");
        }
        return await SaveToDatabaseAsync(items, ct);
    }

    private async Task<DownloadResultInfo> SaveToDatabaseAsync(
        List<ErrorSettingsResponseDto> items,
        CancellationToken ct)
    {
        try
        {
            var entities = await MapToEntitiesAsync(items, ct);
            await ErrorSettingsTemplateService.ReplaceAllAsync(entities, ct);
            return new DownloadResultInfo(true, null, items.Count);
        }
        catch (Exception ex)
        {
            return HandleSaveException(ex);
        }
    }

    private async Task<List<ErrorSettingsTemplate>> MapToEntitiesAsync(
        List<ErrorSettingsResponseDto> dtos,
        CancellationToken ct)
    {
        var stepIdByName = await ResolveUniqueStepsAsync(dtos, ct);
        return dtos.Select(dto => CreateEntity(dto, stepIdByName)).ToList();
    }

    private async Task<Dictionary<string, long>> ResolveUniqueStepsAsync(
        List<ErrorSettingsResponseDto> dtos,
        CancellationToken ct)
    {
        var uniqueNames = dtos
            .Select(d => d.StepName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .ToList();

        var result = new Dictionary<string, long>();
        foreach (var name in uniqueNames)
        {
            var step = await StepFinalTestService.GetOrCreateByNameAsync(name!, ct);
            result[name!] = step.Id;
        }
        return result;
    }

    private static ErrorSettingsTemplate CreateEntity(
        ErrorSettingsResponseDto dto,
        Dictionary<string, long> stepIdByName)
    {
        return new ErrorSettingsTemplate
        {
            StepId = string.IsNullOrWhiteSpace(dto.StepName)
                ? null
                : stepIdByName.GetValueOrDefault(dto.StepName),
            AddressError = dto.AddressError,
            Description = dto.Description
        };
    }

    private DownloadResultInfo HandleSaveException(Exception ex)
    {
        return ex switch
        {
            OperationCanceledException => CreateFailResult("Операция отменена"),
            InvalidOperationException invalidEx => LogAndCreateFailResult(invalidEx),
            _ => LogAndCreateFailResult(ex)
        };
    }

    private DownloadResultInfo LogAndCreateFailResult(Exception ex)
    {
        Logger.LogError(ex, "Failed to replace error settings");
        return CreateFailResult(ex.Message);
    }

    private static DownloadResultInfo CreateFailResult(string? message) =>
        new(false, message ?? "Ошибка загрузки");

    private Task HandleDownloadResultAsync(DownloadResultInfo result)
    {
        return result.IsSuccess
            ? OnDownloadSuccessAsync(result.ItemCount)
            : ShowDownloadErrorAsync(result.ErrorMessage);
    }

    private Task ShowDownloadErrorAsync(string? errorMessage)
    {
        ShowError(errorMessage ?? "Неизвестная ошибка");
        return Task.CompletedTask;
    }

    private async Task OnDownloadSuccessAsync(int itemCount)
    {
        ShowSuccess($"Загружено настроек: {itemCount}");
        await LoadDataAsync();
    }

    private record DownloadResultInfo(bool IsSuccess, string? ErrorMessage, int ItemCount = 0);

    public async ValueTask DisposeAsync()
    {
        if (_downloadCts == null)
        {
            return;
        }
        await _downloadCts.CancelAsync();
        _downloadCts.Dispose();
        _downloadCts = null;
    }
}
