using Final_Test_Hybrid.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Sequence;

public class TestSequenceService(
    ISequenceExcelService sequenceExcelService,
    IConfiguration configuration,
    ILogger<TestSequenceService> logger)
{
    public string? CurrentFileName { get; set; }
    public string? CurrentFilePath { get; set; }

    public List<SequenceRow> InitializeRows(int count, int columnCount)
    {
        return Enumerable.Range(0, count)
            .Select(_ => new SequenceRow(columnCount))
            .ToList();
    }

    public SequenceRow? InsertRowAfter(List<SequenceRow> rows, SequenceRow currentRow, int columnCount)
    {
        var index = rows.IndexOf(currentRow);
        return index < 0 ? null : InsertRowAtIndex(rows, index + 1, columnCount);
    }

    public SequenceRow? InsertRowBefore(List<SequenceRow> rows, SequenceRow currentRow, int columnCount)
    {
        var index = rows.IndexOf(currentRow);
        return index < 0 ? null : InsertRowAtIndex(rows, index, columnCount);
    }

    private SequenceRow InsertRowAtIndex(List<SequenceRow> rows, int index, int columnCount)
    {
        var newRow = new SequenceRow(columnCount) { CssClass = "row-animate-new" };
        rows.Insert(index, newRow);
        return newRow;
    }

    public void PrepareForDelete(SequenceRow row)
    {
        row.CssClass = "row-animate-delete";
    }

    public void RemoveRow(List<SequenceRow> rows, SequenceRow row)
    {
        rows.Remove(row);
    }

    public void SaveToExcel(string path, List<SequenceRow> rows)
    {
        sequenceExcelService.SaveSequence(path, rows);
    }

    public List<SequenceRow> LoadFromExcel(string path, int columnCount)
    {
        return sequenceExcelService.LoadSequence(path, columnCount);
    }

    public string GetTestsSequencePath()
    {
        var path = GetRequiredConfigPath("Paths:PathToTestsSequence");
        return GetOrCreateDirectory(path);
    }

    private string GetOrCreateDirectory(string path)
    {
        return Directory.Exists(path) ? path : CreateDirectory(path);
    }

    private string CreateDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return path;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка пути: Не удалось создать директорию: {Path}", path);
            throw new IOException($"Ошибка пути: Не удалось создать директорию: {path}. {ex.Message}", ex);
        }
    }

    public string GetValidRootPath()
    {
        var path = GetRequiredConfigPath("Paths:PathToTestSteps");
        ValidatePathExists(path);
        return path;
    }

    private string GetRequiredConfigPath(string key)
    {
        var path = configuration[key];
        if (!string.IsNullOrEmpty(path))
        {
            return path;
        }
        logger.LogError("Ошибка конфигурации: {Key} отсутствует в appsettings.json", key);
        throw new InvalidOperationException($"Ошибка конфигурации: {key} отсутствует в appsettings.json");
    }

    private void ValidatePathExists(string path)
    {
        if (Directory.Exists(path))
        {
            return;
        }
        logger.LogError("Ошибка пути: Путь не найден: {Path}", path);
        throw new DirectoryNotFoundException($"Ошибка пути: Путь не найден: {path}");
    }

    public void UpdateCell(SequenceRow row, int colIndex, string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return;
        }
        row.Columns[colIndex] = relativePath;
    }
}
