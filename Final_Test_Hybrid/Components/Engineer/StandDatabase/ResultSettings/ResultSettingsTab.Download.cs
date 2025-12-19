using Final_Test_Hybrid.Components.Engineer.StandDatabase.Modals;
using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.SpringBoot.ResultSettings;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase.ResultSettings;

public partial class ResultSettingsTab
{
    [Inject] public required ResultSettingsDownloadService ResultSettingsDownloadService { get; set; }

    private CancellationTokenSource? _downloadCts;
    private bool _isDownloading;

    private async Task DownloadFromServer()
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
        return CanDownload() && ValidateArticle(GetSelectedBoilerTypeArticle());
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

    private BoilerType? FindBoilerType(long id) =>
        _boilerTypes.FirstOrDefault(b => b.Id == id);

    private async Task ExecuteDownloadAsync(string article)
    {
        if (_downloadCts != null)
        {
            await _downloadCts.CancelAsync();
            _downloadCts.Dispose();
        }
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
        DownloadResultInfo result;
        try
        {
            result = await DownloadAndProcessAsync(article, _downloadCts!.Token);
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
            { "Message", "Загрузка результатов..." },
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

    private async Task<DownloadResultInfo> DownloadAndProcessAsync(string article, CancellationToken ct)
    {
        var downloadResult = await ResultSettingsDownloadService.DownloadAsync(article, ct);
        return await ConvertToDownloadResultAsync(downloadResult, ct);
    }

    private async Task<DownloadResultInfo> ConvertToDownloadResultAsync(
        ResultSettingsDownloadResult downloadResult,
        CancellationToken ct)
    {
        if (!downloadResult.IsSuccess)
        {
            return CreateFailResult(downloadResult.ErrorMessage);
        }
        return await ProcessDownloadedItemsAsync(downloadResult.Items, ct);
    }

    private async Task<DownloadResultInfo> ProcessDownloadedItemsAsync(
        List<ResultSettingsResponseDto> items,
        CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return CreateFailResult("Настройки результатов не найдены на сервере");
        }
        return await SaveToDatabaseAsync(items, ct);
    }

    private async Task<DownloadResultInfo> SaveToDatabaseAsync(
        List<ResultSettingsResponseDto> items,
        CancellationToken ct)
    {
        try
        {
            var entities = MapToEntities(items);
            await ResultSettingsService.ReplaceForBoilerTypeAsync(_selectedBoilerTypeId!.Value, entities, ct);
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
        Logger.LogError(ex, "Failed to replace result settings");
        return CreateFailResult(ex.Message);
    }

    private static DownloadResultInfo CreateFailResult(string? message) =>
        new(false, message ?? "Ошибка загрузки");

    private static List<Models.Database.ResultSettings> MapToEntities(List<ResultSettingsResponseDto> dtos)
    {
        return dtos.Select(MapToEntity).ToList();
    }

    private static Models.Database.ResultSettings MapToEntity(ResultSettingsResponseDto dto)
    {
        return new Models.Database.ResultSettings
        {
            ParameterName = dto.ParameterName,
            AddressValue = dto.AddressValue,
            AddressMin = dto.AddressMin,
            AddressMax = dto.AddressMax,
            AddressStatus = dto.AddressStatus,
            PlcType = dto.PlcType,
            Nominal = dto.Nominal,
            Unit = dto.Unit,
            Description = dto.Description,
            AuditType = dto.ParseAuditType()
        };
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
        ShowSuccess($"Загружено настроек: {itemCount}");
        await LoadDataAsync();
    }

    private void ShowSuccess(string message) =>
        NotificationService.Notify(NotificationSeverity.Success, "Успех", message);

    private void ShowError(string message) =>
        NotificationService.Notify(NotificationSeverity.Error, "Ошибка", message);

    private record DownloadResultInfo(bool IsSuccess, string? ErrorMessage, int ItemCount = 0);
}
