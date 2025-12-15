using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Database;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase;

public partial class StandDatabaseDialog
{
    [Inject]
    public required BoilerTypeService BoilerTypeService { get; set; }
    [Inject]
    public required ILogger<StandDatabaseDialog> Logger { get; set; }
    [Inject]
    public required NotificationService NotificationService { get; set; }

    private int _selectedIndex;
    private List<BoilerTypeEditModel> _boilerTypes = [];
    private RadzenDataGrid<BoilerTypeEditModel>? _grid;
    private bool _loadError;
    private BoilerTypeEditModel? _itemToInsert;

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
        await _grid!.EditRow(item);
    }

    private async Task SaveRow(BoilerTypeEditModel item)
    {
        await _grid!.UpdateRow(item);
    }

    private void CancelEdit(BoilerTypeEditModel item)
    {
        if (item == _itemToInsert)
        {
            _itemToInsert = null;
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

    public class BoilerTypeEditModel
    {
        public long Id { get; set; }
        public string Article { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Version { get; set; }

        public BoilerTypeEditModel()
        {
        }

        public BoilerTypeEditModel(BoilerType entity)
        {
            Id = entity.Id;
            Article = entity.Article;
            Type = entity.Type;
            Version = entity.Version;
        }

        public BoilerType ToEntity()
        {
            return new BoilerType
            {
                Id = Id,
                Article = Article,
                Type = Type,
                Version = Version
            };
        }
    }
}
