using Final_Test_Hybrid.Models.Database.Edit;
using Final_Test_Hybrid.Services.Database;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase;

public partial class BoilerTypesGrid
{
    [Inject]
    public required BoilerTypeService BoilerTypeService { get; set; }
    [Inject]
    public required ILogger<BoilerTypesGrid> Logger { get; set; }
    [Inject]
    public required NotificationService NotificationService { get; set; }

    private List<BoilerTypeEditModel> _boilerTypes = [];
    private RadzenDataGrid<BoilerTypeEditModel>? _grid;
    private bool _loadError;
    private BoilerTypeEditModel? _itemToInsert;
    private BoilerTypeEditModel? _originalItem;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _loadError = false;
            var items = await BoilerTypeService.GetAllAsync();
            _boilerTypes = items.Select(x => new BoilerTypeEditModel(x)).ToList();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load BoilerTypes");
            _loadError = true;
        }
    }

    private async Task AddNewRow()
    {
        _itemToInsert = new BoilerTypeEditModel();
        await _grid!.InsertRow(_itemToInsert);
    }

    private async Task EditRow(BoilerTypeEditModel item)
    {
        _originalItem = new BoilerTypeEditModel
        {
            Id = item.Id,
            Article = item.Article,
            Type = item.Type,
            Version = item.Version
        };
        await _grid!.EditRow(item);
    }

    private async Task SaveRow(BoilerTypeEditModel item)
    {
        _originalItem = null;
        await _grid!.UpdateRow(item);
    }

    private void CancelEdit(BoilerTypeEditModel item)
    {
        if (item == _itemToInsert)
        {
            _itemToInsert = null;
        }
        else if (_originalItem != null)
        {
            item.Article = _originalItem.Article;
            item.Type = _originalItem.Type;
            _originalItem = null;
        }
        _grid!.CancelEditRow(item);
    }

    private async Task OnRowCreate(BoilerTypeEditModel item)
    {
        try
        {
            var entity = item.ToEntity();
            await BoilerTypeService.CreateAsync(entity);
            _itemToInsert = null;
            NotificationService.Notify(NotificationSeverity.Success, "Успех", "Тип котла создан");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create BoilerType");
            NotificationService.Notify(NotificationSeverity.Error, "Ошибка", ex.Message);
            await LoadDataAsync();
        }
    }

    private async Task OnRowUpdate(BoilerTypeEditModel item)
    {
        try
        {
            var entity = item.ToEntity();
            await BoilerTypeService.UpdateAsync(entity);
            NotificationService.Notify(NotificationSeverity.Success, "Успех", "Тип котла обновлён");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update BoilerType");
            NotificationService.Notify(NotificationSeverity.Error, "Ошибка", ex.Message);
            await LoadDataAsync();
        }
    }

    private async Task DeleteRow(BoilerTypeEditModel item)
    {
        try
        {
            await BoilerTypeService.DeleteAsync(item.Id);
            _boilerTypes.Remove(item);
            NotificationService.Notify(NotificationSeverity.Success, "Успех", "Тип котла удалён");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete BoilerType");
            NotificationService.Notify(NotificationSeverity.Error, "Ошибка", ex.Message);
        }
    }

}
