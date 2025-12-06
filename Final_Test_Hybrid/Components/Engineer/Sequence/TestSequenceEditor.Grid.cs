using Final_Test_Hybrid.Models;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer.Sequence;

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
        if (_disposed || _grid == null)
        {
            return;
        }
        await _grid.Reload();
        StateHasChanged();
    }

    private async Task AnimateNewRow(SequenceRow newRow)
    {
        await RefreshGrid();
        await DelayAsync(1000);
        ClearRowCssClass(newRow);
    }

    private async Task DelayAsync(int milliseconds)
    {
        try
        {
            await Task.Delay(milliseconds, _cts.Token);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void ClearRowCssClass(SequenceRow row)
    {
        if (_disposed)
        {
            return;
        }
        row.CssClass = "";
        StateHasChanged();
    }
}
