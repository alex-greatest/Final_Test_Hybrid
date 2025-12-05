using Radzen;
using Radzen.Blazor;
using Final_Test_Hybrid.Models;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class TestSequenceEditor : IDisposable
{
    private RadzenDataGrid<SequenceRow>? _grid;
    private List<SequenceRow> _rows = new List<SequenceRow>();
    private int _columnCount = 4;
    private bool _disposed;

    protected override void OnInitialized()
    {
        _rows = TestSequenceService.InitializeRows(20, _columnCount);
    }

    private void OnRowRender(RowRenderEventArgs<SequenceRow> args)
    {
        if (!string.IsNullOrEmpty(args.Data.CssClass))
        {
            args.Attributes.Add("class", args.Data.CssClass);
        }
    }

    private void OnCellContextMenu(DataGridCellMouseEventArgs<SequenceRow> args)
    {
        var menuItems = new List<ContextMenuItem>
        {
            new ContextMenuItem { Text = "Insert Test Step", Value = 1 },
            new ContextMenuItem { Text = "Insert Row Before", Value = 2 },
            new ContextMenuItem { Text = "Delete Row", Value = 4 },
        };

        ContextMenuService.Open(args, menuItems, (e) => OnMenuItemClick(e, args.Data));
    }

    private void OnMenuItemClick(MenuItemEventArgs e, SequenceRow row)
    {
        ContextMenuService.Close();
        var actionValue = (int)e.Value;

        InvokeAsync(async () => await ExecuteContextAction(actionValue, row));
    }

    private async Task ExecuteContextAction(int actionValue, SequenceRow row)
    {
        if (_disposed)
        {
            return;
        }

        await RunAction(actionValue, row);
        await RefreshGrid();
    }

    private async Task RunAction(int actionValue, SequenceRow row)
    {
        Task task = actionValue switch
        {
            1 => InsertRowAfter(row),
            2 => InsertRowBefore(row),
            4 => DeleteRow(row),
            _ => Task.CompletedTask
        };
        await task;
    }

    private async Task RefreshGrid()
    {
        if (_disposed)
        {
            return;
        }

        await (_grid?.Reload() ?? Task.CompletedTask);
        StateHasChanged();
    }

    private async Task InsertRowAfter(SequenceRow currentRow)
    {
        var newRow = TestSequenceService.InsertRowAfter(_rows, currentRow, _columnCount);
        if (newRow == null)
        {
            return;
        }

        await AnimateNewRow(newRow);
    }

    private async Task InsertRowBefore(SequenceRow currentRow)
    {
        var newRow = TestSequenceService.InsertRowBefore(_rows, currentRow, _columnCount);
        if (newRow == null)
        {
            return;
        }

        await AnimateNewRow(newRow);
    }

    private async Task AnimateNewRow(SequenceRow newRow)
    {
        await RefreshGrid();
        await Task.Delay(1000);

        if (_disposed)
        {
            return;
        }

        newRow.CssClass = "";
        StateHasChanged();
    }

    private async Task DeleteRow(SequenceRow currentRow)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        await ProceedWithDelete(currentRow);
    }

    private async Task ProceedWithDelete(SequenceRow currentRow)
    {
        await PerformDeleteAnimation(currentRow);
        
        if (_disposed)
        {
            return;
        }

        TestSequenceService.RemoveRow(_rows, currentRow);
        await RefreshGrid();
    }

    private async Task PerformDeleteAnimation(SequenceRow currentRow)
    {
        Task deleteAnimationTask = TestSequenceService.PrepareForDelete(currentRow);
        await RefreshGrid();
        await deleteAnimationTask;
    }

    private void OnRowSelect(SequenceRow row)
    {
        // Handle row selection
    }

    private void OpenFolder(SequenceRow row, int colIndex)
    {
        TestSequenceService.OpenFolder(row, colIndex);
        StateHasChanged();
    }

    private void OpenSequence()
    {
         NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Info, Summary = "Open", Detail = "Open Sequence clicked" });
    }

    private void SaveSequence()
    {
         NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Info, Summary = "Save", Detail = "Sequence saved" });
    }

    private void CloseDialog()
    {
        DialogService.Close();
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

