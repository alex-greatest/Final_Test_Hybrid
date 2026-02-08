using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Engineer.Modals;

/// <summary>
/// Code-behind для диалога редактирования IO: AI Calibration, RTD Calibration, PID Regulator, Analog Outputs.
/// </summary>
public partial class IoEditorDialog : IAsyncDisposable
{
    // === INJECTS ===
    [Inject]
    private PlcSubscriptionState SubscriptionState { get; set; } = null!;

    [Inject]
    private OpcUaConnectionState ConnectionState { get; set; } = null!;

    [Inject]
    private OpcUaTagService TagService { get; set; } = null!;

    [Inject]
    private DualLogger<IoEditorDialog> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    // === COMMON ===
    private bool _isLoading = true;
    private bool _disposed;
    private List<string> _saveErrors = [];

    // === AI CALIBRATION ===
    private RadzenDataGrid<AiCalibrationItem> _aiGrid = null!;
    private List<AiCalibrationItem> _aiItems = [];
    private List<AiCalibrationItem> _aiSnapshot = [];
    private AiCalibrationItem? _aiItemToUpdate;
    private string? _aiEditingColumn;

    // === RTD CALIBRATION ===
    private RadzenDataGrid<RtdCalibrationItem> _rtdGrid = null!;
    private List<RtdCalibrationItem> _rtdItems = [];
    private List<RtdCalibrationItem> _rtdSnapshot = [];
    private RtdCalibrationItem? _rtdItemToUpdate;
    private string? _rtdEditingColumn;

    // === PID REGULATOR ===
    private RadzenDataGrid<PidRegulatorItem> _pidGrid = null!;
    private List<PidRegulatorItem> _pidItems = [];
    private List<PidRegulatorItem> _pidSnapshot = [];
    private PidRegulatorItem? _pidItemToUpdate;
    private string? _pidEditingColumn;

    // === ANALOG OUTPUTS ===
    private RadzenDataGrid<AnalogOutputItem> _aoGrid = null!;
    private List<AnalogOutputItem> _aoItems = [];
    private List<AnalogOutputItem> _aoSnapshot = [];
    private AnalogOutputItem? _aoItemToUpdate;
    private string? _aoEditingColumn;

    // === OUTSIDE CLICK ===
    private const string AiContainerId = "ai-grid-container";
    private const string RtdContainerId = "rtd-grid-container";
    private const string PidContainerId = "pid-grid-container";
    private const string AoContainerId = "ao-grid-container";
    private OutsideClickHelper? _aiClickHelper;
    private OutsideClickHelper? _rtdClickHelper;
    private OutsideClickHelper? _pidClickHelper;
    private OutsideClickHelper? _aoClickHelper;

    #region Lifecycle

    /// <summary>
    /// Инициализирует компонент: подписывается на события и загружает данные.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        SubscriptionState.OnStateChanged += HandleStateChanged;
        ConnectionState.ConnectionStateChanged += HandleConnectionChanged;

        if (!ConnectionState.IsConnected)
        {
            Logger.LogWarning("OPC UA не подключен при открытии IoEditor");
            _isLoading = false;
            return;
        }

        try
        {
            await LoadAiDataAsync();
            CreateAiSnapshot();

            await LoadRtdDataAsync();
            CreateRtdSnapshot();

            await LoadPidDataAsync();
            CreatePidSnapshot();

            await LoadAoDataAsync();
            CreateAoSnapshot();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка загрузки данных IO Editor");
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Регистрирует JS outside-click обработчики для каждого грида.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _aiClickHelper = new OutsideClickHelper(CloseAiEdit);
        _rtdClickHelper = new OutsideClickHelper(CloseRtdEdit);
        _pidClickHelper = new OutsideClickHelper(ClosePidEdit);
        _aoClickHelper = new OutsideClickHelper(CloseAoEdit);

        try
        {
            await JsRuntime.InvokeVoidAsync("outsideClickHandler.add", AiContainerId, _aiClickHelper.Reference);
            await JsRuntime.InvokeVoidAsync("outsideClickHandler.add", RtdContainerId, _rtdClickHelper.Reference);
            await JsRuntime.InvokeVoidAsync("outsideClickHandler.add", PidContainerId, _pidClickHelper.Reference);
            await JsRuntime.InvokeVoidAsync("outsideClickHandler.add", AoContainerId, _aoClickHelper.Reference);
        }
        catch (JSException ex)
        {
            Logger.LogWarning("Не удалось зарегистрировать outsideClickHandler: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Освобождает ресурсы компонента.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        SubscriptionState.OnStateChanged -= HandleStateChanged;
        ConnectionState.ConnectionStateChanged -= HandleConnectionChanged;

        try
        {
            await JsRuntime.InvokeVoidAsync("outsideClickHandler.remove", AiContainerId);
            await JsRuntime.InvokeVoidAsync("outsideClickHandler.remove", RtdContainerId);
            await JsRuntime.InvokeVoidAsync("outsideClickHandler.remove", PidContainerId);
            await JsRuntime.InvokeVoidAsync("outsideClickHandler.remove", AoContainerId);
        }
        catch { /* Circuit may be gone */ }

        _aiClickHelper?.Dispose();
        _rtdClickHelper?.Dispose();
        _pidClickHelper?.Dispose();
        _aoClickHelper?.Dispose();
    }

    private void HandleStateChanged()
    {
        if (_disposed)
        {
            return;
        }
        _ = InvokeAsync(StateHasChanged);
    }

    private void HandleConnectionChanged(bool isConnected)
    {
        if (_disposed)
        {
            return;
        }
        _ = InvokeAsync(StateHasChanged);
    }

    #endregion

    #region AI Calibration

    /// <summary>
    /// Загружает данные AI Calibration из OPC.
    /// </summary>
    private async Task LoadAiDataAsync()
    {
        _aiItems = [];

        foreach (var sensor in AiCallCheckTags.Sensors)
        {
            var min = await TagService.ReadAsync<float>(
                AiCallCheckTags.BuildNodeId(sensor.Name, AiCallCheckTags.Fields.LimMin));
            var max = await TagService.ReadAsync<float>(
                AiCallCheckTags.BuildNodeId(sensor.Name, AiCallCheckTags.Fields.LimMax));
            var mult = await TagService.ReadAsync<float>(
                AiCallCheckTags.BuildNodeId(sensor.Name, AiCallCheckTags.Fields.Gain));
            var offset = await TagService.ReadAsync<float>(
                AiCallCheckTags.BuildNodeId(sensor.Name, AiCallCheckTags.Fields.Offset));

            if (!min.Success || !max.Success || !mult.Success || !offset.Success)
            {
                var errors = string.Join(", ", new[] { min, max, mult, offset }
                    .Where(r => !r.Success)
                    .Select(r => r.Error));
                Logger.LogWarning("Не удалось прочитать AI сенсор {Sensor}: {Errors}", sensor.Name, errors);
                continue;
            }

            _aiItems.Add(new AiCalibrationItem
            {
                SensorName = sensor.Name,
                Description = sensor.Description,
                Min = min.Value,
                Max = max.Value,
                Multiplier = mult.Value,
                Offset = offset.Value
            });
        }
    }

    /// <summary>
    /// Создаёт снимок текущих значений AI для отслеживания изменений.
    /// </summary>
    private void CreateAiSnapshot()
    {
        _aiSnapshot = _aiItems.Select(item => new AiCalibrationItem
        {
            SensorName = item.SensorName,
            Description = item.Description,
            Min = item.Min,
            Max = item.Max,
            Multiplier = item.Multiplier,
            Offset = item.Offset
        }).ToList();
    }

    private bool IsAiMinChanged(AiCalibrationItem item)
    {
        var index = _aiItems.IndexOf(item);
        if (index < 0 || index >= _aiSnapshot.Count)
        {
            return false;
        }
        return item.Min != _aiSnapshot[index].Min;
    }

    private bool IsAiMaxChanged(AiCalibrationItem item)
    {
        var index = _aiItems.IndexOf(item);
        if (index < 0 || index >= _aiSnapshot.Count)
        {
            return false;
        }
        return item.Max != _aiSnapshot[index].Max;
    }

    private bool IsAiMultiplierChanged(AiCalibrationItem item)
    {
        var index = _aiItems.IndexOf(item);
        if (index < 0 || index >= _aiSnapshot.Count)
        {
            return false;
        }
        return item.Multiplier != _aiSnapshot[index].Multiplier;
    }

    private bool IsAiOffsetChanged(AiCalibrationItem item)
    {
        var index = _aiItems.IndexOf(item);
        if (index < 0 || index >= _aiSnapshot.Count)
        {
            return false;
        }
        return item.Offset != _aiSnapshot[index].Offset;
    }

    private bool HasAiUnsavedChanges
    {
        get
        {
            if (_aiItems.Count != _aiSnapshot.Count)
            {
                return false;
            }

            for (var i = 0; i < _aiItems.Count; i++)
            {
                var item = _aiItems[i];
                var snapshot = _aiSnapshot[i];
                if (item.Min != snapshot.Min ||
                    item.Max != snapshot.Max ||
                    item.Multiplier != snapshot.Multiplier ||
                    item.Offset != snapshot.Offset)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Сохраняет изменения AI в OPC.
    /// </summary>
    private async Task SaveAiAsync()
    {
        _saveErrors.Clear();

        try
        {
            for (var i = 0; i < _aiItems.Count; i++)
            {
                var item = _aiItems[i];
                var snapshot = _aiSnapshot[i];

                if (item.Min != snapshot.Min)
                {
                    var result = await TagService.WriteAsync(
                        AiCallCheckTags.BuildNodeId(item.SensorName, AiCallCheckTags.Fields.LimMin), (float)item.Min);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.SensorName}.Min: {result.Error}");
                    }
                }

                if (item.Max != snapshot.Max)
                {
                    var result = await TagService.WriteAsync(
                        AiCallCheckTags.BuildNodeId(item.SensorName, AiCallCheckTags.Fields.LimMax), (float)item.Max);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.SensorName}.Max: {result.Error}");
                    }
                }

                if (item.Multiplier != snapshot.Multiplier)
                {
                    var result = await TagService.WriteAsync(
                        AiCallCheckTags.BuildNodeId(item.SensorName, AiCallCheckTags.Fields.Gain), (float)item.Multiplier);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.SensorName}.Multiplier: {result.Error}");
                    }
                }

                if (item.Offset != snapshot.Offset)
                {
                    var result = await TagService.WriteAsync(
                        AiCallCheckTags.BuildNodeId(item.SensorName, AiCallCheckTags.Fields.Offset), (float)item.Offset);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.SensorName}.Offset: {result.Error}");
                    }
                }
            }

            if (_saveErrors.Count == 0)
            {
                CreateAiSnapshot();
                Logger.LogInformation("AI Calibration сохранено");
            }
            else
            {
                Logger.LogWarning("Частичная ошибка сохранения AI Calibration: {Errors}",
                    string.Join("; ", _saveErrors));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка сохранения AI Calibration");
        }
        finally
        {
            StateHasChanged();
        }
    }

    /// <summary>
    /// Сбрасывает изменения AI к исходным значениям.
    /// </summary>
    private void ResetAiChanges()
    {
        for (var i = 0; i < _aiItems.Count; i++)
        {
            _aiItems[i].Min = _aiSnapshot[i].Min;
            _aiItems[i].Max = _aiSnapshot[i].Max;
            _aiItems[i].Multiplier = _aiSnapshot[i].Multiplier;
            _aiItems[i].Offset = _aiSnapshot[i].Offset;
        }
        StateHasChanged();
    }

    private async Task OnAiCellClick(DataGridCellMouseEventArgs<AiCalibrationItem> args)
    {
        if (_aiItemToUpdate == args.Data && _aiEditingColumn == args.Column.Property)
        {
            return;
        }

        if (_aiItemToUpdate != null)
        {
            await _aiGrid.UpdateRow(_aiItemToUpdate);
        }

        _aiItemToUpdate = args.Data;
        _aiEditingColumn = args.Column.Property;

        await _aiGrid.EditRow(args.Data);
        StateHasChanged();
    }

    private bool IsAiEditing(string propertyName) => _aiEditingColumn == propertyName;

    /// <summary>
    /// Завершает редактирование AI при клике вне грида.
    /// </summary>
    private async Task CloseAiEdit()
    {
        if (_aiItemToUpdate == null) return;
        await _aiGrid.UpdateRow(_aiItemToUpdate);
        _aiItemToUpdate = null;
        _aiEditingColumn = null;
        await InvokeAsync(StateHasChanged);
    }

    private void OnAiUpdateRow(AiCalibrationItem item)
    {
        // Изменения уже применены через binding
    }

    #endregion

    #region RTD Calibration

    /// <summary>
    /// Загружает данные RTD Calibration из OPC.
    /// </summary>
    private async Task LoadRtdDataAsync()
    {
        _rtdItems = [];

        foreach (var sensor in RtdCalCheckTags.Sensors)
        {
            var raw = await TagService.ReadAsync<float>(
                RtdCalCheckTags.BuildNodeId(sensor.Name, RtdCalCheckTags.Fields.Value));
            var multiplier = await TagService.ReadAsync<float>(
                RtdCalCheckTags.BuildNodeId(sensor.Name, RtdCalCheckTags.Fields.Gain));
            var offset = await TagService.ReadAsync<float>(
                RtdCalCheckTags.BuildNodeId(sensor.Name, RtdCalCheckTags.Fields.Offset));
            var calculated = await TagService.ReadAsync<float>(
                RtdCalCheckTags.BuildNodeId(sensor.Name, RtdCalCheckTags.Fields.ValueAct));

            if (!raw.Success || !multiplier.Success || !offset.Success || !calculated.Success)
            {
                var errors = string.Join(", ", new[] { raw, multiplier, offset, calculated }
                    .Where(r => !r.Success)
                    .Select(r => r.Error));
                Logger.LogWarning("Не удалось прочитать RTD сенсор {Sensor}: {Errors}", sensor.Name, errors);
                continue;
            }

            _rtdItems.Add(new RtdCalibrationItem
            {
                PlcTag = sensor.Name,
                Type = "Pt100",
                Raw = raw.Value,
                Multiplier = multiplier.Value,
                Offset = offset.Value,
                Calculated = calculated.Value
            });
        }
    }

    /// <summary>
    /// Создаёт снимок текущих значений RTD для отслеживания изменений.
    /// </summary>
    private void CreateRtdSnapshot()
    {
        _rtdSnapshot = _rtdItems.Select(item => new RtdCalibrationItem
        {
            PlcTag = item.PlcTag,
            Type = item.Type,
            Raw = item.Raw,
            Multiplier = item.Multiplier,
            Offset = item.Offset,
            Calculated = item.Calculated
        }).ToList();
    }

    private bool IsRtdMultiplierChanged(RtdCalibrationItem item)
    {
        var index = _rtdItems.IndexOf(item);
        if (index < 0 || index >= _rtdSnapshot.Count)
        {
            return false;
        }
        return item.Multiplier != _rtdSnapshot[index].Multiplier;
    }

    private bool IsRtdOffsetChanged(RtdCalibrationItem item)
    {
        var index = _rtdItems.IndexOf(item);
        if (index < 0 || index >= _rtdSnapshot.Count)
        {
            return false;
        }
        return item.Offset != _rtdSnapshot[index].Offset;
    }

    private bool HasRtdUnsavedChanges
    {
        get
        {
            if (_rtdItems.Count != _rtdSnapshot.Count)
            {
                return false;
            }

            for (var i = 0; i < _rtdItems.Count; i++)
            {
                var item = _rtdItems[i];
                var snapshot = _rtdSnapshot[i];
                if (item.Multiplier != snapshot.Multiplier ||
                    item.Offset != snapshot.Offset)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Сохраняет изменения RTD в OPC.
    /// </summary>
    private async Task SaveRtdAsync()
    {
        _saveErrors.Clear();

        try
        {
            for (var i = 0; i < _rtdItems.Count; i++)
            {
                var item = _rtdItems[i];
                var snapshot = _rtdSnapshot[i];

                if (item.Multiplier != snapshot.Multiplier)
                {
                    var result = await TagService.WriteAsync(
                        RtdCalCheckTags.BuildNodeId(item.PlcTag, RtdCalCheckTags.Fields.Gain), (float)item.Multiplier);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.PlcTag}.Multiplier: {result.Error}");
                    }
                }

                if (item.Offset != snapshot.Offset)
                {
                    var result = await TagService.WriteAsync(
                        RtdCalCheckTags.BuildNodeId(item.PlcTag, RtdCalCheckTags.Fields.Offset), (float)item.Offset);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.PlcTag}.Offset: {result.Error}");
                    }
                }
            }

            if (_saveErrors.Count == 0)
            {
                CreateRtdSnapshot();
                Logger.LogInformation("RTD Calibration сохранено");
            }
            else
            {
                Logger.LogWarning("Частичная ошибка сохранения RTD Calibration: {Errors}",
                    string.Join("; ", _saveErrors));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка сохранения RTD Calibration");
        }
        finally
        {
            StateHasChanged();
        }
    }

    /// <summary>
    /// Сбрасывает изменения RTD к исходным значениям.
    /// </summary>
    private void ResetRtdChanges()
    {
        for (var i = 0; i < _rtdItems.Count; i++)
        {
            _rtdItems[i].Multiplier = _rtdSnapshot[i].Multiplier;
            _rtdItems[i].Offset = _rtdSnapshot[i].Offset;
        }
        StateHasChanged();
    }

    private async Task OnRtdCellClick(DataGridCellMouseEventArgs<RtdCalibrationItem> args)
    {
        if (_rtdItemToUpdate == args.Data && _rtdEditingColumn == args.Column.Property)
        {
            return;
        }

        if (_rtdItemToUpdate != null)
        {
            await _rtdGrid.UpdateRow(_rtdItemToUpdate);
        }

        _rtdItemToUpdate = args.Data;
        _rtdEditingColumn = args.Column.Property;

        await _rtdGrid.EditRow(args.Data);
        StateHasChanged();
    }

    private bool IsRtdEditing(string propertyName) => _rtdEditingColumn == propertyName;

    /// <summary>
    /// Завершает редактирование RTD при клике вне грида.
    /// </summary>
    private async Task CloseRtdEdit()
    {
        if (_rtdItemToUpdate == null) return;
        await _rtdGrid.UpdateRow(_rtdItemToUpdate);
        _rtdItemToUpdate = null;
        _rtdEditingColumn = null;
        await InvokeAsync(StateHasChanged);
    }

    private void OnRtdUpdateRow(RtdCalibrationItem item)
    {
        // Изменения уже применены через binding
    }

    #endregion

    #region PID Regulator

    /// <summary>
    /// Загружает данные PID Regulator из OPC.
    /// </summary>
    private async Task LoadPidDataAsync()
    {
        _pidItems = [];

        foreach (var regulator in PidRegulatorTags.Regulators)
        {
            var setPoint = await TagService.ReadAsync<float>(
                PidRegulatorTags.BuildNodeId(regulator.Name, PidRegulatorTags.Fields.SetPoint));
            var actuelValue = await TagService.ReadAsync<float>(
                PidRegulatorTags.BuildNodeId(regulator.Name, PidRegulatorTags.Fields.ActuelValue));
            var manualValue = await TagService.ReadAsync<float>(
                PidRegulatorTags.BuildNodeId(regulator.Name, PidRegulatorTags.Fields.ManualValue));
            var actuelloutValue = await TagService.ReadAsync<float>(
                PidRegulatorTags.BuildNodeId(regulator.Name, PidRegulatorTags.Fields.ActuelloutValue));
            var gain1 = await TagService.ReadAsync<float>(
                PidRegulatorTags.BuildNodeId(regulator.Name, PidRegulatorTags.Fields.Gain1));
            var ti1 = await TagService.ReadAsync<float>(
                PidRegulatorTags.BuildNodeId(regulator.Name, PidRegulatorTags.Fields.Ti1));
            var td1 = await TagService.ReadAsync<float>(
                PidRegulatorTags.BuildNodeId(regulator.Name, PidRegulatorTags.Fields.Td1));
            var gain2 = await TagService.ReadAsync<float>(
                PidRegulatorTags.BuildNodeId(regulator.Name, PidRegulatorTags.Fields.Gain2));
            var ti2 = await TagService.ReadAsync<float>(
                PidRegulatorTags.BuildNodeId(regulator.Name, PidRegulatorTags.Fields.Ti2));
            var td2 = await TagService.ReadAsync<float>(
                PidRegulatorTags.BuildNodeId(regulator.Name, PidRegulatorTags.Fields.Td2));

            var allResults = new[] { setPoint, actuelValue, manualValue, actuelloutValue, gain1, ti1, td1, gain2, ti2, td2 };
            if (allResults.Any(r => !r.Success))
            {
                var errors = string.Join(", ", allResults
                    .Where(r => !r.Success)
                    .Select(r => r.Error));
                Logger.LogWarning("Не удалось прочитать PID регулятор {Regulator}: {Errors}", regulator.Name, errors);
                continue;
            }

            _pidItems.Add(new PidRegulatorItem
            {
                PlcTag = regulator.Name,
                Type = "PID",
                SetPoint = setPoint.Value,
                ActuelValue = actuelValue.Value,
                ManualValue = manualValue.Value,
                ActuelloutValue = actuelloutValue.Value,
                Gain1 = gain1.Value,
                Ti1 = ti1.Value,
                Td1 = td1.Value,
                Gain2 = gain2.Value,
                Ti2 = ti2.Value,
                Td2 = td2.Value
            });
        }
    }

    /// <summary>
    /// Создаёт снимок текущих значений PID для отслеживания изменений.
    /// </summary>
    private void CreatePidSnapshot()
    {
        _pidSnapshot = _pidItems.Select(item => new PidRegulatorItem
        {
            PlcTag = item.PlcTag,
            Type = item.Type,
            SetPoint = item.SetPoint,
            ActuelValue = item.ActuelValue,
            ManualValue = item.ManualValue,
            ActuelloutValue = item.ActuelloutValue,
            Gain1 = item.Gain1,
            Ti1 = item.Ti1,
            Td1 = item.Td1,
            Gain2 = item.Gain2,
            Ti2 = item.Ti2,
            Td2 = item.Td2
        }).ToList();
    }

    private bool IsPidGain1Changed(PidRegulatorItem item)
    {
        var index = _pidItems.IndexOf(item);
        if (index < 0 || index >= _pidSnapshot.Count)
        {
            return false;
        }
        return item.Gain1 != _pidSnapshot[index].Gain1;
    }

    private bool IsPidTi1Changed(PidRegulatorItem item)
    {
        var index = _pidItems.IndexOf(item);
        if (index < 0 || index >= _pidSnapshot.Count)
        {
            return false;
        }
        return item.Ti1 != _pidSnapshot[index].Ti1;
    }

    private bool IsPidTd1Changed(PidRegulatorItem item)
    {
        var index = _pidItems.IndexOf(item);
        if (index < 0 || index >= _pidSnapshot.Count)
        {
            return false;
        }
        return item.Td1 != _pidSnapshot[index].Td1;
    }

    private bool IsPidGain2Changed(PidRegulatorItem item)
    {
        var index = _pidItems.IndexOf(item);
        if (index < 0 || index >= _pidSnapshot.Count)
        {
            return false;
        }
        return item.Gain2 != _pidSnapshot[index].Gain2;
    }

    private bool IsPidTi2Changed(PidRegulatorItem item)
    {
        var index = _pidItems.IndexOf(item);
        if (index < 0 || index >= _pidSnapshot.Count)
        {
            return false;
        }
        return item.Ti2 != _pidSnapshot[index].Ti2;
    }

    private bool IsPidTd2Changed(PidRegulatorItem item)
    {
        var index = _pidItems.IndexOf(item);
        if (index < 0 || index >= _pidSnapshot.Count)
        {
            return false;
        }
        return item.Td2 != _pidSnapshot[index].Td2;
    }

    private bool HasPidUnsavedChanges
    {
        get
        {
            if (_pidItems.Count != _pidSnapshot.Count)
            {
                return false;
            }

            for (var i = 0; i < _pidItems.Count; i++)
            {
                var item = _pidItems[i];
                var snapshot = _pidSnapshot[i];
                if (item.Gain1 != snapshot.Gain1 ||
                    item.Ti1 != snapshot.Ti1 ||
                    item.Td1 != snapshot.Td1 ||
                    item.Gain2 != snapshot.Gain2 ||
                    item.Ti2 != snapshot.Ti2 ||
                    item.Td2 != snapshot.Td2)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Сохраняет изменения PID в OPC.
    /// </summary>
    private async Task SavePidAsync()
    {
        _saveErrors.Clear();

        try
        {
            for (var i = 0; i < _pidItems.Count; i++)
            {
                var item = _pidItems[i];
                var snapshot = _pidSnapshot[i];

                if (item.Gain1 != snapshot.Gain1)
                {
                    var result = await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.PlcTag, PidRegulatorTags.Fields.Gain1), (float)item.Gain1);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.PlcTag}.Gain1: {result.Error}");
                    }
                }

                if (item.Ti1 != snapshot.Ti1)
                {
                    var result = await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.PlcTag, PidRegulatorTags.Fields.Ti1), (float)item.Ti1);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.PlcTag}.Ti1: {result.Error}");
                    }
                }

                if (item.Td1 != snapshot.Td1)
                {
                    var result = await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.PlcTag, PidRegulatorTags.Fields.Td1), (float)item.Td1);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.PlcTag}.Td1: {result.Error}");
                    }
                }

                if (item.Gain2 != snapshot.Gain2)
                {
                    var result = await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.PlcTag, PidRegulatorTags.Fields.Gain2), (float)item.Gain2);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.PlcTag}.Gain2: {result.Error}");
                    }
                }

                if (item.Ti2 != snapshot.Ti2)
                {
                    var result = await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.PlcTag, PidRegulatorTags.Fields.Ti2), (float)item.Ti2);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.PlcTag}.Ti2: {result.Error}");
                    }
                }

                if (item.Td2 != snapshot.Td2)
                {
                    var result = await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.PlcTag, PidRegulatorTags.Fields.Td2), (float)item.Td2);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.PlcTag}.Td2: {result.Error}");
                    }
                }
            }

            if (_saveErrors.Count == 0)
            {
                CreatePidSnapshot();
                Logger.LogInformation("PID Regulator сохранено");
            }
            else
            {
                Logger.LogWarning("Частичная ошибка сохранения PID Regulator: {Errors}",
                    string.Join("; ", _saveErrors));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка сохранения PID Regulator");
        }
        finally
        {
            StateHasChanged();
        }
    }

    /// <summary>
    /// Сбрасывает изменения PID к исходным значениям.
    /// </summary>
    private void ResetPidChanges()
    {
        for (var i = 0; i < _pidItems.Count; i++)
        {
            _pidItems[i].Gain1 = _pidSnapshot[i].Gain1;
            _pidItems[i].Ti1 = _pidSnapshot[i].Ti1;
            _pidItems[i].Td1 = _pidSnapshot[i].Td1;
            _pidItems[i].Gain2 = _pidSnapshot[i].Gain2;
            _pidItems[i].Ti2 = _pidSnapshot[i].Ti2;
            _pidItems[i].Td2 = _pidSnapshot[i].Td2;
        }
        StateHasChanged();
    }

    private async Task OnPidCellClick(DataGridCellMouseEventArgs<PidRegulatorItem> args)
    {
        if (_pidItemToUpdate == args.Data && _pidEditingColumn == args.Column.Property)
        {
            return;
        }

        if (_pidItemToUpdate != null)
        {
            await _pidGrid.UpdateRow(_pidItemToUpdate);
        }

        _pidItemToUpdate = args.Data;
        _pidEditingColumn = args.Column.Property;

        await _pidGrid.EditRow(args.Data);
        StateHasChanged();
    }

    private bool IsPidEditing(string propertyName) => _pidEditingColumn == propertyName;

    /// <summary>
    /// Завершает редактирование PID при клике вне грида.
    /// </summary>
    private async Task ClosePidEdit()
    {
        if (_pidItemToUpdate == null) return;
        await _pidGrid.UpdateRow(_pidItemToUpdate);
        _pidItemToUpdate = null;
        _pidEditingColumn = null;
        await InvokeAsync(StateHasChanged);
    }

    private void OnPidUpdateRow(PidRegulatorItem item)
    {
        // Изменения уже применены через binding
    }

    #endregion

    #region Analog Outputs

    /// <summary>
    /// Загружает данные Analog Outputs (PID-параметры) из OPC.
    /// </summary>
    private async Task LoadAoDataAsync()
    {
        _aoItems = [];

        var outputs = new[] { ("VRP2_1", "DHW Flow"), ("VPP3_1", "Blr Gas Pressure") };

        foreach (var (tag, desc) in outputs)
        {
            var gain1 = await TagService.ReadAsync<float>(
                PidRegulatorTags.BuildNodeId(tag, PidRegulatorTags.Fields.Gain1));
            var ti1 = await TagService.ReadAsync<float>(
                PidRegulatorTags.BuildNodeId(tag, PidRegulatorTags.Fields.Ti1));
            var td1 = await TagService.ReadAsync<float>(
                PidRegulatorTags.BuildNodeId(tag, PidRegulatorTags.Fields.Td1));

            var allResults = new[] { gain1, ti1, td1 };
            if (allResults.Any(r => !r.Success))
            {
                var errors = string.Join(", ", allResults
                    .Where(r => !r.Success)
                    .Select(r => r.Error));
                Logger.LogWarning("Не удалось прочитать AO {Tag}: {Errors}", tag, errors);
                continue;
            }

            _aoItems.Add(new AnalogOutputItem
            {
                TagName = tag,
                Description = desc,
                Gain1 = gain1.Value,
                Ti1 = ti1.Value,
                Td1 = td1.Value
            });
        }
    }

    /// <summary>
    /// Создаёт снимок текущих значений AO для отслеживания изменений.
    /// </summary>
    private void CreateAoSnapshot()
    {
        _aoSnapshot = _aoItems.Select(item => new AnalogOutputItem
        {
            TagName = item.TagName,
            Description = item.Description,
            Gain1 = item.Gain1,
            Ti1 = item.Ti1,
            Td1 = item.Td1
        }).ToList();
    }

    private bool IsAoGain1Changed(AnalogOutputItem item)
    {
        var index = _aoItems.IndexOf(item);
        if (index < 0 || index >= _aoSnapshot.Count)
        {
            return false;
        }
        return item.Gain1 != _aoSnapshot[index].Gain1;
    }

    private bool IsAoTi1Changed(AnalogOutputItem item)
    {
        var index = _aoItems.IndexOf(item);
        if (index < 0 || index >= _aoSnapshot.Count)
        {
            return false;
        }
        return item.Ti1 != _aoSnapshot[index].Ti1;
    }

    private bool IsAoTd1Changed(AnalogOutputItem item)
    {
        var index = _aoItems.IndexOf(item);
        if (index < 0 || index >= _aoSnapshot.Count)
        {
            return false;
        }
        return item.Td1 != _aoSnapshot[index].Td1;
    }

    private bool HasAoUnsavedChanges
    {
        get
        {
            if (_aoItems.Count != _aoSnapshot.Count)
            {
                return false;
            }

            for (var i = 0; i < _aoItems.Count; i++)
            {
                var item = _aoItems[i];
                var snapshot = _aoSnapshot[i];
                if (item.Gain1 != snapshot.Gain1 ||
                    item.Ti1 != snapshot.Ti1 ||
                    item.Td1 != snapshot.Td1)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Сохраняет изменения Analog Outputs в OPC.
    /// </summary>
    private async Task SaveAoAsync()
    {
        _saveErrors.Clear();

        try
        {
            for (var i = 0; i < _aoItems.Count; i++)
            {
                var item = _aoItems[i];
                var snapshot = _aoSnapshot[i];

                if (item.Gain1 != snapshot.Gain1)
                {
                    var result = await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.TagName, PidRegulatorTags.Fields.Gain1), (float)item.Gain1);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.TagName}.Gain1: {result.Error}");
                    }
                }

                if (item.Ti1 != snapshot.Ti1)
                {
                    var result = await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.TagName, PidRegulatorTags.Fields.Ti1), (float)item.Ti1);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.TagName}.Ti1: {result.Error}");
                    }
                }

                if (item.Td1 != snapshot.Td1)
                {
                    var result = await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.TagName, PidRegulatorTags.Fields.Td1), (float)item.Td1);
                    if (!result.Success)
                    {
                        _saveErrors.Add($"{item.TagName}.Td1: {result.Error}");
                    }
                }
            }

            if (_saveErrors.Count == 0)
            {
                CreateAoSnapshot();
                Logger.LogInformation("Analog Outputs сохранено");
            }
            else
            {
                Logger.LogWarning("Частичная ошибка сохранения Analog Outputs: {Errors}",
                    string.Join("; ", _saveErrors));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка сохранения Analog Outputs");
        }
        finally
        {
            StateHasChanged();
        }
    }

    /// <summary>
    /// Сбрасывает изменения AO к исходным значениям.
    /// </summary>
    private void ResetAoChanges()
    {
        for (var i = 0; i < _aoItems.Count; i++)
        {
            _aoItems[i].Gain1 = _aoSnapshot[i].Gain1;
            _aoItems[i].Ti1 = _aoSnapshot[i].Ti1;
            _aoItems[i].Td1 = _aoSnapshot[i].Td1;
        }
        StateHasChanged();
    }

    private async Task OnAoCellClick(DataGridCellMouseEventArgs<AnalogOutputItem> args)
    {
        if (_aoItemToUpdate == args.Data && _aoEditingColumn == args.Column.Property)
        {
            return;
        }

        if (_aoItemToUpdate != null)
        {
            await _aoGrid.UpdateRow(_aoItemToUpdate);
        }

        _aoItemToUpdate = args.Data;
        _aoEditingColumn = args.Column.Property;

        await _aoGrid.EditRow(args.Data);
        StateHasChanged();
    }

    private bool IsAoEditing(string propertyName) => _aoEditingColumn == propertyName;

    /// <summary>
    /// Завершает редактирование AO при клике вне грида.
    /// </summary>
    private async Task CloseAoEdit()
    {
        if (_aoItemToUpdate == null) return;
        await _aoGrid.UpdateRow(_aoItemToUpdate);
        _aoItemToUpdate = null;
        _aoEditingColumn = null;
        await InvokeAsync(StateHasChanged);
    }

    private void OnAoUpdateRow(AnalogOutputItem item)
    {
        // Изменения уже применены через binding
    }

    #endregion

    #region Data Classes

    /// <summary>
    /// Хелпер для вызова CloseEdit из JS через DotNetObjectReference.
    /// </summary>
    private sealed class OutsideClickHelper : IDisposable
    {
        private readonly Func<Task> _closeAction;
        private DotNetObjectReference<OutsideClickHelper>? _ref;

        public OutsideClickHelper(Func<Task> closeAction)
        {
            _closeAction = closeAction;
            _ref = DotNetObjectReference.Create(this);
        }

        public DotNetObjectReference<OutsideClickHelper> Reference => _ref!;

        [JSInvokable]
        public Task CloseEdit() => _closeAction();

        public void Dispose() => _ref?.Dispose();
    }

    /// <summary>
    /// Модель данных для AI Calibration.
    /// </summary>
    public class AiCalibrationItem
    {
        public string SensorName { get; set; } = "";
        public string Description { get; set; } = "";
        public double Min { get; set; }
        public double Max { get; set; }
        public double Multiplier { get; set; }
        public double Offset { get; set; }
    }

    /// <summary>
    /// Модель данных для RTD Calibration.
    /// </summary>
    public class RtdCalibrationItem
    {
        public string PlcTag { get; set; } = "";
        public string Type { get; set; } = "";
        public double Raw { get; set; }
        public double Multiplier { get; set; }
        public double Offset { get; set; }
        public double Calculated { get; set; }
    }

    /// <summary>
    /// Модель данных для PID Regulator.
    /// </summary>
    public class PidRegulatorItem
    {
        public string PlcTag { get; set; } = "";
        public string Type { get; set; } = "";
        public double SetPoint { get; set; }
        public double ActuelValue { get; set; }
        public double ManualValue { get; set; }
        public double ActuelloutValue { get; set; }
        public double Gain1 { get; set; }
        public double Ti1 { get; set; }
        public double Td1 { get; set; }
        public double Gain2 { get; set; }
        public double Ti2 { get; set; }
        public double Td2 { get; set; }
    }

    /// <summary>
    /// Модель данных для Analog Output (PID-параметры).
    /// </summary>
    public class AnalogOutputItem
    {
        public string TagName { get; set; } = "";
        public string Description { get; set; } = "";
        public double Gain1 { get; set; }
        public double Ti1 { get; set; }
        public double Td1 { get; set; }
    }

    #endregion
}
