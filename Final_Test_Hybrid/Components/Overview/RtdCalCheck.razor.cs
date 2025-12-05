using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Overview;

public partial class RtdCalCheck : IAsyncDisposable
{
    private RadzenDataGrid<RtdCalCheckItem> grid = null!;
    private List<RtdCalCheckItem> items = new();

    private RtdCalCheckItem? itemToUpdate;
    private string? editingColumn;

    private string containerId = $"rtd-cal-check-{Guid.NewGuid()}";
    private DotNetObjectReference<RtdCalCheck>? dotNetHelper;
    private bool outsideClickHandlerRegistered;

    protected override void OnInitialized()
    {
        items = new List<RtdCalCheckItem>
        {
            new() { PlcTag = "TAG", Type = "Pt100", Raw = 21.700, Multiplier = 1.000, Offset = 0.000, Calculated = 21.700 },
            new() { PlcTag = "CH_TMR", Type = "Pt100", Raw = 21.700, Multiplier = 1.000, Offset = 0.000, Calculated = 21.700 },
            new() { PlcTag = "CH_TRR", Type = "Pt100", Raw = 21.700, Multiplier = 1.000, Offset = 0.000, Calculated = 21.700 },
            new() { PlcTag = "DHW_TES", Type = "Pt100", Raw = 21.700, Multiplier = 1.000, Offset = 0.000, Calculated = 21.700 },
            new() { PlcTag = "DHW_TUS", Type = "Pt100", Raw = 21.700, Multiplier = 1.000, Offset = 0.000, Calculated = 21.700 },
        };
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RegisterOutsideClickHandler();
        }
    }

    // Helper to check if a specific column in the current row is being edited
    private bool IsEditing(string propertyName)
    {
        return editingColumn == propertyName;
    }

    private async Task OnCellClick(DataGridCellMouseEventArgs<RtdCalCheckItem> args)
    {
        await SwitchEditMode(args.Data, args.Column.Property);
    }

    private async Task SwitchEditMode(RtdCalCheckItem item, string property)
    {
        if (itemToUpdate == item && editingColumn == property)
        {
            return;
        }

        await ChangeEditMode(item, property);
    }

    private async Task ChangeEditMode(RtdCalCheckItem item, string property)
    {
        await CommitPreviousChanges();

        itemToUpdate = item;
        editingColumn = property;

        await grid.EditRow(item);
    }

    private async Task CommitPreviousChanges()
    {
        if (itemToUpdate == null)
        {
            return;
        }
        await grid.UpdateRow(itemToUpdate);
    }

    [JSInvokable]
    public async Task CloseEdit()
    {
        if (itemToUpdate == null)
        {
            return;
        }

        await grid.UpdateRow(itemToUpdate);
        itemToUpdate = null;
        editingColumn = null;
        await InvokeAsync(StateHasChanged);
    }

    private void OnUpdateRow(RtdCalCheckItem item)
    {
        // Update logic
    }

    private async Task RegisterOutsideClickHandler()
    {
        dotNetHelper = DotNetObjectReference.Create(this);
        await TryRegisterHandler();
    }

    private async Task TryRegisterHandler()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("outsideClickHandler.add", containerId, dotNetHelper);
            outsideClickHandlerRegistered = true;
        }
        catch (JSException ex)
        {
            outsideClickHandlerRegistered = false;
            dotNetHelper?.Dispose();
            dotNetHelper = null;
            System.Diagnostics.Debug.WriteLine($"RtdCalCheck outsideClick handler registration failed: {ex.Message}");
        }
    }

    private async Task UnregisterOutsideClickHandler()
    {
        if (!outsideClickHandlerRegistered || dotNetHelper is null)
        {
            return;
        }

        await SafeRemoveHandler();
    }

    private async Task SafeRemoveHandler()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("outsideClickHandler.remove", containerId);
        }
        catch
        {
            /* Ignored */
        }
    }

    public async ValueTask DisposeAsync()
    {
        await UnregisterOutsideClickHandler();
        dotNetHelper?.Dispose();
        dotNetHelper = null;
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

