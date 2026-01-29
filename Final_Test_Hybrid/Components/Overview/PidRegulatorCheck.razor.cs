using Final_Test_Hybrid.Components.Base;
using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Overview;

public partial class PidRegulatorCheck : GridInplaceEditorBase<PidRegulatorCheck.PidRegulatorItem>
{
    [Inject]
    private OpcUaSubscription Subscription { get; set; } = null!;

    [Inject]
    private OpcUaTagService TagService { get; set; } = null!;

    [Inject]
    private DualLogger<PidRegulatorCheck> Logger { get; set; } = null!;

    private readonly Dictionary<string, Func<object?, Task>> _callbacks = new();
    private readonly Dictionary<PidRegulatorItem, string> _pendingEdits = new();
    private bool _disposed;

    /// <summary>
    /// Обрабатывает клик по ячейке и отслеживает редактируемое поле для конкретного элемента.
    /// </summary>
    protected new async Task OnCellClick(DataGridCellMouseEventArgs<PidRegulatorItem> args)
    {
        await base.OnCellClick(args);
        _pendingEdits[args.Data] = args.Column.Property;
    }

    protected override async Task OnInitializedAsync()
    {
        InitializeItems();
        await SubscribeToAllRegulatorsAsync();
    }

    /// <summary>
    /// Инициализирует список элементов из тегов.
    /// </summary>
    private void InitializeItems()
    {
        Items = PidRegulatorTags.Regulators.Select(regulator => new PidRegulatorItem
        {
            PlcTag = regulator.Name,
            Type = "PID"
        }).ToList();
    }

    /// <summary>
    /// Подписывается на все поля всех регуляторов.
    /// </summary>
    private async Task SubscribeToAllRegulatorsAsync()
    {
        foreach (var item in Items)
        {
            await SubscribeToRegulatorFieldsAsync(item);
        }
    }

    /// <summary>
    /// Подписывается на все поля одного регулятора.
    /// </summary>
    private async Task SubscribeToRegulatorFieldsAsync(PidRegulatorItem item)
    {
        await SubscribeFieldAsync(item, PidRegulatorTags.Fields.SetPoint, v => item.SetPoint = ToDouble(v));
        await SubscribeFieldAsync(item, PidRegulatorTags.Fields.ActuelValue, v => item.ActuelValue = ToDouble(v));
        await SubscribeFieldAsync(item, PidRegulatorTags.Fields.ManualValue, v => item.ManualValue = ToDouble(v));
        await SubscribeFieldAsync(item, PidRegulatorTags.Fields.ActuelloutValue, v => item.ActuelloutValue = ToDouble(v));
        await SubscribeFieldAsync(item, PidRegulatorTags.Fields.Gain1, v => item.Gain1 = ToDouble(v));
        await SubscribeFieldAsync(item, PidRegulatorTags.Fields.Ti1, v => item.Ti1 = ToDouble(v));
        await SubscribeFieldAsync(item, PidRegulatorTags.Fields.Td1, v => item.Td1 = ToDouble(v));
        await SubscribeFieldAsync(item, PidRegulatorTags.Fields.Gain2, v => item.Gain2 = ToDouble(v));
        await SubscribeFieldAsync(item, PidRegulatorTags.Fields.Ti2, v => item.Ti2 = ToDouble(v));
        await SubscribeFieldAsync(item, PidRegulatorTags.Fields.Td2, v => item.Td2 = ToDouble(v));
    }

    /// <summary>
    /// Подписывается на одно поле регулятора.
    /// </summary>
    private async Task SubscribeFieldAsync(PidRegulatorItem item, string field, Action<object?> setter)
    {
        var nodeId = PidRegulatorTags.BuildNodeId(item.PlcTag, field);

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
    protected override void OnUpdateRow(PidRegulatorItem item)
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
    private async Task WriteChangedFieldAsync(PidRegulatorItem item, string editedField)
    {
        var (field, value) = GetFieldMapping(item, editedField);
        if (field == null)
        {
            return;
        }

        var nodeId = PidRegulatorTags.BuildNodeId(item.PlcTag, field);

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
    private static (string? Field, double Value) GetFieldMapping(PidRegulatorItem item, string uiField) => uiField switch
    {
        nameof(PidRegulatorItem.Gain1) => (PidRegulatorTags.Fields.Gain1, item.Gain1),
        nameof(PidRegulatorItem.Ti1) => (PidRegulatorTags.Fields.Ti1, item.Ti1),
        nameof(PidRegulatorItem.Td1) => (PidRegulatorTags.Fields.Td1, item.Td1),
        nameof(PidRegulatorItem.Gain2) => (PidRegulatorTags.Fields.Gain2, item.Gain2),
        nameof(PidRegulatorItem.Ti2) => (PidRegulatorTags.Fields.Ti2, item.Ti2),
        nameof(PidRegulatorItem.Td2) => (PidRegulatorTags.Fields.Td2, item.Td2),
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
}
