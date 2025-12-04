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
            
            if (string.IsNullOrEmpty(selectedFile))
                return null;

            if (string.IsNullOrEmpty(rootPath))
                return selectedFile;

            // Normalize paths for robust comparison
            var absRoot = System.IO.Path.GetFullPath(rootPath);
            var absSelected = System.IO.Path.GetFullPath(selectedFile);
            
            // Add directory separator to root to ensure we only match inside the folder
            // e.g. ensure root "C:\Test" doesn't match "C:\TestSteps"
            var rootCheck = absRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) 
                               + System.IO.Path.DirectorySeparatorChar;
            
            // Check if selected file starts with the root path
            if (absSelected.StartsWith(rootCheck, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(absRoot, absSelected);
            }
      
            MessageBox.Show(
                $"Выбранный файл находится за пределами разрешенной папки:\n{absRoot}\n\nПожалуйста, выберите файл внутри этой директории.", 
                "Недопустимый выбор", 
                MessageBoxButtons.OK, 
                MessageBoxIcon.Warning);
            return null;

        }

        private string? OpenDialog(string initialDirectory)
        {
            using var openFileDialog = new OpenFileDialog();
            // Normalize path separators to Windows style
            var path = initialDirectory?.Replace('/', '\\') ?? string.Empty;
                
            // Ensure absolute path
            if (!string.IsNullOrEmpty(path) && !System.IO.Path.IsPathRooted(path)) 
            {
                path = Path.GetFullPath(path);
            }

            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                openFileDialog.InitialDirectory = path;
            }
                
            // RestoreDirectory attempts to keep the directory, but Windows often overrides this behavior based on user history
            openFileDialog.RestoreDirectory = true; 
            return openFileDialog.ShowDialog() == DialogResult.OK ? openFileDialog.FileName : null;
        }
    }
}
