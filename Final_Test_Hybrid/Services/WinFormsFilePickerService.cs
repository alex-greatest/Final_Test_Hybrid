using System;
using System.Windows.Forms;

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
            string? selectedFile = OpenDialog(rootPath);
            
            if (string.IsNullOrEmpty(selectedFile))
                return null;

            if (string.IsNullOrEmpty(rootPath))
                return selectedFile;

            // Normalize paths for robust comparison
            string absRoot = System.IO.Path.GetFullPath(rootPath);
            string absSelected = System.IO.Path.GetFullPath(selectedFile);
            
            // Add directory separator to root to ensure we only match inside the folder
            // e.g. ensure root "C:\Test" doesn't match "C:\TestSteps"
            string rootCheck = absRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) 
                               + System.IO.Path.DirectorySeparatorChar;
            
            // Check if selected file starts with the root path
            if (!absSelected.StartsWith(rootCheck, StringComparison.OrdinalIgnoreCase))
            {
                 MessageBox.Show(
                     $"Выбранный файл находится за пределами разрешенной папки:\n{absRoot}\n\nПожалуйста, выберите файл внутри этой директории.", 
                     "Недопустимый выбор", 
                     MessageBoxButtons.OK, 
                     MessageBoxIcon.Warning);
                 return null;
            }

            return System.IO.Path.GetRelativePath(absRoot, absSelected);
        }

        private string? OpenDialog(string initialDirectory)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                // Normalize path separators to Windows style
                string path = initialDirectory?.Replace('/', '\\') ?? string.Empty;
                
                // Ensure absolute path
                if (!string.IsNullOrEmpty(path) && !System.IO.Path.IsPathRooted(path)) 
                {
                    path = System.IO.Path.GetFullPath(path);
                }

                if (!string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path))
                {
                    openFileDialog.InitialDirectory = path;
                }
                
                // RestoreDirectory attempts to keep the directory, but Windows often overrides this behavior based on user history
                openFileDialog.RestoreDirectory = true; 

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    return openFileDialog.FileName;
                }
            }
            return null;
        }
    }
}
