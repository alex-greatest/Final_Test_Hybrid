using Final_Test_Hybrid.Models;
using Microsoft.Extensions.Configuration;

namespace Final_Test_Hybrid.Services
{
    public class TestSequenceService(
        IFilePickerService filePickerService,
        ISequenceExcelService sequenceExcelService,
        IConfiguration configuration,
        INotificationService notificationService)
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
            sequenceExcelService.SaveSequence(path, rows);
        }

        public List<SequenceRow> LoadFromExcel(string path, int columnCount)
        {
            return sequenceExcelService.LoadSequence(path, columnCount);
        }

        public string? GetTestsSequencePath()
        {
            var path = configuration["Paths:PathToTestsSequence"];

            if (!string.IsNullOrEmpty(path))
            {
                return GetOrCreateDirectory(path);
            }
            notificationService.ShowError("Configuration Error", "PathToTestsSequence is missing in appsettings.json");
            return null;

        }

        private string? GetOrCreateDirectory(string path)
        {
            return Directory.Exists(path) ? path : TryCreateDirectory(path);
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
                notificationService.ShowError("Path Error", $"Could not create directory: {path}. {ex.Message}");
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
            notificationService.ShowError("Configuration Error", "PathToTestSteps is missing in appsettings.json");
            return false;
        }

        private bool ValidatePathExists(string path)
        {
            if (Directory.Exists(path))
            {
                return true;
            }
            notificationService.ShowError("Path Error", $"Path not found: {path}");
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
    }
}
