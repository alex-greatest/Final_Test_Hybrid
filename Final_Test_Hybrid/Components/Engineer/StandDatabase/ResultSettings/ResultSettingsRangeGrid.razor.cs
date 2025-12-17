using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Database.Edit;
using Final_Test_Hybrid.Services.Database;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase.ResultSettings;

public partial class ResultSettingsRangeGrid
{
    [Inject] public required ResultSettingsService ResultSettingsService { get; set; }
    [Inject] public required ILogger<ResultSettingsRangeGrid> Logger { get; set; }
    [Inject] public required NotificationService NotificationService { get; set; }
    [Inject] public required DialogService DialogService { get; set; }
    [Parameter] public List<ResultSettingsEditModel> Items { get; set; } = [];
    [Parameter] public List<BoilerType> BoilerTypes { get; set; } = [];
    [Parameter] public long? SelectedBoilerTypeId { get; set; }
    [Parameter] public EventCallback OnSaved { get; set; }
    [Parameter] public IList<ResultSettingsEditModel> SelectedItems { get; set; } = [];
    [Parameter] public EventCallback<IList<ResultSettingsEditModel>> SelectedItemsChanged { get; set; }
    private RadzenDataGrid<ResultSettingsEditModel>? _grid;
    private ResultSettingsEditModel? _itemToInsert;
    private ResultSettingsEditModel? _originalItem;
    private readonly PlcType[] _plcTypes = Enum.GetValues<PlcType>();
    private readonly KeyValuePair<AuditType, string>[] _allowedAuditTypes =
    [
        new(AuditType.NumericWithRange, "Диапазон"),
        new(AuditType.SimpleStatus, "Без диапазона"),
        new(AuditType.BoardParameters, "Параметры платы")
    ];

    private async Task AddNewRow()
    {
        if (!SelectedBoilerTypeId.HasValue)
        {
            return;
        }
        _itemToInsert = new ResultSettingsEditModel
        {
            BoilerTypeId = SelectedBoilerTypeId.Value,
            PlcType = PlcType.Real,
            AuditType = AuditType.NumericWithRange
        };
        await _grid!.InsertRow(_itemToInsert);
    }

    private async Task EditRow(ResultSettingsEditModel item)
    {
        _originalItem = CloneEditModel(item);
        await _grid!.EditRow(item);
    }

    private async Task SaveRow(ResultSettingsEditModel item)
    {
        var error = GetValidationError(item);
        if (error != null)
        {
            ShowError(error);
            return;
        }
        try
        {
            var entity = item.ToEntity();
            if (item == _itemToInsert)
            {
                await ResultSettingsService.CreateAsync(entity);
                _itemToInsert = null;
                ShowSuccess("Настройка создана");
            }
            else
            {
                await ResultSettingsService.UpdateAsync(entity);
                ShowSuccess("Настройка обновлена");
            }
            _originalItem = null;
            _grid!.CancelEditRow(item);
            await OnSaved.InvokeAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save ResultSettings");
            ShowError(ex.Message);
        }
    }

    private void CancelEdit(ResultSettingsEditModel item)
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

    private async Task DeleteRow(ResultSettingsEditModel item)
    {
        var confirmed = await DialogService.Confirm(
            "Удалить настройку?",
            "Подтверждение",
            new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Нет" });
        if (confirmed != true)
        {
            return;
        }
        try
        {
            await ResultSettingsService.DeleteAsync(item.Id);
            ShowSuccess("Настройка удалена");
            await OnSaved.InvokeAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete ResultSettings");
            ShowError(ex.Message);
        }
    }

    private static string? GetValidationError(ResultSettingsEditModel item)
    {
        if (string.IsNullOrWhiteSpace(item.ParameterName))
        {
            return "Введите имя параметра";
        }
        return string.IsNullOrWhiteSpace(item.AddressValue) ? "Введите адрес значения" : null;
    }

    private static string GetAuditTypeDisplayName(AuditType type) => type switch
    {
        AuditType.NumericWithRange => "Диапазон",
        AuditType.SimpleStatus => "Без диапазона",
        AuditType.BoardParameters => "Параметры платы",
        _ => type.ToString()
    };

    private static ResultSettingsEditModel CloneEditModel(ResultSettingsEditModel source)
    {
        return new ResultSettingsEditModel
        {
            Id = source.Id,
            BoilerTypeId = source.BoilerTypeId,
            ParameterName = source.ParameterName,
            AddressValue = source.AddressValue,
            AddressMin = source.AddressMin,
            AddressMax = source.AddressMax,
            AddressStatus = source.AddressStatus,
            PlcType = source.PlcType,
            Nominal = source.Nominal,
            Unit = source.Unit,
            Description = source.Description,
            AuditType = source.AuditType,
            BoilerTypeName = source.BoilerTypeName
        };
    }

    private void RestoreFromOriginal(ResultSettingsEditModel target)
    {
        target.BoilerTypeId = _originalItem!.BoilerTypeId;
        target.ParameterName = _originalItem.ParameterName;
        target.AddressValue = _originalItem.AddressValue;
        target.AddressMin = _originalItem.AddressMin;
        target.AddressMax = _originalItem.AddressMax;
        target.AddressStatus = _originalItem.AddressStatus;
        target.PlcType = _originalItem.PlcType;
        target.Nominal = _originalItem.Nominal;
        target.Unit = _originalItem.Unit;
        target.Description = _originalItem.Description;
        target.AuditType = _originalItem.AuditType;
    }

    private void ShowSuccess(string message) =>
        NotificationService.Notify(NotificationSeverity.Success, "Успех", message);

    private void ShowError(string message) =>
        NotificationService.Notify(NotificationSeverity.Error, "Ошибка", message);

    private bool IsAllSelected => Items.Count > 0 && SelectedItems.Count == Items.Count;

    private async Task OnSelectAllChanged(bool value)
    {
        var newSelection = value ? Items.ToList() : new List<ResultSettingsEditModel>();
        await SelectedItemsChanged.InvokeAsync(newSelection);
    }

    private async Task OnRowSelectChanged(ResultSettingsEditModel item, bool selected)
    {
        var newSelection = selected
            ? [..SelectedItems.Where(r => r != item), item]
            : SelectedItems.Where(r => r != item).ToList();
        await SelectedItemsChanged.InvokeAsync(newSelection);
    }
}
