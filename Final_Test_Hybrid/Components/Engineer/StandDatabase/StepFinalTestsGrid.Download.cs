using Final_Test_Hybrid.Components.Engineer.StandDatabase.Modals;
using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.SpringBoot.StepFinalTest;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase;

public partial class StepFinalTestsGrid
{
    [Inject] public required StepFinalTestDownloadService StepFinalTestDownloadService { get; set; }
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
            { "Message", "Загрузка шагов теста..." },
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
        var downloadResult = await StepFinalTestDownloadService.DownloadAsync(ct);
        return await ConvertToDownloadResultAsync(downloadResult, ct);
    }

    private async Task<DownloadResultInfo> ConvertToDownloadResultAsync(
        StepFinalTestDownloadResult downloadResult,
        CancellationToken ct)
    {
        if (!downloadResult.IsSuccess)
        {
            return CreateFailResult(downloadResult.ErrorMessage);
        }
        return await ProcessDownloadedItemsAsync(downloadResult.Items, ct);
    }

    private async Task<DownloadResultInfo> ProcessDownloadedItemsAsync(
        List<StepFinalTestResponseDto> items,
        CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return CreateFailResult("Шаги теста не найдены на сервере");
        }
        return await SaveToDatabaseAsync(items, ct);
    }

    private async Task<DownloadResultInfo> SaveToDatabaseAsync(
        List<StepFinalTestResponseDto> items,
        CancellationToken ct)
    {
        try
        {
            var entities = MapToEntities(items);
            await StepFinalTestService.ReplaceAllAsync(entities, ct);
            return new DownloadResultInfo(true, null, items.Count);
        }
        catch (Exception ex)
        {
            return HandleSaveException(ex);
        }
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
        Logger.LogError(ex, "Failed to replace step final tests");
        return CreateFailResult(ex.Message);
    }

    private static DownloadResultInfo CreateFailResult(string? message) =>
        new(false, message ?? "Ошибка загрузки");

    private static List<StepFinalTest> MapToEntities(List<StepFinalTestResponseDto> dtos)
    {
        return dtos.Select(MapToEntity).ToList();
    }

    private static StepFinalTest MapToEntity(StepFinalTestResponseDto dto)
    {
        return new StepFinalTest { Name = dto.Name };
    }

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
        ShowSuccess($"Загружено шагов: {itemCount}");
        await LoadDataAsync();
        await OnDataChanged.InvokeAsync();
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
