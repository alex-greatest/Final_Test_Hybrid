using Final_Test_Hybrid.Models;
using Microsoft.Extensions.Configuration;

namespace Final_Test_Hybrid.Services
{
    public class TestSequenceService(
        ISequenceExcelService sequenceExcelService,
        IConfiguration configuration)
    {
        public string CurrentFileName { get; set; } = "sequence";
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
                throw new IOException($"Path Error: Could not create directory: {path}. {ex.Message}", ex);
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
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException($"Configuration Error: {key} is missing in appsettings.json");
            }
            return path;
        }

        private void ValidatePathExists(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Path Error: Path not found: {path}");
            }
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
}
