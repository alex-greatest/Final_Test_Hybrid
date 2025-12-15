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
    [Inject] public required RecipeService RecipeService { get; set; }
    [Inject] public required BoilerTypeService BoilerTypeService { get; set; }
    [Inject] public required ILogger<RecipesGrid> Logger { get; set; }
    [Inject] public required NotificationService NotificationService { get; set; }
    [Inject] public required DialogService DialogService { get; set; }

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

    #region Data Loading

    private async Task LoadDataAsync()
    {
        try
        {
            _loadError = false;
            _boilerTypes = await BoilerTypeService.GetAllAsync();
            var items = await RecipeService.GetAllAsync();
            _recipes = items.Select(x => new RecipeEditModel(x)).ToList();
            ApplyFilter();
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

    private void OnBoilerTypeFilterChanged() => ApplyFilter();

    #endregion

    #region Row Operations

    private async Task AddNewRow()
    {
        if (!_selectedBoilerTypeId.HasValue)
        {
            return;
        }
        var boilerType = FindBoilerType(_selectedBoilerTypeId.Value);
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
        _originalItem = CloneEditModel(item);
        await _grid!.EditRow(item);
    }

    private async Task SaveRow(RecipeEditModel item)
    {
        var error = GetValueValidationError(item);
        if (error != null)
        {
            ShowError(error);
            return;
        }
        var boilerType = FindBoilerType(item.BoilerTypeId);
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
            RestoreFromOriginal(item);
            _originalItem = null;
        }
        _grid!.CancelEditRow(item);
    }

    private async Task DeleteRow(RecipeEditModel item)
    {
        var confirmed = await DialogService.Confirm(
            "Удалить рецепт?",
            "Подтверждение",
            new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Нет" });
        if (confirmed != true)
        {
            return;
        }
        try
        {
            await RecipeService.DeleteAsync(item.Id);
            _recipes.Remove(item);
            ApplyFilter();
            ShowSuccess("Рецепт удалён");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete Recipe");
            ShowError(ex.Message);
        }
    }

    #endregion

    #region Grid Events

    private async Task OnRowCreate(RecipeEditModel item)
    {
        try
        {
            var entity = item.ToEntity();
            await RecipeService.CreateAsync(entity);
            _itemToInsert = null;
            ShowSuccess("Рецепт создан");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create Recipe");
            ShowError(ex.Message);
            await LoadDataAsync();
        }
    }

    private async Task OnRowUpdate(RecipeEditModel item)
    {
        try
        {
            var entity = item.ToEntity();
            await RecipeService.UpdateAsync(entity);
            ShowSuccess("Рецепт обновлён");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update Recipe");
            ShowError(ex.Message);
            await LoadDataAsync();
        }
    }

    #endregion

    #region Validation

    private void OnPlcTypeChanged(RecipeEditModel item)
    {
        item.Value = item.PlcType == PlcType.Bool ? "false" : string.Empty;
    }

    private void ValidateValue(RecipeEditModel item)
    {
        var error = GetValueValidationError(item);
        if (error != null)
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Внимание", error);
        }
    }

    private static string? GetValueValidationError(RecipeEditModel item)
    {
        return item.PlcType switch
        {
            PlcType.Real => !double.TryParse(item.Value, out _) ? "Введите число" : null,
            PlcType.Int16 => !short.TryParse(item.Value, out _) ? "Введите целое число (-32768..32767)" : null,
            PlcType.Dint => !int.TryParse(item.Value, out _) ? "Введите целое число" : null,
            _ => null
        };
    }

    #endregion

    #region Helpers

    private BoilerType? FindBoilerType(long id) =>
        _boilerTypes.FirstOrDefault(b => b.Id == id);

    private static void CopyEditableProperties(RecipeEditModel source, RecipeEditModel target)
    {
        target.BoilerTypeId = source.BoilerTypeId;
        target.PlcType = source.PlcType;
        target.IsPlc = source.IsPlc;
        target.Address = source.Address;
        target.TagName = source.TagName;
        target.Value = source.Value;
        target.Description = source.Description;
        target.Unit = source.Unit;
        target.BoilerTypeName = source.BoilerTypeName;
    }

    private static RecipeEditModel CloneEditModel(RecipeEditModel source)
    {
        var clone = new RecipeEditModel { Id = source.Id };
        CopyEditableProperties(source, clone);
        return clone;
    }

    private void RestoreFromOriginal(RecipeEditModel target) =>
        CopyEditableProperties(_originalItem!, target);

    private void ShowSuccess(string message) =>
        NotificationService.Notify(NotificationSeverity.Success, "Успех", message);

    private void ShowError(string message) =>
        NotificationService.Notify(NotificationSeverity.Error, "Ошибка", message);

    #endregion
}
