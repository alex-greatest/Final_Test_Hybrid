namespace Final_Test_Hybrid.Services
{
    public class WinFormsFilePickerService : IFilePickerService
    {
        public string? PickFile(string initialDirectory)
        {
            return OpenDialog(initialDirectory);
        }

        public string? PickFileRelative(string rootPath)
        {
            var selectedFile = OpenDialog(rootPath);
            return string.IsNullOrEmpty(selectedFile) ? null : ProcessSelection(selectedFile, rootPath);
        }

        private string? ProcessSelection(string selectedFile, string rootPath)
        {
            return string.IsNullOrEmpty(rootPath) ? selectedFile : ValidateAndGetRelativePath(selectedFile, rootPath);
        }

        private string? ValidateAndGetRelativePath(string selectedFile, string rootPath)
        {
            // Normalize paths for robust comparison
            var absRoot = System.IO.Path.GetFullPath(rootPath);
            var absSelected = System.IO.Path.GetFullPath(selectedFile);
            
            // Add directory separator to root to ensure we only match inside the folder
            var rootCheck = absRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) 
                               + System.IO.Path.DirectorySeparatorChar;
            
            // Check if selected file starts with the root path
            if (absSelected.StartsWith(rootCheck, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(absRoot, absSelected);
            }
      
            ShowInvalidPathMessage(absRoot);
            return null;
        }

        private void ShowInvalidPathMessage(string absRoot)
        {
            MessageBox.Show(
                $"Выбранный файл находится за пределами разрешенной папки:\n{absRoot}\n\nПожалуйста, выберите файл внутри этой директории.", 
                "Недопустимый выбор", 
                MessageBoxButtons.OK, 
                MessageBoxIcon.Warning);
        }

        private string? OpenDialog(string initialDirectory)
        {
            using var openFileDialog = new OpenFileDialog();
            ConfigureFileDialog(openFileDialog, initialDirectory);
            return openFileDialog.ShowDialog() == DialogResult.OK ? openFileDialog.FileName : null;
        }

        private void ConfigureFileDialog(OpenFileDialog dialog, string initialDirectory)
        {
            // Normalize path separators to Windows style
            var path = initialDirectory?.Replace('/', '\\') ?? string.Empty;
            var validatedPath = GetValidatedPath(path);
            if (!string.IsNullOrEmpty(validatedPath))
            {
                dialog.InitialDirectory = validatedPath;
            }
            // RestoreDirectory attempts to keep the directory, but Windows often overrides this behavior based on user history
            dialog.RestoreDirectory = true;
        }

        private string? GetValidatedPath(string path)
        {
            var absolutePath = EnsureAbsolutePath(path);
            return CheckPathExists(absolutePath);
        }

        private string EnsureAbsolutePath(string path)
        {
            // Ensure absolute path
            if (!string.IsNullOrEmpty(path) && !System.IO.Path.IsPathRooted(path)) 
            {
                return Path.GetFullPath(path);
            }
            return path;
        }

        private string? CheckPathExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                return path;
            }
            return null;
        }
    }
}
