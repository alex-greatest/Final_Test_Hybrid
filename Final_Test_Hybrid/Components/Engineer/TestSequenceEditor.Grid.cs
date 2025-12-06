using Radzen;
using Final_Test_Hybrid.Models;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class TestSequenceEditor
{
    private void OnRowRender(RowRenderEventArgs<SequenceRow> args)
    {
        if (string.IsNullOrEmpty(args.Data.CssClass))
        {
            return;
        }
        args.Attributes.Add("class", args.Data.CssClass);
    }

    private async Task RefreshGrid()
    {
        if (_disposed)
        {
            return;
        }
        await ReloadGrid();
    }

    private async Task ReloadGrid()
    {
        if (ShouldSkipGridReload())
        {
            return;
        }
        await _grid!.Reload();
        InvokeStateHasChangedIfNotDisposed();
    }

    private bool ShouldSkipGridReload()
    {
        return _grid == null || _disposed;
    }

    private void InvokeStateHasChangedIfNotDisposed()
    {
        if (_disposed)
        {
            return;
        }
        StateHasChanged();
    }

    private async Task AnimateNewRow(SequenceRow newRow)
    {
        await RefreshGrid();
        await WaitForAnimationIfNotDisposed();
        ClearRowCssClassIfNotDisposed(newRow);
    }

    private async Task WaitForAnimationIfNotDisposed()
    {
        if (_disposed)
        {
            return;
        }
        await Task.Delay(1000);
    }

    private void ClearRowCssClassIfNotDisposed(SequenceRow row)
    {
        if (_disposed)
        {
            return;
        }
        row.CssClass = "";
        StateHasChanged();
    }
}
