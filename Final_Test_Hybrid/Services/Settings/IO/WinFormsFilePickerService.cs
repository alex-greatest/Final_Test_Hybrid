using Final_Test_Hybrid.Services.IO;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Settings.IO
{
    public class WinFormsFilePickerService(ILogger<WinFormsFilePickerService> logger) : IFilePickerService
    {
        public string? PickFile(string initialDirectory, string filter = "")
        {
            return OpenDialog(initialDirectory, filter);
        }

        public string? PickFileRelative(string rootPath)
        {
            var selectedFile = OpenDialog(rootPath, "");
            return string.IsNullOrEmpty(selectedFile) ? null : ProcessSelection(selectedFile, rootPath);
        }

        public string? SaveFile(string defaultName, string? initialDirectory = null, string filter = "Файлы Excel (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*")
        {
            using var saveFileDialog = new SaveFileDialog();
            saveFileDialog.FileName = defaultName;
            saveFileDialog.Filter = filter;

            var validatedPath = string.IsNullOrEmpty(initialDirectory) ? null : GetValidatedPath(initialDirectory.Replace('/', '\\'));
            
            if (!string.IsNullOrEmpty(validatedPath))
            {
                saveFileDialog.InitialDirectory = validatedPath;
            }
            else
            {
                saveFileDialog.RestoreDirectory = true;
            }

            return saveFileDialog.ShowDialog() == DialogResult.OK ? saveFileDialog.FileName : null;
        }

        private string? ProcessSelection(string selectedFile, string rootPath)
        {
            return string.IsNullOrEmpty(rootPath) ? selectedFile : ValidateAndGetRelativePath(selectedFile, rootPath);
        }

        private string? ValidateAndGetRelativePath(string selectedFile, string rootPath)
        {
            var absRoot = Path.GetFullPath(rootPath);
            var absSelected = Path.GetFullPath(selectedFile);
            var rootCheck = absRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) 
                               + Path.DirectorySeparatorChar;
            if (absSelected.StartsWith(rootCheck, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(absRoot, absSelected);
            }
            ShowInvalidPathMessage(absRoot);
            return null;
        }

        private void ShowInvalidPathMessage(string absRoot)
        {
            logger.LogWarning("Попытка выбора файла вне разрешенной директории: {RootPath}", absRoot);
            MessageBox.Show(
                $"Выбранный файл находится за пределами разрешенной папки:\n{absRoot}\n\nПожалуйста, выберите файл внутри этой директории.", 
                "Недопустимый выбор", 
                MessageBoxButtons.OK, 
                MessageBoxIcon.Warning);
        }

        private string? OpenDialog(string initialDirectory, string filter)
        {
            using var openFileDialog = new OpenFileDialog();
            ConfigureFileDialog(openFileDialog, initialDirectory, filter);
            return openFileDialog.ShowDialog() == DialogResult.OK ? openFileDialog.FileName : null;
        }

        private void ConfigureFileDialog(OpenFileDialog dialog, string initialDirectory, string filter)
        {
            if (!string.IsNullOrEmpty(filter))
            {
                dialog.Filter = filter;
            }

            var path = initialDirectory?.Replace('/', '\\') ?? string.Empty;
            var validatedPath = GetValidatedPath(path);
            
            if (!string.IsNullOrEmpty(validatedPath))
            {
                dialog.InitialDirectory = validatedPath;
            }
            
            dialog.RestoreDirectory = true;
        }

        private string? GetValidatedPath(string path)
        {
            var absolutePath = EnsureAbsolutePath(path);
            return CheckPathExists(absolutePath);
        }

        private string EnsureAbsolutePath(string path)
        {
            if (!string.IsNullOrEmpty(path) && !Path.IsPathRooted(path)) 
            {
                return Path.GetFullPath(path);
            }
            return path;
        }

        private string? CheckPathExists(string path)
        {
            return !string.IsNullOrEmpty(path) && Directory.Exists(path) ? path : null;
        }
    }
}
