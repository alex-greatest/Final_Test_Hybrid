using Final_Test_Hybrid.Models;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using Radzen;
using FileInfo = System.IO.FileInfo;

namespace Final_Test_Hybrid.Services
{
    public class TestSequenceService(
        IFilePickerService filePickerService,
        IConfiguration configuration,
        NotificationService notificationService)
    {
        public string CurrentFileName { get; set; } = "sequence";
        public string? CurrentFilePath { get; set; }
        
        public List<SequenceRow> InitializeRows(int count, int columnCount)
        {
            var rows = new List<SequenceRow>();
            for (var i = 0; i < count; i++)
            {
                rows.Add(new SequenceRow(columnCount));
            }
            return rows;
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

        public async Task PrepareForDelete(SequenceRow row)
        {
            row.CssClass = "row-animate-delete";
            await Task.Delay(500);
        }

        public void RemoveRow(List<SequenceRow> rows, SequenceRow row)
        {
            rows.Remove(row);
        }

        public void OpenFolder(SequenceRow row, int colIndex)
        {
            var rootPath = GetValidRootPath();
            if (rootPath == null)
            {
                return;
            }

            var relativePath = filePickerService.PickFileRelative(rootPath);
            UpdateRowColumn(row, colIndex, relativePath);
        }

        public void SaveToExcel(string path, List<SequenceRow> rows)
        {
            EnsureDirectoryExists(path);

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Sequence");

            CreateHeader(worksheet);
            PopulateData(worksheet, rows);

            package.SaveAs(new FileInfo(path));
        }

        private void EnsureDirectoryExists(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) 
            {
                return;
            }
            
            TryCreateDirectoryIfNotExists(directory);
        }

        private void TryCreateDirectoryIfNotExists(string directory)
        {
            if (Directory.Exists(directory))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                NotifyError("Path Error", $"Could not create directory: {directory}. {ex.Message}");
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

        public List<SequenceRow> LoadFromExcel(string path, int columnCount)
        {
            using var package = new ExcelPackage(new FileInfo(path));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            return worksheet == null ? [] : ParseWorksheet(worksheet, columnCount);
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

        public string? GetTestsSequencePath()
        {
            var path = configuration["Paths:PathToTestsSequence"];
            
            if (string.IsNullOrEmpty(path))
            {
                NotifyError("Configuration Error", "PathToTestsSequence is missing in appsettings.json");
                return null;
            }

            return GetOrCreateDirectory(path);
        }

        private string? GetOrCreateDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                return path;
            }

            return TryCreateDirectory(path);
        }

        private string? TryCreateDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return path;
            }
            catch (Exception ex)
            {
                NotifyError("Path Error", $"Could not create directory: {path}. {ex.Message}");
                return null;
            }
        }

        private string? GetValidRootPath()
        {
            var path = configuration["Paths:PathToTestSteps"];
            
            return !ValidatePathConfigured(path) ? null : CheckPathExists(path!);
        }

        private string? CheckPathExists(string path)
        {
            return !ValidatePathExists(path) ? null : path;
        }

        private bool ValidatePathConfigured(string? path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                return true;
            }
            NotifyError("Configuration Error", "PathToTestSteps is missing in appsettings.json");
            return false;
        }

        private bool ValidatePathExists(string path)
        {
            if (Directory.Exists(path))
            {
                return true;
            }
            NotifyError("Path Error", $"Path not found: {path}");
            return false;
        }

        private void UpdateRowColumn(SequenceRow row, int colIndex, string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return;
            }
            row.Columns[colIndex] = relativePath;
        }

        private void NotifyError(string summary, string detail)
        {
            notificationService.Notify(new NotificationMessage 
            { 
                Severity = NotificationSeverity.Error, 
                Summary = summary, 
                Detail = detail 
            });
        }
    }
}
