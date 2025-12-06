namespace Final_Test_Hybrid.Components.Engineer.Sequence;

public partial class TestSequenceEditor
{
    private async Task SaveSequence()
    {
        try
        {
            await SaveCurrentSequenceAsync();
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task SaveCurrentSequenceAsync()
    {
        _isLoading = true;
        await Task.Yield();
        if (_disposed)
        {
            return;
        }
        var filePath = GetOrGenerateFilePath();
        await PerformSaveAsync(filePath);
    }

    private string GetOrGenerateFilePath()
    {
        return string.IsNullOrEmpty(TestSequenceService.CurrentFilePath)
            ? GenerateDefaultFilePath()
            : TestSequenceService.CurrentFilePath;
    }

    private string GenerateDefaultFilePath()
    {
        var directory = TestSequenceService.GetTestsSequencePath();
        var fileName = EnsureXlsxExtension(TestSequenceService.CurrentFileName);
        var fullPath = Path.Combine(directory, fileName);
        TestSequenceService.CurrentFilePath = fullPath;
        return fullPath;
    }

    private static string EnsureXlsxExtension(string? fileName)
    {
        var hasExtension = fileName != null && fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);
        return hasExtension ? fileName! : fileName + ".xlsx";
    }

    private async Task SaveSequenceAs()
    {
        try
        {
            await SaveSequenceAsAsync();
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

    private async Task SaveSequenceAsAsync()
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
        await UpdateDialogTitleAsync();
        await PerformSaveAsync(filePath);
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

    private async Task PerformSaveAsync(string filePath)
    {
        try
        {
            await TestSequenceService.SaveToExcelAsync(filePath, _rows);
            NotifySuccess("Сохранено", "Последовательность сохранена");
        }
        catch (Exception ex)
        {
            NotifySaveError(ex);
        }
    }

    private void NotifySaveError(Exception ex)
    {
        var message = GetSaveErrorMessage(ex);
        NotificationService.ShowError("Ошибка сохранения", message, duration: 10000, closeOnClick: true);
    }

    private string GetSaveErrorMessage(Exception ex)
    {
        return IsFileLockedException(ex)
            ? "Закройте файл в Excel"
            : $"Произошла ошибка при сохранении файла: {ex.Message}";
    }

    private static bool IsFileLockedException(Exception? ex)
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
        return ex is IOException ioEx && (ioEx.HResult == sharingViolation || ioEx.HResult == lockViolation);
    }
}
