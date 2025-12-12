using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Steps.Steps;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer.Sequence;

public partial class TestSequenceEditor
{
    private void OnStepSelected(SequenceRow row, int colIndex, object value)
    {
        var newStepId = value?.ToString() ?? "";
        if (newStepId == TestStepPlaceholder.StepId)
        {
            FillRowWithTestStep(row);
            return;
        }
        if (HasTestStepPlaceholder(row))
        {
            ClearTestStepsAndSetValue(row, colIndex, newStepId);
            return;
        }
        row.Columns[colIndex] = newStepId;
        StateHasChanged();
    }

    private void FillRowWithTestStep(SequenceRow row)
    {
        for (var i = 0; i < row.Columns.Count; i++)
        {
            row.Columns[i] = TestStepPlaceholder.StepId;
        }
        StateHasChanged();
    }

    private static bool HasTestStepPlaceholder(SequenceRow row)
    {
        return row.Columns.Any(c => c == TestStepPlaceholder.StepId);
    }

    private void ClearTestStepsAndSetValue(SequenceRow row, int colIndex, string newStepId)
    {
        for (var i = 0; i < row.Columns.Count; i++)
        {
            row.Columns[i] = i == colIndex ? newStepId : "";
        }
        StateHasChanged();
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
