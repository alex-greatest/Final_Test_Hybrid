using Final_Test_Hybrid.Models;
using Microsoft.JSInterop;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class TestSequenceEditor
{
    private async Task NewSequence()
    {
        try
        {
            await NewSequenceWithDialog();
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

    private async Task NewSequenceWithDialog()
    {
        await Task.Yield();
        if (_disposed)
        {
            return;
        }
        await TryPickAndCreateNew();
    }

    private async Task TryPickAndCreateNew()
    {
        var filePath = PickNewFile();
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }
        _isLoading = true;
        await Task.Yield();
        await CreateNewSequenceFile(filePath);
    }

    private string? PickNewFile()
    {
        var defaultPath = TestSequenceService.GetTestsSequencePath();
        return FilePickerService.SaveFile("new_sequence", defaultPath, "Excel Files (*.xlsx)|*.xlsx");
    }

    private async Task CreateNewSequenceFile(string filePath)
    {
        _rows = TestSequenceService.InitializeRows(20, _columnCount);
        TestSequenceService.CurrentFilePath = filePath;
        TestSequenceService.CurrentFileName = Path.GetFileNameWithoutExtension(filePath);
        TestSequenceService.SaveToExcel(filePath, _rows);
        _isFileActive = true;
        await RefreshGrid();
        await UpdateDialogTitleAsync();
        NotifyNewFileCreated();
    }

    private void NotifyNewFileCreated()
    {
        NotificationService.ShowSuccess(
            "Создано",
            $"Файл создан: {TestSequenceService.CurrentFileName}",
            duration: 4000,
            closeOnClick: true
        );
    }

    private void OpenFolder(SequenceRow row, int colIndex)
    {
        try
        {
            PickAndUpdateCell(row, colIndex);
        }
        catch (Exception ex)
        {
            NotifyError(ex.Message);
        }
    }

    private void PickAndUpdateCell(SequenceRow row, int colIndex)
    {
        var rootPath = TestSequenceService.GetValidRootPath();
        var relativePath = FilePickerService.PickFileRelative(rootPath);
        TestSequenceService.UpdateCell(row, colIndex, relativePath);
        StateHasChanged();
    }

    private async Task OpenSequence()
    {
        try
        {
            await OpenSequenceWithSpinner();
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

    private async Task OpenSequenceWithSpinner()
    {
        await Task.Yield();
        if (_disposed)
        {
            return;
        }
        await TryPickAndLoadSequence();
    }

    private async Task TryPickAndLoadSequence()
    {
        var filePath = PickSequenceFile();
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }
        _isLoading = true;
        await Task.Yield();
        await LoadSequenceFromFile(filePath);
    }

    private string? PickSequenceFile()
    {
        var defaultPath = TestSequenceService.GetTestsSequencePath();
        return FilePickerService.PickFile(defaultPath, "Excel Files (*.xlsx)|*.xlsx");
    }

    private async Task LoadSequenceFromFile(string filePath)
    {
        TestSequenceService.CurrentFilePath = filePath;
        TestSequenceService.CurrentFileName = Path.GetFileNameWithoutExtension(filePath);
        _isFileActive = true;
        await UpdateDialogTitleAsync();
        await TryLoadAndNotify(filePath);
    }

    private async Task TryLoadAndNotify(string filePath)
    {
        try
        {
            await TryLoadFromService(filePath);
            NotifySuccessIfNotDisposed();
        }
        catch (Exception ex)
        {
            NotifyLoadErrorIfNotDisposed(ex);
        }
    }

    private void NotifySuccessIfNotDisposed()
    {
        if (_disposed)
        {
            return;
        }
        NotifySuccess();
    }

    private void NotifyLoadErrorIfNotDisposed(Exception ex)
    {
        if (_disposed)
        {
            return;
        }
        NotifyLoadError(ex);
    }

    private void NotifySuccess()
    {
        NotificationService.ShowSuccess(
            "Открыто",
            $"Загружено: {TestSequenceService.CurrentFileName}",
            duration: 4000,
            closeOnClick: true
        );
    }

    private void NotifyError(string message)
    {
        NotificationService.ShowError(
            "Ошибка",
            message,
            duration: 10000,
            closeOnClick: true
        );
    }

    private void NotifyLoadError(Exception ex)
    {
        NotificationService.ShowError(
            "Ошибка загрузки",
            ex.Message,
            duration: 10000,
            closeOnClick: true
        );
    }

    private async Task TryLoadFromService(string filePath)
    {
        _rows = TestSequenceService.LoadFromExcel(filePath, _columnCount);
        _rows = _rows.Count == 0
            ? TestSequenceService.InitializeRows(20, _columnCount)
            : _rows;
        await RefreshGrid();
    }

    private async Task UpdateDialogTitleAsync()
    {
        const string baseName = "Редактор Тестовой Последовательности";
        var title = string.IsNullOrEmpty(TestSequenceService.CurrentFileName)
            ? baseName
            : $"{TestSequenceService.CurrentFileName}.xlsx - {baseName}";
        await JSRuntime.InvokeVoidAsync("updateDialogTitle", title);
    }

}
