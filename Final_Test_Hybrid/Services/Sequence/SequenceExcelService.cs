using Final_Test_Hybrid.Models;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using FileInfo = System.IO.FileInfo;

namespace Final_Test_Hybrid.Services.Sequence;

public class SequenceExcelService(ILogger<SequenceExcelService> logger) : ISequenceExcelService
{
    private const int MaxColumns = 4;

    public void SaveSequence(string path, List<SequenceRow> rows)
    {
        try
        {
            EnsureDirectoryExists(path);
            SaveWorkbook(path, rows);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка сохранения последовательности в файл: {Path}", path);
            throw;
        }
    }

    public List<SequenceRow> LoadSequence(string path, int columnCount)
    {
        try
        {
            return LoadWorkbook(path, columnCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка загрузки последовательности из файла: {Path}", path);
            throw;
        }
    }

    private void SaveWorkbook(string path, List<SequenceRow> rows)
    {
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Sequence");
        CreateHeader(worksheet);
        PopulateData(worksheet, rows);
        FormatWorksheet(worksheet);
        package.SaveAs(new FileInfo(path));
    }

    private List<SequenceRow> LoadWorkbook(string path, int columnCount)
    {
        using var package = new ExcelPackage(new FileInfo(path));
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        return worksheet == null ? [] : ParseWorksheet(worksheet, columnCount);
    }

    private void FormatWorksheet(ExcelWorksheet worksheet)
    {
        worksheet.Cells.Style.Font.Size = 14;
        worksheet.Cells.AutoFitColumns();
    }

    private void EnsureDirectoryExists(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }
        CreateDirectoryIfNotExists(directory);
    }

    private void CreateDirectoryIfNotExists(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void CreateHeader(ExcelWorksheet worksheet)
    {
        Enumerable.Range(0, MaxColumns)
            .ToList()
            .ForEach(i => worksheet.Cells[1, i + 1].Value = $"Column {i + 1}");
    }

    private void PopulateData(ExcelWorksheet worksheet, List<SequenceRow> rows)
    {
        rows.Select((row, index) => new { row, index })
            .ToList()
            .ForEach(item => PopulateRow(worksheet, item.row, item.index));
    }

    private void PopulateRow(ExcelWorksheet worksheet, SequenceRow row, int rowIndex)
    {
        row.Columns
            .Take(MaxColumns)
            .Select((value, colIndex) => new { value, colIndex })
            .ToList()
            .ForEach(item => worksheet.Cells[rowIndex + 2, item.colIndex + 1].Value = item.value);
    }

    private List<SequenceRow> ParseWorksheet(ExcelWorksheet worksheet, int columnCount)
    {
        var rowCount = worksheet.Dimension?.Rows ?? 0;
        return Enumerable.Range(2, Math.Max(0, rowCount - 1))
            .Select(rowIndex => ParseRow(worksheet, rowIndex, columnCount))
            .ToList();
    }

    private SequenceRow ParseRow(ExcelWorksheet worksheet, int rowIndex, int columnCount)
    {
        var newRow = new SequenceRow(columnCount);
        Enumerable.Range(1, columnCount)
            .ToList()
            .ForEach(c => newRow.Columns[c - 1] = worksheet.Cells[rowIndex, c].Value?.ToString() ?? "");
        return newRow;
    }
}
