using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Database.Edit;
using Final_Test_Hybrid.Services.Database;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase;

public partial class StepFinalTestsGrid
{
    [Inject]
    public required StepFinalTestService StepFinalTestService { get; set; }
    [Inject]
    public required ILogger<StepFinalTestsGrid> Logger { get; set; }
    [Inject]
    public required NotificationService NotificationService { get; set; }
    [Inject]
    public required DialogService DialogService { get; set; }
    [Parameter]
    public EventCallback OnDataChanged { get; set; }

    private List<StepFinalTestEditModel> _stepFinalTests = [];
    private RadzenDataGrid<StepFinalTestEditModel>? _grid;
    private bool _loadError;
    private StepFinalTestEditModel? _itemToInsert;
    private StepFinalTestEditModel? _originalItem;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    public async Task RefreshAsync() => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        try
        {
            await LoadStepFinalTestsFromDatabase();
        }
        catch (Exception ex)
        {
            HandleLoadError(ex);
        }
    }

    private async Task LoadStepFinalTestsFromDatabase()
    {
        _loadError = false;
        var items = await StepFinalTestService.GetAllAsync();
        _stepFinalTests = items.Select(x => new StepFinalTestEditModel(x)).ToList();
        StateHasChanged();
    }

    private void HandleLoadError(Exception ex)
    {
        Logger.LogError(ex, "Failed to load StepFinalTests");
        _loadError = true;
    }

    private async Task AddNewRow()
    {
        _itemToInsert = new StepFinalTestEditModel();
        await _grid!.InsertRow(_itemToInsert);
    }

    private async Task EditRow(StepFinalTestEditModel item)
    {
        _originalItem = CreateBackupOf(item);
        await _grid!.EditRow(item);
    }

    private static StepFinalTestEditModel CreateBackupOf(StepFinalTestEditModel item)
    {
        return new StepFinalTestEditModel
        {
            Id = item.Id,
            Name = item.Name
        };
    }

    private async Task SaveRow(StepFinalTestEditModel item)
    {
        var validationError = GetValidationError(item);
        if (validationError != null)
        {
            ShowError(validationError);
            return;
        }
        await PersistRowChanges(item);
    }

    private async Task PersistRowChanges(StepFinalTestEditModel item)
    {
        try
        {
            await SaveItemToDatabase(item);
            await FinishEditingAndReload(item);
        }
        catch (Exception ex)
        {
            HandleSaveError(ex);
        }
    }

    private async Task SaveItemToDatabase(StepFinalTestEditModel item)
    {
        var entity = item.ToEntity();
        var isNewItem = item == _itemToInsert;
        if (isNewItem)
        {
            await CreateNewItem(entity);
        }
        else
        {
            await UpdateExistingItem(entity);
        }
    }

    private async Task CreateNewItem(StepFinalTest entity)
    {
        await StepFinalTestService.CreateAsync(entity);
        _itemToInsert = null;
        ShowSuccess("Шаг теста создан");
    }

    private async Task UpdateExistingItem(StepFinalTest entity)
    {
        await StepFinalTestService.UpdateAsync(entity);
        ShowSuccess("Шаг теста обновлён");
    }

    private async Task FinishEditingAndReload(StepFinalTestEditModel item)
    {
        _originalItem = null;
        _grid!.CancelEditRow(item);
        await LoadDataAsync();
        await OnDataChanged.InvokeAsync();
    }

    private void HandleSaveError(Exception ex)
    {
        Logger.LogError(ex, "Failed to save StepFinalTest");
        ShowError(ex.Message);
    }

    private static string? GetValidationError(StepFinalTestEditModel item)
    {
        return string.IsNullOrWhiteSpace(item.Name) ? "Введите название шага" : null;
    }

    private void CancelEdit(StepFinalTestEditModel item)
    {
        var isNewItem = item == _itemToInsert;
        if (isNewItem)
        {
            CancelNewItemInsertion();
        }
        else
        {
            RestoreOriginalValues(item);
        }
        _grid!.CancelEditRow(item);
    }

    private void CancelNewItemInsertion()
    {
        _itemToInsert = null;
    }

    private void RestoreOriginalValues(StepFinalTestEditModel item)
    {
        if (_originalItem == null)
        {
            return;
        }
        item.Name = _originalItem.Name;
        _originalItem = null;
    }

    private async Task DeleteRow(StepFinalTestEditModel item)
    {
        var isConfirmed = await ConfirmDeletion();
        if (!isConfirmed)
        {
            return;
        }
        await ExecuteDeletion(item);
    }

    private async Task<bool> ConfirmDeletion()
    {
        var result = await DialogService.Confirm(
            "Удалить шаг теста?",
            "Подтверждение",
            new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Нет" });
        return result == true;
    }

    private async Task ExecuteDeletion(StepFinalTestEditModel item)
    {
        try
        {
            await DeleteItemFromDatabase(item);
        }
        catch (Exception ex)
        {
            HandleDeleteError(ex);
        }
    }

    private async Task DeleteItemFromDatabase(StepFinalTestEditModel item)
    {
        await StepFinalTestService.DeleteAsync(item.Id);
        _stepFinalTests.Remove(item);
        await _grid!.Reload();
        ShowSuccess("Шаг теста удалён");
        await OnDataChanged.InvokeAsync();
    }

    private void HandleDeleteError(Exception ex)
    {
        Logger.LogError(ex, "Failed to delete StepFinalTest");
        ShowError(ex.Message);
    }

    private void ShowSuccess(string message)
    {
        NotificationService.Notify(NotificationSeverity.Success, "Успех", message);
    }

    private void ShowError(string message)
    {
        NotificationService.Notify(NotificationSeverity.Error, "Ошибка", message);
    }

    private async Task ClearAllAsync()
    {
        var confirmed = await DialogService.Confirm(
            "Удалить все шаги теста?",
            "Подтверждение",
            new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Нет" });
        if (confirmed != true)
        {
            return;
        }
        await ExecuteClearAllAsync();
    }

    private async Task ExecuteClearAllAsync()
    {
        try
        {
            await StepFinalTestService.DeleteAllAsync();
            ShowSuccess("Все шаги теста удалены");
            await LoadDataAsync();
            await OnDataChanged.InvokeAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to clear all StepFinalTests");
            ShowError(ex.Message);
        }
    }
}
