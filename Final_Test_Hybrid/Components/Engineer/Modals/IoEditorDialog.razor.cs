using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Engineer.Modals;

/// <summary>
/// Code-behind для диалога редактирования IO: AI Calibration, RTD Calibration, PID Regulator.
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

    // === COMMON ===
    private bool _isLoading = true;
    private bool _disposed;

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

    #region Lifecycle

    /// <summary>
    /// Инициализирует компонент: подписывается на события и загружает данные.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        SubscriptionState.OnStateChanged += HandleStateChanged;
        ConnectionState.ConnectionStateChanged += HandleConnectionChanged;

        try
        {
            await LoadAiDataAsync();
            CreateAiSnapshot();

            await LoadRtdDataAsync();
            CreateRtdSnapshot();

            await LoadPidDataAsync();
            CreatePidSnapshot();
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
    /// Освобождает ресурсы компонента.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        SubscriptionState.OnStateChanged -= HandleStateChanged;
        ConnectionState.ConnectionStateChanged -= HandleConnectionChanged;
        await Task.CompletedTask;
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
        return Math.Abs(item.Min - _aiSnapshot[index].Min) > 0.0001;
    }

    private bool IsAiMaxChanged(AiCalibrationItem item)
    {
        var index = _aiItems.IndexOf(item);
        if (index < 0 || index >= _aiSnapshot.Count)
        {
            return false;
        }
        return Math.Abs(item.Max - _aiSnapshot[index].Max) > 0.0001;
    }

    private bool IsAiMultiplierChanged(AiCalibrationItem item)
    {
        var index = _aiItems.IndexOf(item);
        if (index < 0 || index >= _aiSnapshot.Count)
        {
            return false;
        }
        return Math.Abs(item.Multiplier - _aiSnapshot[index].Multiplier) > 0.0001;
    }

    private bool IsAiOffsetChanged(AiCalibrationItem item)
    {
        var index = _aiItems.IndexOf(item);
        if (index < 0 || index >= _aiSnapshot.Count)
        {
            return false;
        }
        return Math.Abs(item.Offset - _aiSnapshot[index].Offset) > 0.0001;
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
                if (Math.Abs(item.Min - snapshot.Min) > 0.0001 ||
                    Math.Abs(item.Max - snapshot.Max) > 0.0001 ||
                    Math.Abs(item.Multiplier - snapshot.Multiplier) > 0.0001 ||
                    Math.Abs(item.Offset - snapshot.Offset) > 0.0001)
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
        try
        {
            for (var i = 0; i < _aiItems.Count; i++)
            {
                var item = _aiItems[i];
                var snapshot = _aiSnapshot[i];

                if (Math.Abs(item.Min - snapshot.Min) > 0.0001)
                {
                    await TagService.WriteAsync(
                        AiCallCheckTags.BuildNodeId(item.SensorName, AiCallCheckTags.Fields.LimMin), (float)item.Min);
                }

                if (Math.Abs(item.Max - snapshot.Max) > 0.0001)
                {
                    await TagService.WriteAsync(
                        AiCallCheckTags.BuildNodeId(item.SensorName, AiCallCheckTags.Fields.LimMax), (float)item.Max);
                }

                if (Math.Abs(item.Multiplier - snapshot.Multiplier) > 0.0001)
                {
                    await TagService.WriteAsync(
                        AiCallCheckTags.BuildNodeId(item.SensorName, AiCallCheckTags.Fields.Gain), (float)item.Multiplier);
                }

                if (Math.Abs(item.Offset - snapshot.Offset) > 0.0001)
                {
                    await TagService.WriteAsync(
                        AiCallCheckTags.BuildNodeId(item.SensorName, AiCallCheckTags.Fields.Offset), (float)item.Offset);
                }
            }

            CreateAiSnapshot();
            Logger.LogInformation("AI Calibration сохранено");
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
        return Math.Abs(item.Multiplier - _rtdSnapshot[index].Multiplier) > 0.0001;
    }

    private bool IsRtdOffsetChanged(RtdCalibrationItem item)
    {
        var index = _rtdItems.IndexOf(item);
        if (index < 0 || index >= _rtdSnapshot.Count)
        {
            return false;
        }
        return Math.Abs(item.Offset - _rtdSnapshot[index].Offset) > 0.0001;
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
                if (Math.Abs(item.Multiplier - snapshot.Multiplier) > 0.0001 ||
                    Math.Abs(item.Offset - snapshot.Offset) > 0.0001)
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
        try
        {
            for (var i = 0; i < _rtdItems.Count; i++)
            {
                var item = _rtdItems[i];
                var snapshot = _rtdSnapshot[i];

                if (Math.Abs(item.Multiplier - snapshot.Multiplier) > 0.0001)
                {
                    await TagService.WriteAsync(
                        RtdCalCheckTags.BuildNodeId(item.PlcTag, RtdCalCheckTags.Fields.Gain), (float)item.Multiplier);
                }

                if (Math.Abs(item.Offset - snapshot.Offset) > 0.0001)
                {
                    await TagService.WriteAsync(
                        RtdCalCheckTags.BuildNodeId(item.PlcTag, RtdCalCheckTags.Fields.Offset), (float)item.Offset);
                }
            }

            CreateRtdSnapshot();
            Logger.LogInformation("RTD Calibration сохранено");
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
        return Math.Abs(item.Gain1 - _pidSnapshot[index].Gain1) > 0.0001;
    }

    private bool IsPidTi1Changed(PidRegulatorItem item)
    {
        var index = _pidItems.IndexOf(item);
        if (index < 0 || index >= _pidSnapshot.Count)
        {
            return false;
        }
        return Math.Abs(item.Ti1 - _pidSnapshot[index].Ti1) > 0.0001;
    }

    private bool IsPidTd1Changed(PidRegulatorItem item)
    {
        var index = _pidItems.IndexOf(item);
        if (index < 0 || index >= _pidSnapshot.Count)
        {
            return false;
        }
        return Math.Abs(item.Td1 - _pidSnapshot[index].Td1) > 0.0001;
    }

    private bool IsPidGain2Changed(PidRegulatorItem item)
    {
        var index = _pidItems.IndexOf(item);
        if (index < 0 || index >= _pidSnapshot.Count)
        {
            return false;
        }
        return Math.Abs(item.Gain2 - _pidSnapshot[index].Gain2) > 0.0001;
    }

    private bool IsPidTi2Changed(PidRegulatorItem item)
    {
        var index = _pidItems.IndexOf(item);
        if (index < 0 || index >= _pidSnapshot.Count)
        {
            return false;
        }
        return Math.Abs(item.Ti2 - _pidSnapshot[index].Ti2) > 0.0001;
    }

    private bool IsPidTd2Changed(PidRegulatorItem item)
    {
        var index = _pidItems.IndexOf(item);
        if (index < 0 || index >= _pidSnapshot.Count)
        {
            return false;
        }
        return Math.Abs(item.Td2 - _pidSnapshot[index].Td2) > 0.0001;
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
                if (Math.Abs(item.Gain1 - snapshot.Gain1) > 0.0001 ||
                    Math.Abs(item.Ti1 - snapshot.Ti1) > 0.0001 ||
                    Math.Abs(item.Td1 - snapshot.Td1) > 0.0001 ||
                    Math.Abs(item.Gain2 - snapshot.Gain2) > 0.0001 ||
                    Math.Abs(item.Ti2 - snapshot.Ti2) > 0.0001 ||
                    Math.Abs(item.Td2 - snapshot.Td2) > 0.0001)
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
        try
        {
            for (var i = 0; i < _pidItems.Count; i++)
            {
                var item = _pidItems[i];
                var snapshot = _pidSnapshot[i];

                if (Math.Abs(item.Gain1 - snapshot.Gain1) > 0.0001)
                {
                    await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.PlcTag, PidRegulatorTags.Fields.Gain1), (float)item.Gain1);
                }

                if (Math.Abs(item.Ti1 - snapshot.Ti1) > 0.0001)
                {
                    await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.PlcTag, PidRegulatorTags.Fields.Ti1), (float)item.Ti1);
                }

                if (Math.Abs(item.Td1 - snapshot.Td1) > 0.0001)
                {
                    await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.PlcTag, PidRegulatorTags.Fields.Td1), (float)item.Td1);
                }

                if (Math.Abs(item.Gain2 - snapshot.Gain2) > 0.0001)
                {
                    await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.PlcTag, PidRegulatorTags.Fields.Gain2), (float)item.Gain2);
                }

                if (Math.Abs(item.Ti2 - snapshot.Ti2) > 0.0001)
                {
                    await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.PlcTag, PidRegulatorTags.Fields.Ti2), (float)item.Ti2);
                }

                if (Math.Abs(item.Td2 - snapshot.Td2) > 0.0001)
                {
                    await TagService.WriteAsync(
                        PidRegulatorTags.BuildNodeId(item.PlcTag, PidRegulatorTags.Fields.Td2), (float)item.Td2);
                }
            }

            CreatePidSnapshot();
            Logger.LogInformation("PID Regulator сохранено");
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

    private void OnPidUpdateRow(PidRegulatorItem item)
    {
        // Изменения уже применены через binding
    }

    #endregion

    #region Data Classes

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

    #endregion
}
