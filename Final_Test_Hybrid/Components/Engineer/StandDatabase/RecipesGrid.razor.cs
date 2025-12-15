using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Database.Edit;
using Final_Test_Hybrid.Services.Database;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase;

public partial class RecipesGrid
{
    [Inject]
    public required RecipeService RecipeService { get; set; }
    [Inject]
    public required BoilerTypeService BoilerTypeService { get; set; }
    [Inject]
    public required ILogger<RecipesGrid> Logger { get; set; }
    [Inject]
    public required NotificationService NotificationService { get; set; }

    private List<RecipeEditModel> _recipes = [];
    private List<RecipeEditModel> _filteredRecipes = [];
    private List<BoilerType> _boilerTypes = [];
    private RadzenDataGrid<RecipeEditModel>? _grid;
    private bool _loadError;
    private RecipeEditModel? _itemToInsert;
    private RecipeEditModel? _originalItem;
    private long? _selectedBoilerTypeId;
    private readonly PlcType[] _plcTypes = Enum.GetValues<PlcType>();
    private readonly string[] _boolValues = ["true", "false"];

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _loadError = false;
            _boilerTypes = await BoilerTypeService.GetAllAsync();
            var items = await RecipeService.GetAllAsync();
            _recipes = items.Select(x => new RecipeEditModel(x)).ToList();
            ApplyFilter();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load Recipes");
            _loadError = true;
        }
    }

    private void ApplyFilter()
    {
        _filteredRecipes = _selectedBoilerTypeId.HasValue
            ? _recipes.Where(r => r.BoilerTypeId == _selectedBoilerTypeId.Value).ToList()
            : [];
    }

    private void OnBoilerTypeFilterChanged()
    {
        ApplyFilter();
    }

    private async Task AddNewRow()
    {
        if (!_selectedBoilerTypeId.HasValue)
        {
            return;
        }
        var boilerType = _boilerTypes.FirstOrDefault(b => b.Id == _selectedBoilerTypeId.Value);
        _itemToInsert = new RecipeEditModel
        {
            BoilerTypeId = _selectedBoilerTypeId.Value,
            BoilerTypeName = boilerType?.Type,
            PlcType = PlcType.Real
        };
        await _grid!.InsertRow(_itemToInsert);
    }

    private async Task EditRow(RecipeEditModel item)
    {
        _originalItem = new RecipeEditModel
        {
            Id = item.Id,
            BoilerTypeId = item.BoilerTypeId,
            PlcType = item.PlcType,
            IsPlc = item.IsPlc,
            Address = item.Address,
            TagName = item.TagName,
            Value = item.Value,
            Description = item.Description,
            Unit = item.Unit,
            Version = item.Version,
            BoilerTypeName = item.BoilerTypeName
        };
        await _grid!.EditRow(item);
    }

    private async Task SaveRow(RecipeEditModel item)
    {
        var boilerType = _boilerTypes.FirstOrDefault(b => b.Id == item.BoilerTypeId);
        item.BoilerTypeName = boilerType?.Type;
        _originalItem = null;
        await _grid!.UpdateRow(item);
    }

    private void CancelEdit(RecipeEditModel item)
    {
        if (item == _itemToInsert)
        {
            _itemToInsert = null;
        }
        else if (_originalItem != null)
        {
            item.BoilerTypeId = _originalItem.BoilerTypeId;
            item.PlcType = _originalItem.PlcType;
            item.IsPlc = _originalItem.IsPlc;
            item.Address = _originalItem.Address;
            item.TagName = _originalItem.TagName;
            item.Value = _originalItem.Value;
            item.Description = _originalItem.Description;
            item.Unit = _originalItem.Unit;
            item.BoilerTypeName = _originalItem.BoilerTypeName;
            _originalItem = null;
        }
        _grid!.CancelEditRow(item);
    }

    private async Task OnRowCreate(RecipeEditModel item)
    {
        try
        {
            var entity = item.ToEntity();
            await RecipeService.CreateAsync(entity);
            _itemToInsert = null;
            NotificationService.Notify(NotificationSeverity.Success, "Успех", "Рецепт создан");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create Recipe");
            NotificationService.Notify(NotificationSeverity.Error, "Ошибка", ex.Message);
            await LoadDataAsync();
        }
    }

    private async Task OnRowUpdate(RecipeEditModel item)
    {
        try
        {
            var entity = item.ToEntity();
            await RecipeService.UpdateAsync(entity);
            NotificationService.Notify(NotificationSeverity.Success, "Успех", "Рецепт обновлён");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update Recipe");
            NotificationService.Notify(NotificationSeverity.Error, "Ошибка", ex.Message);
            await LoadDataAsync();
        }
    }

    private async Task DeleteRow(RecipeEditModel item)
    {
        try
        {
            await RecipeService.DeleteAsync(item.Id);
            _recipes.Remove(item);
            ApplyFilter();
            NotificationService.Notify(NotificationSeverity.Success, "Успех", "Рецепт удалён");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete Recipe");
            NotificationService.Notify(NotificationSeverity.Error, "Ошибка", ex.Message);
        }
    }

    private void OnPlcTypeChanged(RecipeEditModel item)
    {
        item.Value = item.PlcType == PlcType.Bool ? "false" : string.Empty;
    }

    private void ValidateValue(RecipeEditModel item)
    {
        var error = item.PlcType switch
        {
            PlcType.Real => !double.TryParse(item.Value, out _)
                ? "Введите число" : null,
            PlcType.Int16 => !short.TryParse(item.Value, out _)
                ? "Введите целое число (-32768..32767)" : null,
            PlcType.Dint => !int.TryParse(item.Value, out _)
                ? "Введите целое число" : null,
            _ => null
        };
        if (error != null)
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Внимание", error);
        }
    }
}
