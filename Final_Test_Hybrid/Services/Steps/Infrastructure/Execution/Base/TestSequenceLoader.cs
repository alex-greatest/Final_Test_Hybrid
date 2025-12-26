using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using FileInfo = System.IO.FileInfo;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Base;

public class TestSequenceLoader(
    IConfiguration configuration,
    SequenceValidationState validationState,
    ILogger<TestSequenceLoader> logger) : ITestSequenceLoader
{
    private const int ColumnCount = 4;
    private const string PlaceholderName = "<TEST STEP>";

    private string SequencesPath => configuration["Paths:PathToTestsSequence"] ?? "";

    public async Task<List<string?[]>?> LoadRawDataAsync(string articleNumber)
    {
        logger.LogInformation("Загрузка последовательности для артикула {Article}", articleNumber);
        validationState.ClearError();
        var filePath = BuildFilePath(articleNumber);
        if (!ValidateFileExists(filePath, articleNumber))
        {
            return null;
        }
        return await Task.Run(() => ReadExcelSafe(filePath));
    }

    private string BuildFilePath(string articleNumber)
    {
        return Path.Combine(SequencesPath, $"{articleNumber}.xlsx");
    }

    private bool ValidateFileExists(string filePath, string articleNumber)
    {
        if (File.Exists(filePath))
        {
            return true;
        }
        var error = $"Файл последовательности не найден: {articleNumber}.xlsx";
        logger.LogWarning("Файл не найден: {FilePath}", filePath);
        validationState.SetError(error);
        return false;
    }

    private List<string?[]>? ReadExcelSafe(string filePath)
    {
        try
        {
            return ReadAndValidateExcel(filePath);
        }
        catch (Exception ex)
        {
            HandleReadError(ex, filePath);
            return null;
        }
    }

    private void HandleReadError(Exception ex, string filePath)
    {
        logger.LogError(ex, "Ошибка чтения Excel файла: {FilePath}", filePath);
        validationState.SetError($"Ошибка чтения файла: {ex.Message}");
    }

    private List<string?[]>? ReadAndValidateExcel(string filePath)
    {
        var rows = ReadExcelFile(filePath);
        if (!ValidateNotEmpty(rows, filePath))
        {
            return null;
        }
        if (!ValidateAllPlaceholderRows(rows))
        {
            return null;
        }
        logger.LogInformation("Загружено {Count} строк из {FilePath}", rows.Count, filePath);
        return rows;
    }

    private List<string?[]> ReadExcelFile(string filePath)
    {
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
        {
            return [];
        }
        return ParseWorksheet(worksheet);
    }

    private List<string?[]> ParseWorksheet(ExcelWorksheet worksheet)
    {
        var rowCount = worksheet.Dimension?.Rows ?? 0;
        return Enumerable
            .Range(1, rowCount)
            .Select(row => ReadRow(worksheet, row))
            .ToList();
    }

    private string?[] ReadRow(ExcelWorksheet worksheet, int rowIndex)
    {
        return Enumerable
            .Range(1, ColumnCount)
            .Select(col => worksheet.Cells[rowIndex, col].Value?.ToString())
            .ToArray();
    }

    private bool ValidateNotEmpty(List<string?[]> rows, string filePath)
    {
        if (rows.Count > 0)
        {
            return true;
        }
        var error = "Файл последовательности пустой";
        logger.LogWarning("Файл пустой: {FilePath}", filePath);
        validationState.SetError(error);
        return false;
    }

    private bool ValidateAllPlaceholderRows(List<string?[]> rows)
    {
        var invalidRow = FindFirstInvalidPlaceholderRow(rows);
        if (invalidRow == null)
        {
            return true;
        }
        ReportPlaceholderError(invalidRow.Value);
        return false;
    }

    private int? FindFirstInvalidPlaceholderRow(List<string?[]> rows)
    {
        return rows
            .Select((cells, index) => new { Cells = cells, RowNumber = index + 1 })
            .Where(row => !IsValidPlaceholderRow(row.Cells))
            .Select(row => (int?)row.RowNumber)
            .FirstOrDefault();
    }

    private bool IsValidPlaceholderRow(string?[] cells)
    {
        var placeholderCount = cells.Count(IsPlaceholder);
        return placeholderCount is 0 or ColumnCount;
    }

    private void ReportPlaceholderError(int rowNumber)
    {
        var error = $"Строка {rowNumber}: <TEST STEP> должен быть во всех 4 колонках";
        logger.LogError("Нарушена структура <TEST STEP> в строке {Row}", rowNumber);
        validationState.SetError(error);
    }

    private static bool IsPlaceholder(string? value)
    {
        return string.Equals(value?.Trim(), PlaceholderName, StringComparison.OrdinalIgnoreCase);
    }
}
