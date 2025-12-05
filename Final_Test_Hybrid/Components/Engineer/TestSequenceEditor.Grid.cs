using Radzen;
using Final_Test_Hybrid.Models;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class TestSequenceEditor
{
    private void OnRowRender(RowRenderEventArgs<SequenceRow> args)
    {
        if (!string.IsNullOrEmpty(args.Data.CssClass))
        {
            args.Attributes.Add("class", args.Data.CssClass);
        }
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
        if (_grid == null || _disposed)
        {
             return;
        }
        await _grid.Reload();
        if (!_disposed)
        {
            StateHasChanged();
        }
    }

    private async Task AnimateNewRow(SequenceRow newRow)
    {
        await RefreshGrid();
        if (_disposed)
        {
            return;
        }
        await Task.Delay(1000);
        if (_disposed)
        {
            return;
        }
        newRow.CssClass = "";
        StateHasChanged();
    }

    private void OnRowSelect(SequenceRow row)
    {
        // Handle row selection
    }
}
