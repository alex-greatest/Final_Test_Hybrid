using Final_Test_Hybrid.Components.Engineer.StandDatabase.Modals;
using Final_Test_Hybrid.Models.Database;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase.ResultSettings;

public partial class ResultSettingsTab
{
    [Inject] public required DialogService DialogService { get; set; }
    [Inject] public required NotificationService NotificationService { get; set; }

    private async Task CopySelectedItems()
    {
        if (!CanCopy())
        {
            return;
        }
        await TryCopyToTargetAsync();
    }

    private bool CanCopy() =>
        _selectedBoilerTypeId.HasValue && TotalSelectedCount > 0;

    private async Task TryCopyToTargetAsync()
    {
        var targetId = await ShowCopyDialogAsync();
        if (!targetId.HasValue)
        {
            return;
        }
        await ExecuteCopyAsync(targetId.Value);
    }

    private async Task<long?> ShowCopyDialogAsync()
    {
        var result = await DialogService.OpenAsync<CopyResultSettingsDialog>(
            "Копировать настройки",
            new Dictionary<string, object>
            {
                { "CurrentBoilerTypeId", _selectedBoilerTypeId!.Value },
                { "BoilerTypes", _boilerTypes }
            },
            new DialogOptions { Width = "500px", CloseDialogOnOverlayClick = false });
        return result as long?;
    }

    private async Task ExecuteCopyAsync(long targetBoilerTypeId)
    {
        var allSelected = GetAllSelectedItems();
        var entities = allSelected.Select(r => r.ToEntity()).ToList();
        var failedItems = await ResultSettingsService.CopyToBoilerTypeAsync(entities, targetBoilerTypeId);
        ClearSelections();
        await LoadDataAsync();
        await ShowCopyResultsAsync(entities.Count - failedItems.Count, failedItems);
    }

    private List<Models.Database.Edit.ResultSettingsEditModel> GetAllSelectedItems()
    {
        var result = new List<Models.Database.Edit.ResultSettingsEditModel>();
        result.AddRange(_selectedRange);
        result.AddRange(_selectedSimple);
        result.AddRange(_selectedBoard);
        return result;
    }

    private void ClearSelections()
    {
        _selectedRange = [];
        _selectedSimple = [];
        _selectedBoard = [];
    }

    private async Task ShowCopyResultsAsync(int copiedCount, List<string> failedItems)
    {
        ShowSuccessIfAny(copiedCount);
        await ShowFailedDialogIfAny(failedItems);
    }

    private void ShowSuccessIfAny(int copiedCount)
    {
        if (copiedCount > 0)
        {
            NotificationService.Notify(
                NotificationSeverity.Success,
                "Успех",
                $"Скопировано {copiedCount} {RussianPluralization.GetResultSettingWord(copiedCount)}");
        }
    }

    private async Task ShowFailedDialogIfAny(List<string> failedItems)
    {
        if (failedItems.Count == 0)
        {
            return;
        }
        await DialogService.OpenAsync<FailedCopiesDialog>(
            "Ошибки копирования",
            new Dictionary<string, object>
            {
                { "FailedItems", failedItems },
                { "ItemWord", RussianPluralization.GetResultSettingWord(failedItems.Count) },
                { "ColumnTitle", "Название параметра" }
            },
            new DialogOptions { Width = "500px", Height = "400px", CloseDialogOnOverlayClick = false });
    }
}
