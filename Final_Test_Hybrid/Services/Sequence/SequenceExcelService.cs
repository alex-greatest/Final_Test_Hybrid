using Final_Test_Hybrid.Models;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using FileInfo = System.IO.FileInfo;

namespace Final_Test_Hybrid.Services.Sequence;

public class SequenceExcelService(ILogger<SequenceExcelService> logger) : ISequenceExcelService
{
    public void SaveSequence(string path, List<SequenceRow> rows)
    {
        try
        {
            EnsureDirectoryExists(path);

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Sequence");

            CreateHeader(worksheet);
            PopulateData(worksheet, rows);
            FormatWorksheet(worksheet);

            package.SaveAs(new FileInfo(path));
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
            using var package = new ExcelPackage(new FileInfo(path));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            return worksheet == null ? [] : ParseWorksheet(worksheet, columnCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка загрузки последовательности из файла: {Path}", path);
            throw;
        }
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

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void CreateHeader(ExcelWorksheet worksheet)
    {
        for (var i = 0; i < 4; i++)
        {
            worksheet.Cells[1, i + 1].Value = $"Column {i + 1}";
        }
    }

    private void PopulateData(ExcelWorksheet worksheet, List<SequenceRow> rows)
    {
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            PopulateRow(worksheet, row, r);
        }
    }

    private void PopulateRow(ExcelWorksheet worksheet, SequenceRow row, int rowIndex)
    {
        for (var c = 0; c < row.Columns.Count && c < 4; c++)
        {
            worksheet.Cells[rowIndex + 2, c + 1].Value = row.Columns[c];
        }
    }

    private List<SequenceRow> ParseWorksheet(ExcelWorksheet worksheet, int columnCount)
    {
        var rows = new List<SequenceRow>();
        var rowCount = worksheet.Dimension?.Rows ?? 0;

        // Skip header, start from row 2
        for (var r = 2; r <= rowCount; r++)
        {
            rows.Add(ParseRow(worksheet, r, columnCount));
        }

        return rows;
    }

    private SequenceRow ParseRow(ExcelWorksheet worksheet, int rowIndex, int columnCount)
    {
        var newRow = new SequenceRow(columnCount);
        for (var c = 1; c <= columnCount; c++)
        {
            var value = worksheet.Cells[rowIndex, c].Value?.ToString() ?? "";
            newRow.Columns[c - 1] = value;
        }
        return newRow;
    }
}
