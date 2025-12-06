using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Enum;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer.Sequence;

public partial class TestSequenceEditor
{
    private void OnCellContextMenu(DataGridCellMouseEventArgs<SequenceRow> args)
    {
        var menuItems = GetContextMenuItems();
        ContextMenuService.Open(args, menuItems, e => OnMenuItemClick(e, args.Data));
    }

    private static List<ContextMenuItem> GetContextMenuItems()
    {
        return
        [
            new ContextMenuItem { Text = "Вставить шаг теста", Value = (int)SequenceContextAction.InsertStep },
            new ContextMenuItem { Text = "Вставить строку перед", Value = (int)SequenceContextAction.InsertRowBefore },
            new ContextMenuItem { Text = "Удалить строку", Value = (int)SequenceContextAction.DeleteRow }
        ];
    }

    private void OnMenuItemClick(MenuItemEventArgs e, SequenceRow row)
    {
        ContextMenuService.Close();
        if (e.Value is not int actionValue)
        {
            return;
        }
        _ = InvokeAsync(() => ExecuteContextAction((SequenceContextAction)actionValue, row));
    }

    private async Task ExecuteContextAction(SequenceContextAction action, SequenceRow row)
    {
        if (_disposed)
        {
            return;
        }
        await ExecuteAction(action, row);
    }

    private Task ExecuteAction(SequenceContextAction action, SequenceRow row)
    {
        return action switch
        {
            SequenceContextAction.InsertStep => InsertRowAfter(row),
            SequenceContextAction.InsertRowBefore => InsertRowBefore(row),
            SequenceContextAction.DeleteRow => DeleteRow(row),
            _ => Task.CompletedTask
        };
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

    private async Task DeleteRow(SequenceRow currentRow)
    {
        if (_rows.Count == 0)
        {
            return;
        }
        TestSequenceService.PrepareForDelete(currentRow);
        await RefreshGrid();
        await DelayAsync(500);
        await RemoveAndRefresh(currentRow);
    }

    private async Task RemoveAndRefresh(SequenceRow currentRow)
    {
        if (_disposed)
        {
            return;
        }
        TestSequenceService.RemoveRow(_rows, currentRow);
        await RefreshGrid();
    }
}
