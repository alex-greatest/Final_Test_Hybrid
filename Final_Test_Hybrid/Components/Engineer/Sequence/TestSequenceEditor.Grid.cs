using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Steps.Steps;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer.Sequence;

public partial class TestSequenceEditor
{
    private static string GetStepDisplayText(object? stepItem)
    {
        return stepItem?.ToString() ?? string.Empty;
    }

    private static string GetStepTooltip(object? stepItem)
    {
        var text = GetStepDisplayText(stepItem);
        return string.IsNullOrEmpty(text) ? string.Empty : text;
    }

    private Task OnStepSelected(SequenceRow row, int colIndex, object? value)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        var selectedStepId = value?.ToString() ?? "";
        var hasChanges = ApplyStepSelectionRules(row, colIndex, selectedStepId);
        if (!hasChanges)
        {
            return Task.CompletedTask;
        }

        return RefreshValuesAsync();
    }

    private static bool ApplyStepSelectionRules(SequenceRow row, int currentIndex, string selectedStepId)
    {
        var shouldFillWithPlaceholder = selectedStepId == TestStepPlaceholder.StepId;
        var hasChanges = false;

        for (var i = 0; i < row.Columns.Count; i++)
        {
            if (i == currentIndex)
            {
                continue;
            }

            var value = row.Columns[i];
            switch (shouldFillWithPlaceholder)
            {
                case true when value != TestStepPlaceholder.StepId:
                    row.Columns[i] = TestStepPlaceholder.StepId;
                    hasChanges = true;
                    continue;
                case false when value == TestStepPlaceholder.StepId:
                    row.Columns[i] = "";
                    hasChanges = true;
                    break;
            }
        }

        return hasChanges;
    }

    private void OnRowRender(RowRenderEventArgs<SequenceRow> args)
    {
        if (string.IsNullOrEmpty(args.Data.CssClass))
        {
            return;
        }
        args.Attributes.Add("class", args.Data.CssClass);
    }

    private async Task RefreshValuesAsync()
    {
        if (_disposed)
        {
            return;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task RefreshStructureAsync()
    {
        if (_disposed || _grid == null)
        {
            return;
        }

        await _grid.Reload();
    }

    private async Task AnimateNewRow(SequenceRow newRow)
    {
        await RefreshStructureAsync();
        await DelayAsync(1000);
        await ClearRowCssClassAsync(newRow);
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

    private async Task ClearRowCssClassAsync(SequenceRow row)
    {
        if (_disposed)
        {
            return;
        }

        row.CssClass = "";
        await RefreshValuesAsync();
    }
}
