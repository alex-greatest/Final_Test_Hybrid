using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Steps.Steps;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer.Sequence;

public partial class TestSequenceEditor
{
    private async Task OnStepSelected(SequenceRow row, int colIndex, object value)
    {
        var newStepId = value?.ToString() ?? "";
        if (newStepId == TestStepPlaceholder.StepId)
        {
            await FillOtherCellsWithTestStep(row, colIndex);
            return;
        }
        if (HasTestStepPlaceholder(row))
        {
            await ClearOtherTestSteps(row, colIndex);
        }
    }

    private async Task FillOtherCellsWithTestStep(SequenceRow row, int currentIndex)
    {
        var hasOtherCells = false;
        for (var i = 0; i < row.Columns.Count; i++)
        {
            if (i == currentIndex)
            {
                continue;
            }
            row.Columns[i] = TestStepPlaceholder.StepId;
            hasOtherCells = true;
        }
        if (hasOtherCells)
        {
            await RefreshGrid();
        }
    }

    private static bool HasTestStepPlaceholder(SequenceRow row)
    {
        return row.Columns.Any(c => c == TestStepPlaceholder.StepId);
    }

    private async Task ClearOtherTestSteps(SequenceRow row, int currentIndex)
    {
        var hasChanges = false;
        for (var i = 0; i < row.Columns.Count; i++)
        {
            if (i == currentIndex)
            {
                continue;
            }
            if (row.Columns[i] == TestStepPlaceholder.StepId)
            {
                row.Columns[i] = "";
                hasChanges = true;
            }
        }
        if (hasChanges)
        {
            await RefreshGrid();
        }
    }

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
