using Radzen;
using Radzen.Blazor;
using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Enum;
using Microsoft.AspNetCore.Components;
using Final_Test_Hybrid.Services.IO;
using Final_Test_Hybrid.Services.UI;
using Final_Test_Hybrid.Services.Sequence;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class TestSequenceEditor : IDisposable
{
    [Inject]
    public required IFilePickerService FilePickerService { get; set; }
    private RadzenDataGrid<SequenceRow>? _grid;
    private List<SequenceRow> _rows = [];
    private readonly int _columnCount = 4;
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
            new() { Text = "Вставить шаг теста", Value = (int)SequenceContextAction.InsertStep },
            new() { Text = "Вставить строку перед", Value = (int)SequenceContextAction.InsertRowBefore },
            new() { Text = "Удалить строку", Value = (int)SequenceContextAction.DeleteRow },
        };

        ContextMenuService.Open(args, menuItems, (e) => OnMenuItemClick(e, args.Data));
    }

    private void OnMenuItemClick(MenuItemEventArgs e, SequenceRow row)
    {
        ContextMenuService.Close();
        if (e.Value is not int actionValue) return;

        InvokeAsync(async () => await ExecuteContextAction((SequenceContextAction)actionValue, row));
    }

    private async Task ExecuteContextAction(SequenceContextAction action, SequenceRow row)
    {
        if (_disposed)
        {
            return;
        }

        await RunAction(action, row);
        await RefreshGrid();
    }

    private async Task RunAction(SequenceContextAction action, SequenceRow row)
    {
        var task = action switch
        {
            SequenceContextAction.InsertStep => InsertRowAfter(row),
            SequenceContextAction.InsertRowBefore => InsertRowBefore(row),
            SequenceContextAction.DeleteRow => DeleteRow(row),
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
        TestSequenceService.PrepareForDelete(currentRow);
        await RefreshGrid();
        await Task.Delay(500);
    }

    private void OnRowSelect(SequenceRow row)
    {
        // Handle row selection
    }

    private void OpenFolder(SequenceRow row, int colIndex)
    {
        try
        {
            var rootPath = TestSequenceService.GetValidRootPath();
            var relativePath = FilePickerService.PickFileRelative(rootPath);
            TestSequenceService.UpdateCell(row, colIndex, relativePath);
            StateHasChanged();
        }
        catch (Exception ex)
        {
             NotifyError(ex.Message);
        }
    }

    private async Task OpenSequence()
    {
        try
        {
            var defaultPath = TestSequenceService.GetTestsSequencePath();
            var filePath = FilePickerService.PickFile(defaultPath, "Excel Files (*.xlsx)|*.xlsx");
            
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            await LoadSequenceFromFile(filePath);
        }
        catch (Exception ex)
        {
            NotifyError(ex.Message);
        }
    }

    private async Task LoadSequenceFromFile(string filePath)
    {
        TestSequenceService.CurrentFilePath = filePath;
        TestSequenceService.CurrentFileName = Path.GetFileNameWithoutExtension(filePath);
        
        try
        {
             await TryLoadFromService(filePath);
             NotifySuccess(filePath);
        }
        catch (Exception ex)
        {
             NotifyLoadError(ex);
        }
    }

    private void NotifySuccess(string filePath)
    {
        NotificationService.ShowSuccess(
            "Открыто", 
            $"Последовательность загружена из {TestSequenceService.CurrentFileName}", 
            duration: 4000,
            closeOnClick: true,
            style: "width: 400px; white-space: pre-wrap;"
        );
    }

    private void NotifyError(string message)
    {
        NotificationService.ShowError(
            "Ошибка", 
            message, 
            duration: 10000,
            closeOnClick: true,
            style: "width: 400px; white-space: pre-wrap;"
        );
    }

    private void NotifyLoadError(Exception ex)
    {
        NotificationService.ShowError(
            "Ошибка", 
            $"Не удалось загрузить: {ex.Message}", 
            duration: 10000,
            closeOnClick: true,
            style: "width: 400px; white-space: pre-wrap;"
        );
    }

    private async Task TryLoadFromService(string filePath)
    {
        _rows = TestSequenceService.LoadFromExcel(filePath, _columnCount);
        if (_rows.Count == 0)
        {
            _rows = TestSequenceService.InitializeRows(20, _columnCount);
        }
        await RefreshGrid();
    }

    private void SaveSequence()
    {
        if (string.IsNullOrEmpty(TestSequenceService.CurrentFilePath))
        {
            SetDefaultFilePath();
            return;
        }

        PerformSave(TestSequenceService.CurrentFilePath);
    }

    private void SetDefaultFilePath()
    {
        try 
        {
            var directory = TestSequenceService.GetTestsSequencePath();
            var fileName = TestSequenceService.CurrentFileName;
            if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".xlsx";
            }

            TestSequenceService.CurrentFilePath = Path.Combine(directory, fileName);
            PerformSave(TestSequenceService.CurrentFilePath);
        }
        catch (Exception ex)
        {
             NotifyError(ex.Message);
        }
    }

    private void SaveSequenceAs()
    {
        try
        {
            var defaultPath = TestSequenceService.GetTestsSequencePath();
            var filePath = FilePickerService.SaveFile("sequence", defaultPath, "Excel Files (*.xlsx)|*.xlsx");
            
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            TestSequenceService.CurrentFilePath = filePath;
            TestSequenceService.CurrentFileName = Path.GetFileNameWithoutExtension(filePath);
            StateHasChanged(); // Update header
            PerformSave(filePath);
        }
        catch (Exception ex)
        {
             NotifyError(ex.Message);
        }
    }

    private void PerformSave(string filePath)
    {
        try
        {
            TestSequenceService.SaveToExcel(filePath, _rows);
            NotifySaveSuccess();
        }
        catch (Exception ex)
        {
            NotifySaveError(ex);
        }
    }

    private void NotifySaveSuccess()
    {
        NotificationService.ShowSuccess(
            "Сохранено", 
            "Последовательность успешно сохранена", 
            duration: 4000,
            closeOnClick: true,
            style: "width: 400px; white-space: pre-wrap;"
        );
    }

    private void NotifySaveError(Exception ex)
    {
        var message = "Произошла ошибка при сохранении файла.";
        
        if (ex.Message.Contains("being used by another process"))
        {
            message = "Файл открыт в Excel! Пожалуйста, закройте его.";
        }

        NotificationService.ShowError(
            "Ошибка сохранения",
            message,
            duration: 10000,
            closeOnClick: true,
            style: "width: 400px; white-space: pre-wrap;"
        );
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
