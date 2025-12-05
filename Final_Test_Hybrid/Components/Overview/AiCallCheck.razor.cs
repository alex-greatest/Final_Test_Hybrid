using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Overview;

public partial class AiCallCheck : IAsyncDisposable
{
    private RadzenDataGrid<AiCallCheckItem> grid = null!;
    private List<AiCallCheckItem> items = new();

    private AiCallCheckItem? itemToUpdate;
    private string? editingColumn;

    private string containerId = $"ai-call-check-{Guid.NewGuid()}";
    private DotNetObjectReference<AiCallCheck>? dotNetHelper;
    private bool outsideClickHandlerRegistered;

    protected override void OnInitialized()
    {
        items = Enumerable.Range(0, 11).Select(i => new AiCallCheckItem
        {
            PlcTag = i == 0 ? "BlrSupply" : "Tag_" + i,
            Type = "S[4.20].HW[4.20] mA",
        }).ToList();
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

    private async Task OnCellClick(DataGridCellMouseEventArgs<AiCallCheckItem> args)
    {
        await SwitchEditMode(args.Data, args.Column.Property);
    }

    private async Task SwitchEditMode(AiCallCheckItem item, string property)
    {
        if (itemToUpdate == item && editingColumn == property)
        {
            return;
        }

        await ChangeEditMode(item, property);
    }

    private async Task ChangeEditMode(AiCallCheckItem item, string property)
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

    private void OnUpdateRow(AiCallCheckItem item)
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
            System.Diagnostics.Debug.WriteLine($"AiCallCheck outsideClick handler registration failed: {ex.Message}");
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

