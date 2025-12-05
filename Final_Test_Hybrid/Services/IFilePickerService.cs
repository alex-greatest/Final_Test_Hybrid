namespace Final_Test_Hybrid.Services
{
    public interface IFilePickerService
    {
        string? PickFile(string initialDirectory, string filter = "");
        string? PickFileRelative(string rootPath);
        string? SaveFile(string defaultName, string? initialDirectory = null, string filter = "Excel Files (*.xlsx)|*.xlsx|All files (*.*)|*.*");
    }
}
