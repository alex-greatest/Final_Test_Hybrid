using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Base;

public abstract class GridInplaceEditorBase<TItem> : ComponentBase, IAsyncDisposable where TItem : class
{
    [Inject]
    protected IJSRuntime JsRuntime { get; set; } = null!;
    protected RadzenDataGrid<TItem> Grid { get; set; } = null!;
    protected List<TItem> Items { get; set; } = [];
    private TItem? _itemToUpdate;
    private string? _editingColumn;
    protected string ContainerId { get; set; } = $"grid-editor-{Guid.NewGuid()}";
    private DotNetObjectReference<GridInplaceEditorBase<TItem>>? _dotNetHelper;
    private bool _outsideClickHandlerRegistered;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RegisterOutsideClickHandler();
        }
    }

    protected bool IsEditing(string propertyName)
    {
        return _editingColumn == propertyName;
    }

    protected async Task OnCellClick(DataGridCellMouseEventArgs<TItem> args)
    {
        await SwitchEditMode(args.Data, args.Column.Property);
    }

    private async Task SwitchEditMode(TItem item, string property)
    {
        if (_itemToUpdate == item && _editingColumn == property)
        {
            return;
        }

        await ChangeEditMode(item, property);
    }

    private async Task ChangeEditMode(TItem item, string property)
    {
        await CommitPreviousChanges();

        _itemToUpdate = item;
        _editingColumn = property;

        await Grid.EditRow(item);
    }

    private async Task CommitPreviousChanges()
    {
        if (_itemToUpdate == null)
        {
            return;
        }
        await Grid.UpdateRow(_itemToUpdate);
    }

    [JSInvokable]
    public async Task CloseEdit()
    {
        if (_itemToUpdate == null)
        {
            return;
        }

        await Grid.UpdateRow(_itemToUpdate);
        _itemToUpdate = null;
        _editingColumn = null;
        await InvokeAsync(StateHasChanged);
    }

    protected virtual void OnUpdateRow(TItem item)
    {
        // Override if needed
    }

    private async Task RegisterOutsideClickHandler()
    {
        _dotNetHelper = DotNetObjectReference.Create(this);
        await TryRegisterHandler();
    }

    private async Task TryRegisterHandler()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("outsideClickHandler.add", ContainerId, _dotNetHelper);
            _outsideClickHandlerRegistered = true;
        }
        catch (JSException)
        {
            _outsideClickHandlerRegistered = false;
            _dotNetHelper?.Dispose();
            _dotNetHelper = null;
        }
    }

    private async Task UnregisterOutsideClickHandler()
    {
        if (!_outsideClickHandlerRegistered || _dotNetHelper is null)
        {
            return;
        }

        await SafeRemoveHandler();
    }

    private async Task SafeRemoveHandler()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("outsideClickHandler.remove", ContainerId);
        }
        catch
        {
            /* Ignored */
        }
    }

    public async ValueTask DisposeAsync()
    {
        await UnregisterOutsideClickHandler();
        _dotNetHelper?.Dispose();
        _dotNetHelper = null;
    }
}

