using Final_Test_Hybrid.Components.Engineer.StandDatabase.Modals;
using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Database.Edit;
using Final_Test_Hybrid.Services.Database;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase;

public partial class ErrorSettingsTemplatesGrid
{
    [Inject]
    public required ErrorSettingsTemplateService ErrorSettingsTemplateService { get; set; }
    [Inject]
    public required ILogger<ErrorSettingsTemplatesGrid> Logger { get; set; }
    [Inject]
    public required NotificationService NotificationService { get; set; }
    [Inject]
    public required DialogService DialogService { get; set; }
    [Parameter]
    public EventCallback OnDataChanged { get; set; }
    private List<ErrorSettingsTemplateEditModel> _templates = [];
    private RadzenDataGrid<ErrorSettingsTemplateEditModel>? _grid;
    private bool _loadError;
    private ErrorSettingsTemplateEditModel? _itemToInsert;
    private ErrorSettingsTemplateEditModel? _originalItem;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            await LoadTemplatesFromDatabase();
        }
        catch (Exception ex)
        {
            HandleLoadError(ex);
        }
    }

    private async Task LoadTemplatesFromDatabase()
    {
        _loadError = false;
        var items = await ErrorSettingsTemplateService.GetAllAsync();
        _templates = items.Select(x => new ErrorSettingsTemplateEditModel(x)).ToList();
        StateHasChanged();
    }

    private void HandleLoadError(Exception ex)
    {
        Logger.LogError(ex, "Failed to load ErrorSettingsTemplates");
        _loadError = true;
    }

    private async Task AddNewRow()
    {
        _itemToInsert = new ErrorSettingsTemplateEditModel();
        await _grid!.InsertRow(_itemToInsert);
    }

    private async Task EditRow(ErrorSettingsTemplateEditModel item)
    {
        _originalItem = CreateCopyForRestore(item);
        await _grid!.EditRow(item);
    }

    private static ErrorSettingsTemplateEditModel CreateCopyForRestore(ErrorSettingsTemplateEditModel item)
    {
        return new ErrorSettingsTemplateEditModel
        {
            Id = item.Id,
            StepId = item.StepId,
            StepName = item.StepName,
            AddressError = item.AddressError,
            Description = item.Description
        };
    }

    private async Task SaveRow(ErrorSettingsTemplateEditModel item)
    {
        var error = GetValidationError(item);
        if (error != null)
        {
            ShowError(error);
            return;
        }
        await PersistRowChanges(item);
    }

    private async Task PersistRowChanges(ErrorSettingsTemplateEditModel item)
    {
        try
        {
            await SaveToDatabase(item);
            FinishEditing(item);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            HandleSaveError(ex);
        }
    }

    private async Task SaveToDatabase(ErrorSettingsTemplateEditModel item)
    {
        var entity = item.ToEntity();
        if (IsNewItem(item))
        {
            await CreateNewTemplate(entity);
            return;
        }
        await UpdateExistingTemplate(entity);
    }

    private bool IsNewItem(ErrorSettingsTemplateEditModel item)
    {
        return item == _itemToInsert;
    }

    private async Task CreateNewTemplate(ErrorSettingsTemplate entity)
    {
        await ErrorSettingsTemplateService.CreateAsync(entity);
        _itemToInsert = null;
        ShowSuccess("Настройка ошибки создана");
    }

    private async Task UpdateExistingTemplate(ErrorSettingsTemplate entity)
    {
        await ErrorSettingsTemplateService.UpdateAsync(entity);
        ShowSuccess("Настройка ошибки обновлена");
    }

    private void FinishEditing(ErrorSettingsTemplateEditModel item)
    {
        _originalItem = null;
        _grid!.CancelEditRow(item);
    }

    private void HandleSaveError(Exception ex)
    {
        Logger.LogError(ex, "Failed to save ErrorSettingsTemplate");
        ShowError(ex.Message);
    }

    private static string? GetValidationError(ErrorSettingsTemplateEditModel item)
    {
        return string.IsNullOrWhiteSpace(item.AddressError) ? "Введите адрес ошибки" : null;
    }

    private void CancelEdit(ErrorSettingsTemplateEditModel item)
    {
        if (IsNewItem(item))
        {
            CancelInsert();
        }
        else
        {
            RestoreOriginalValues(item);
        }
        _grid!.CancelEditRow(item);
    }

    private void CancelInsert()
    {
        _itemToInsert = null;
    }

    private void RestoreOriginalValues(ErrorSettingsTemplateEditModel item)
    {
        if (_originalItem == null)
        {
            return;
        }
        item.StepId = _originalItem.StepId;
        item.StepName = _originalItem.StepName;
        item.AddressError = _originalItem.AddressError;
        item.Description = _originalItem.Description;
        _originalItem = null;
    }

    private async Task DeleteRow(ErrorSettingsTemplateEditModel item)
    {
        var confirmed = await ConfirmDelete();
        if (confirmed != true)
        {
            return;
        }
        await PerformDelete(item);
    }

    private async Task<bool?> ConfirmDelete()
    {
        return await DialogService.Confirm(
            "Удалить настройку ошибки?",
            "Подтверждение",
            new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Нет" });
    }

    private async Task PerformDelete(ErrorSettingsTemplateEditModel item)
    {
        try
        {
            await ErrorSettingsTemplateService.DeleteAsync(item.Id);
            RemoveFromList(item);
            await _grid!.Reload();
            ShowSuccess("Настройка ошибки удалена");
        }
        catch (Exception ex)
        {
            HandleDeleteError(ex);
        }
    }

    private void RemoveFromList(ErrorSettingsTemplateEditModel item)
    {
        _templates.Remove(item);
    }

    private void HandleDeleteError(Exception ex)
    {
        Logger.LogError(ex, "Failed to delete ErrorSettingsTemplate");
        ShowError(ex.Message);
    }

    private async Task OpenStepSelectDialog(ErrorSettingsTemplateEditModel item)
    {
        var result = await ShowStepSelectDialog();
        ApplyStepSelection(item, result);
    }

    private async Task<object?> ShowStepSelectDialog()
    {
        return await DialogService.OpenAsync<StepSelectDialog>(
            "Выбор шага теста",
            null,
            new DialogOptions
            {
                Width = "800px",
                Height = "700px",
                Resizable = true,
                Draggable = true
            });
    }

    private static void ApplyStepSelection(ErrorSettingsTemplateEditModel item, object? result)
    {
        if (result is StepFinalTest step)
        {
            AssignStep(item, step);
            return;
        }
        if (result == StepSelectDialog.ClearStepMarker)
        {
            ClearStep(item);
        }
    }

    private static void AssignStep(ErrorSettingsTemplateEditModel item, StepFinalTest step)
    {
        item.StepId = step.Id;
        item.StepName = step.Name;
    }

    private static void ClearStep(ErrorSettingsTemplateEditModel item)
    {
        item.StepId = null;
        item.StepName = null;
    }

    private void ShowSuccess(string message)
    {
        NotificationService.Notify(NotificationSeverity.Success, "Успех", message);
    }

    private void ShowError(string message)
    {
        NotificationService.Notify(NotificationSeverity.Error, "Ошибка", message);
    }
}
