namespace Final_Test_Hybrid.Components.Engineer;

public partial class TestSequenceEditor
{
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
        SaveToCurrentOrDefaultPath();
    }

    private void SaveToCurrentOrDefaultPath()
    {
        if (string.IsNullOrEmpty(TestSequenceService.CurrentFilePath))
        {
            SaveToDefaultPath();
            return;
        }
        PerformSave(TestSequenceService.CurrentFilePath);
    }

    private void SaveToDefaultPath()
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
        var fileName = EnsureXlsxExtension(TestSequenceService.CurrentFileName);
        var fullPath = Path.Combine(directory, fileName);
        TestSequenceService.CurrentFilePath = fullPath;
        return fullPath;
    }

    private static string EnsureXlsxExtension(string fileName)
    {
        return fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : fileName + ".xlsx";
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
        await Task.Yield();
        if (_disposed)
        {
            return;
        }
        await TryPickAndSaveAs();
    }

    private async Task TryPickAndSaveAs()
    {
        var filePath = PickSaveFile();
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }
        _isLoading = true;
        await Task.Yield();
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
        StateHasChanged();
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
            "Последовательность сохранена",
            duration: 4000,
            closeOnClick: true
        );
    }

    private void NotifySaveError(Exception ex)
    {
        var message = GetSaveErrorMessage(ex);
        NotificationService.ShowError(
            "Ошибка сохранения",
            message,
            duration: 10000,
            closeOnClick: true
        );
    }

    private string GetSaveErrorMessage(Exception ex)
    {
        return IsFileLockedException(ex)
            ? "Закройте файл в Excel"
            : $"Произошла ошибка при сохранении файла: {ex.Message}";
    }

    private bool IsFileLockedException(Exception? ex)
    {
        return GetExceptionChain(ex).Any(IsLockException);
    }

    private static IEnumerable<Exception> GetExceptionChain(Exception? ex)
    {
        var current = ex;
        while (current != null)
        {
            yield return current;
            current = current.InnerException;
        }
    }

    private static bool IsLockException(Exception ex)
    {
        const int sharingViolation = unchecked((int)0x80070020);
        const int lockViolation = unchecked((int)0x80070021);
        return ex is IOException ioEx && IsLockError(ioEx.HResult, sharingViolation, lockViolation);
    }

    private static bool IsLockError(int hResult, int sharingViolation, int lockViolation)
    {
        return hResult == sharingViolation || hResult == lockViolation;
    }
}
