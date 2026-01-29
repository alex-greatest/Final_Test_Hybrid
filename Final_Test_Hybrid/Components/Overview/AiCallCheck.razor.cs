using Final_Test_Hybrid.Components.Base;
using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Overview;

public partial class AiCallCheck : GridInplaceEditorBase<AiCallCheck.AiCallCheckItem>
{
    [Inject]
    private OpcUaSubscription Subscription { get; set; } = null!;

    [Inject]
    private OpcUaTagService TagService { get; set; } = null!;

    [Inject]
    private DualLogger<AiCallCheck> Logger { get; set; } = null!;

    private readonly Dictionary<string, Func<object?, Task>> _callbacks = new();
    private readonly Dictionary<AiCallCheckItem, string> _pendingEdits = new();
    private bool _disposed;

    /// <summary>
    /// Обрабатывает клик по ячейке и отслеживает редактируемое поле для конкретного элемента.
    /// </summary>
    protected new async Task OnCellClick(DataGridCellMouseEventArgs<AiCallCheckItem> args)
    {
        await base.OnCellClick(args);
        _pendingEdits[args.Data] = args.Column.Property;
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
        Items = AiCallCheckTags.Sensors.Select(sensor => new AiCallCheckItem
        {
            PlcTag = sensor.Name,
            Type = "S[4.20].HW[4.20] mA"
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
    private async Task SubscribeToSensorFieldsAsync(AiCallCheckItem item)
    {
        await SubscribeFieldAsync(item, AiCallCheckTags.Fields.Value, v => item.Raw = ToDouble(v));
        await SubscribeFieldAsync(item, AiCallCheckTags.Fields.LimMin, v => item.Min = ToDouble(v));
        await SubscribeFieldAsync(item, AiCallCheckTags.Fields.LimMax, v => item.Max = ToDouble(v));
        await SubscribeFieldAsync(item, AiCallCheckTags.Fields.Gain, v => item.Multiplier = ToDouble(v));
        await SubscribeFieldAsync(item, AiCallCheckTags.Fields.Offset, v => item.Offset = ToDouble(v));
        await SubscribeFieldAsync(item, AiCallCheckTags.Fields.ValueAct, v => item.Calculated = ToDouble(v));
    }

    /// <summary>
    /// Подписывается на одно поле датчика.
    /// </summary>
    private async Task SubscribeFieldAsync(AiCallCheckItem item, string field, Action<object?> setter)
    {
        var nodeId = AiCallCheckTags.BuildNodeId(item.PlcTag, field);

        Task Callback(object? value)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

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
    protected override void OnUpdateRow(AiCallCheckItem item)
    {
        if (!_pendingEdits.TryGetValue(item, out var editedField))
        {
            return;
        }

        _pendingEdits.Remove(item);
        _ = WriteChangedFieldAsync(item, editedField);
    }

    /// <summary>
    /// Записывает измененное поле в PLC.
    /// </summary>
    private async Task WriteChangedFieldAsync(AiCallCheckItem item, string editedField)
    {
        var (field, value) = GetFieldMapping(item, editedField);
        if (field == null)
        {
            return;
        }

        var nodeId = AiCallCheckTags.BuildNodeId(item.PlcTag, field);

        try
        {
            await TagService.WriteAsync(nodeId, (float)value);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка записи {Field} для {Tag}", field, item.PlcTag);
        }
    }

    /// <summary>
    /// Маппинг UI-поля на PLC-поле и значение (только редактируемые поля).
    /// </summary>
    private static (string? Field, double Value) GetFieldMapping(AiCallCheckItem item, string uiField) => uiField switch
    {
        nameof(AiCallCheckItem.Min) => (AiCallCheckTags.Fields.LimMin, item.Min),
        nameof(AiCallCheckItem.Max) => (AiCallCheckTags.Fields.LimMax, item.Max),
        nameof(AiCallCheckItem.Multiplier) => (AiCallCheckTags.Fields.Gain, item.Multiplier),
        nameof(AiCallCheckItem.Offset) => (AiCallCheckTags.Fields.Offset, item.Offset),
        _ => (null, 0)
    };

    public override async ValueTask DisposeAsync()
    {
        _disposed = true;
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

    public class AiCallCheckItem
    {
        public string PlcTag { get; set; } = "";
        public string Type { get; set; } = "";
        public double Raw { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Multiplier { get; set; }
        public double Offset { get; set; }
        public double Calculated { get; set; }
    }
}
