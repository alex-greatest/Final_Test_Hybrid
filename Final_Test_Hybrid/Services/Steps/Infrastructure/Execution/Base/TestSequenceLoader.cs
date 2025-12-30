using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using FileInfo = System.IO.FileInfo;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Base;

public class TestSequenceLoader(
    IConfiguration configuration,
    ITestStepLogger testLogger,
    ILogger<TestSequenceLoader> logger) : ITestSequenceLoader
{
    private const int ColumnCount = 4;
    private string SequencesPath => configuration["Paths:PathToTestsSequence"] ?? "";

    public async Task<SequenceLoadResult> LoadRawDataAsync(string articleNumber)
    {
        logger.LogInformation("Загрузка тестов для артикула {Article}", articleNumber);
        testLogger.LogInformation("Загрузка файла тестов для артикула {Article}...", articleNumber);
        var filePath = BuildFilePath(articleNumber);
        if (!File.Exists(filePath))
        {
            return HandleFileNotFound(articleNumber, filePath);
        }
        return await Task.Run(() => ReadExcelSafe(filePath));
    }

    private SequenceLoadResult HandleFileNotFound(string articleNumber, string filePath)
    {
        logger.LogWarning("Файл не найден: {FilePath}", filePath);
        testLogger.LogError(null, "Файл тестов не найден: {Article}.xlsx", articleNumber);
        return SequenceLoadResult.WithError($"Файл последовательности тестов не найден: {articleNumber}.xlsx");
    }

    private string BuildFilePath(string articleNumber) =>
        Path.Combine(SequencesPath, $"{articleNumber}.xlsx");

    private SequenceLoadResult ReadExcelSafe(string filePath)
    {
        try
        {
            var rows = ReadExcelFile(filePath);
            if (rows.Count == 0)
            {
                logger.LogWarning("Файл пустой: {FilePath}", filePath);
                testLogger.LogWarning("Файл тестов пустой");
                return SequenceLoadResult.WithError("Файл последовательности тестов пустой");
            }
            logger.LogInformation("Загружено {Count} строк из {FilePath}", rows.Count, filePath);
            testLogger.LogInformation("Файл загружен: {Count} строк", rows.Count);
            return SequenceLoadResult.Success(rows);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка чтения Excel файла: {FilePath}", filePath);
            testLogger.LogError(ex, "Ошибка чтения файла: {Message}", ex.Message);
            return SequenceLoadResult.WithError($"Ошибка чтения файла: {ex.Message}");
        }
    }

    private List<string?[]> ReadExcelFile(string filePath)
    {
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        return worksheet == null ? [] : ParseWorksheet(worksheet);
    }

    private List<string?[]> ParseWorksheet(ExcelWorksheet worksheet)
    {
        var rowCount = worksheet.Dimension?.Rows ?? 0;
        return Enumerable
            .Range(1, rowCount)
            .Select(row => ReadRow(worksheet, row))
            .ToList();
    }

    private string?[] ReadRow(ExcelWorksheet worksheet, int rowIndex) =>
        Enumerable
            .Range(1, ColumnCount)
            .Select(col => worksheet.Cells[rowIndex, col].Value?.ToString())
            .ToArray();
}
