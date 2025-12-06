using Final_Test_Hybrid.Models;
using Microsoft.JSInterop;

namespace Final_Test_Hybrid.Components.Engineer.Sequence;

public partial class TestSequenceEditor
{
    private async Task NewSequence()
    {
        try
        {
            await CreateNewSequenceAsync();
        }
        catch (Exception ex)
        {
            NotifyError(ex.Message);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task CreateNewSequenceAsync()
    {
        await Task.Yield();
        if (_disposed)
        {
            return;
        }
        await TryPickAndCreateFile();
    }

    private async Task TryPickAndCreateFile()
    {
        var filePath = PickNewFile();
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }
        _isLoading = true;
        await Task.Yield();
        await CreateSequenceFile(filePath);
    }

    private string? PickNewFile()
    {
        var defaultPath = TestSequenceService.GetTestsSequencePath();
        return FilePickerService.SaveFile("new_sequence", defaultPath, "Excel Files (*.xlsx)|*.xlsx");
    }

    private async Task CreateSequenceFile(string filePath)
    {
        _rows = TestSequenceService.InitializeRows(20, _columnCount);
        TestSequenceService.CurrentFilePath = filePath;
        TestSequenceService.CurrentFileName = Path.GetFileNameWithoutExtension(filePath);
        await TestSequenceService.SaveToExcelAsync(filePath, _rows);
        _isFileActive = true;
        await RefreshGrid();
        await UpdateDialogTitleAsync();
        NotifySuccess("Создано", $"Файл создан: {TestSequenceService.CurrentFileName}");
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
            await LoadSequenceAsync();
        }
        catch (Exception ex)
        {
            NotifyError(ex.Message);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadSequenceAsync()
    {
        await Task.Yield();
        if (_disposed)
        {
            return;
        }
        await TryPickAndLoadFile();
    }

    private async Task TryPickAndLoadFile()
    {
        var filePath = PickSequenceFile();
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }
        _isLoading = true;
        await Task.Yield();
        await LoadFromFile(filePath);
    }

    private string? PickSequenceFile()
    {
        var defaultPath = TestSequenceService.GetTestsSequencePath();
        return FilePickerService.PickFile(defaultPath, "Excel Files (*.xlsx)|*.xlsx");
    }

    private async Task LoadFromFile(string filePath)
    {
        TestSequenceService.CurrentFilePath = filePath;
        TestSequenceService.CurrentFileName = Path.GetFileNameWithoutExtension(filePath);
        _isFileActive = true;
        await UpdateDialogTitleAsync();
        await LoadRowsFromExcel(filePath);
    }

    private async Task LoadRowsFromExcel(string filePath)
    {
        try
        {
            _rows = await TestSequenceService.LoadFromExcelAsync(filePath, _columnCount);
            _rows = _rows.Count == 0 ? TestSequenceService.InitializeRows(20, _columnCount) : _rows;
            await RefreshGrid();
            NotifySuccessIfNotDisposed();
        }
        catch (Exception ex)
        {
            NotifyErrorIfNotDisposed(ex.Message);
        }
    }

    private void NotifySuccessIfNotDisposed()
    {
        if (_disposed)
        {
            return;
        }
        NotifySuccess("Открыто", $"Загружено: {TestSequenceService.CurrentFileName}");
    }

    private void NotifyErrorIfNotDisposed(string message)
    {
        if (_disposed)
        {
            return;
        }
        NotifyError(message);
    }

    private void NotifySuccess(string title, string message)
    {
        NotificationService.ShowSuccess(title, message, duration: 4000, closeOnClick: true);
    }

    private void NotifyError(string message)
    {
        NotificationService.ShowError("Ошибка", message, duration: 10000, closeOnClick: true);
    }

    private async Task UpdateDialogTitleAsync()
    {
        var title = BuildDialogTitle();
        await JsRuntime.InvokeVoidAsync("updateDialogTitle", title);
    }

    private string BuildDialogTitle()
    {
        const string baseName = "Редактор Тестовой Последовательности";
        return string.IsNullOrEmpty(TestSequenceService.CurrentFileName)
            ? baseName
            : $"{TestSequenceService.CurrentFileName}.xlsx - {baseName}";
    }
}
