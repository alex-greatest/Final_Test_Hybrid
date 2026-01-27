using Final_Test_Hybrid.Components.Base;
using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Overview;

public partial class RtdCalCheck : GridInplaceEditorBase<RtdCalCheck.RtdCalCheckItem>
{
    [Inject]
    private OpcUaSubscription Subscription { get; set; } = null!;

    [Inject]
    private OpcUaTagService TagService { get; set; } = null!;

    private readonly Dictionary<string, Func<object?, Task>> _callbacks = new();
    private string? _lastEditedField;

    /// <summary>
    /// Обрабатывает клик по ячейке и отслеживает редактируемое поле.
    /// </summary>
    protected new async Task OnCellClick(DataGridCellMouseEventArgs<RtdCalCheckItem> args)
    {
        _lastEditedField = args.Column.Property;
        await base.OnCellClick(args);
    }

    protected override async Task OnInitializedAsync()
    {
        InitializeItems();
        await SubscribeToAllSensorsAsync();
    }

    /// <summary>
    /// Инициализирует список элементов из тегов.
    /// </summary>
    private void InitializeItems()
    {
        Items = RtdCalCheckTags.Sensors.Select(sensor => new RtdCalCheckItem
        {
            PlcTag = sensor.Name,
            Type = "Pt100"
        }).ToList();
    }

    /// <summary>
    /// Подписывается на все поля всех датчиков.
    /// </summary>
    private async Task SubscribeToAllSensorsAsync()
    {
        foreach (var item in Items)
        {
            await SubscribeToSensorFieldsAsync(item);
        }
    }

    /// <summary>
    /// Подписывается на все поля одного датчика.
    /// </summary>
    private async Task SubscribeToSensorFieldsAsync(RtdCalCheckItem item)
    {
        await SubscribeFieldAsync(item, RtdCalCheckTags.Fields.Value, v => item.Raw = ToDouble(v));
        await SubscribeFieldAsync(item, RtdCalCheckTags.Fields.Gain, v => item.Multiplier = ToDouble(v));
        await SubscribeFieldAsync(item, RtdCalCheckTags.Fields.Offset, v => item.Offset = ToDouble(v));
        await SubscribeFieldAsync(item, RtdCalCheckTags.Fields.ValueAct, v => item.Calculated = ToDouble(v));
    }

    /// <summary>
    /// Подписывается на одно поле датчика.
    /// </summary>
    private async Task SubscribeFieldAsync(RtdCalCheckItem item, string field, Action<object?> setter)
    {
        var nodeId = RtdCalCheckTags.BuildNodeId(item.PlcTag, field);

        Task Callback(object? value)
        {
            setter(value);
            return InvokeAsync(StateHasChanged);
        }

        _callbacks[nodeId] = Callback;
        await Subscription.SubscribeAsync(nodeId, Callback);
    }

    /// <summary>
    /// Конвертирует значение в double.
    /// </summary>
    private static double ToDouble(object? value) => value switch
    {
        float f => f,
        double d => d,
        int i => i,
        short s => s,
        _ => 0
    };

    /// <summary>
    /// Обрабатывает обновление строки и записывает изменения в PLC.
    /// </summary>
    protected override void OnUpdateRow(RtdCalCheckItem item)
    {
        _ = WriteChangedFieldAsync(item);
    }

    /// <summary>
    /// Записывает измененное поле в PLC.
    /// </summary>
    private async Task WriteChangedFieldAsync(RtdCalCheckItem item)
    {
        if (_lastEditedField == null)
        {
            return;
        }

        var (field, value) = GetFieldMapping(item, _lastEditedField);
        if (field == null)
        {
            return;
        }

        var nodeId = RtdCalCheckTags.BuildNodeId(item.PlcTag, field);
        await TagService.WriteAsync(nodeId, (float)value);
        _lastEditedField = null;
    }

    /// <summary>
    /// Маппинг UI-поля на PLC-поле и значение (только редактируемые поля).
    /// </summary>
    private static (string? Field, double Value) GetFieldMapping(RtdCalCheckItem item, string uiField) => uiField switch
    {
        nameof(RtdCalCheckItem.Multiplier) => (RtdCalCheckTags.Fields.Gain, item.Multiplier),
        nameof(RtdCalCheckItem.Offset) => (RtdCalCheckTags.Fields.Offset, item.Offset),
        _ => (null, 0)
    };

    public new async ValueTask DisposeAsync()
    {
        await UnsubscribeAllAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Отписывается от всех подписок.
    /// </summary>
    private async Task UnsubscribeAllAsync()
    {
        foreach (var (nodeId, callback) in _callbacks)
        {
            await Subscription.UnsubscribeAsync(nodeId, callback, removeTag: false, ct: CancellationToken.None);
        }
        _callbacks.Clear();
    }

    public class RtdCalCheckItem
    {
        public string PlcTag { get; set; } = "";
        public string Type { get; set; } = "";
        public double Raw { get; set; }
        public double Multiplier { get; set; }
        public double Offset { get; set; }
        public double Calculated { get; set; }
    }
}
