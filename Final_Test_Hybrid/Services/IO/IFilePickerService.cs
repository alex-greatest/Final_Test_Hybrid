namespace Final_Test_Hybrid.Services.IO
{
    public interface IFilePickerService
    {
        string? PickFile(string initialDirectory, string filter = "");
        string? PickFileRelative(string rootPath);
        string? SaveFile(string defaultName, string? initialDirectory = null, string filter = "Файлы Excel (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*");
    }
}
