using Final_Test_Hybrid.Models;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class TestSequenceEditor
{
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
        _isLoading = true;
        await Task.Yield(); 

        if (_disposed) return;

        var filePath = PickSequenceFile();
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

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
        
        try
        {
             await TryLoadFromService(filePath);
             if (!_disposed) NotifySuccess(filePath);
        }
        catch (Exception ex)
        {
             if (!_disposed) NotifyLoadError(ex);
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

    private async Task SaveSequence()
    {
        try
        {
            await SaveSequenceWithSpinner();
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task SaveSequenceWithSpinner()
    {
        _isLoading = true;
        await Task.Yield();
        if (_disposed)
        {
            return;
        }
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
            var filePath = GenerateDefaultFilePath();
            PerformSave(filePath);
        }
        catch (Exception ex)
        {
             NotifyError(ex.Message);
        }
    }

    private string GenerateDefaultFilePath()
    {
        var directory = TestSequenceService.GetTestsSequencePath();
        var fileName = TestSequenceService.CurrentFileName;
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".xlsx";
        }

        var fullPath = Path.Combine(directory, fileName);
        TestSequenceService.CurrentFilePath = fullPath;
        return fullPath;
    }

    private async Task SaveSequenceAs()
    {
        try
        {
            await SaveSequenceAsWithSpinner();
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

    private async Task SaveSequenceAsWithSpinner()
    {
        _isLoading = true;
        await Task.Yield();

        if (_disposed) return;

        var filePath = PickSaveFile();
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        UpdateCurrentFile(filePath);
        PerformSave(filePath);
    }

    private string? PickSaveFile()
    {
        var defaultPath = TestSequenceService.GetTestsSequencePath();
        return FilePickerService.SaveFile("sequence", defaultPath, "Excel Files (*.xlsx)|*.xlsx");
    }

    private void UpdateCurrentFile(string filePath)
    {
        TestSequenceService.CurrentFilePath = filePath;
        TestSequenceService.CurrentFileName = Path.GetFileNameWithoutExtension(filePath);
        StateHasChanged(); // Update header
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
        var message = GetSaveErrorMessage(ex);
        NotificationService.ShowError(
            "Ошибка сохранения",
            message,
            duration: 10000,
            closeOnClick: true,
            style: "width: 400px; white-space: pre-wrap;"
        );
    }

    private string GetSaveErrorMessage(Exception ex)
    {
        return ex.Message.Contains("being used by another process") ? "Файл открыт в Excel! Пожалуйста, закройте его." : "Произошла ошибка при сохранении файла.";
    }

    private void CloseDialog()
    {
        DialogService.Close();
    }
}
