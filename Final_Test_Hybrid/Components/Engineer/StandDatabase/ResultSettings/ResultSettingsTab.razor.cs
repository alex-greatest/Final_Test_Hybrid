using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Database.Edit;
using Final_Test_Hybrid.Services.Database;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase.ResultSettings;

public partial class ResultSettingsTab
{
    [Inject] public required ResultSettingsService ResultSettingsService { get; set; }
    [Inject] public required BoilerTypeService BoilerTypeService { get; set; }
    [Inject] public required ILogger<ResultSettingsTab> Logger { get; set; }
    private List<ResultSettingsEditModel> _allItems = [];
    private List<ResultSettingsEditModel> _itemsRange = [];
    private List<ResultSettingsEditModel> _itemsSimple = [];
    private List<ResultSettingsEditModel> _itemsBoard = [];
    private List<BoilerType> _boilerTypes = [];
    private IList<ResultSettingsEditModel> _selectedRange = [];
    private IList<ResultSettingsEditModel> _selectedSimple = [];
    private IList<ResultSettingsEditModel> _selectedBoard = [];
    private int TotalSelectedCount => _selectedRange.Count + _selectedSimple.Count + _selectedBoard.Count;
    private int TotalItemsCount => _itemsRange.Count + _itemsSimple.Count + _itemsBoard.Count;
    private ResultSettingsRangeGrid? _gridRange;
    private ResultSettingsSimpleGrid? _gridSimple;
    private ResultSettingsBoardGrid? _gridBoard;
    private bool _loadError;
    private long? _selectedBoilerTypeId;
    private int _selectedTabIndex;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    public async Task RefreshAsync() => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        try
        {
            _loadError = false;
            _boilerTypes = await BoilerTypeService.GetAllAsync();
            var items = await ResultSettingsService.GetAllAsync();
            _allItems = items.Select(x => new ResultSettingsEditModel(x)).ToList();
            ApplyFilter();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load ResultSettings");
            _loadError = true;
        }
    }

    private void ApplyFilter()
    {
        var filtered = _selectedBoilerTypeId.HasValue
            ? _allItems.Where(r => r.BoilerTypeId == _selectedBoilerTypeId.Value).ToList()
            : [];
        _itemsRange = filtered.Where(x => x.AuditType == AuditType.NumericWithRange).ToList();
        _itemsSimple = filtered.Where(x => x.AuditType == AuditType.SimpleStatus).ToList();
        _itemsBoard = filtered.Where(x => x.AuditType == AuditType.BoardParameters).ToList();
    }

    private void OnBoilerTypeFilterChanged()
    {
        ApplyFilter();
        StateHasChanged();
    }

    private async Task OnItemSaved()
    {
        await LoadDataAsync();
        SelectTabByAuditType();
    }

    private void SelectTabByAuditType()
    {
        // Tab selection will be handled by data refresh
    }

    private async Task ClearAllAsync()
    {
        if (_selectedBoilerTypeId == null)
        {
            return;
        }
        var confirmed = await DialogService.Confirm(
            "Удалить все настройки результатов для выбранного типа котла?",
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
            await ResultSettingsService.DeleteAllByBoilerTypeAsync(_selectedBoilerTypeId!.Value);
            NotificationService.Notify(NotificationSeverity.Success, "Успех", "Все настройки результатов удалены");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to clear all ResultSettings");
            NotificationService.Notify(NotificationSeverity.Error, "Ошибка", ex.Message);
        }
    }
}
