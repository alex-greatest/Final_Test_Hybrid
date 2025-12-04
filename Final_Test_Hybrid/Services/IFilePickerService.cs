namespace Final_Test_Hybrid.Services
{
    public interface IFilePickerService
    {
        string? PickFile(string initialDirectory);
        string? PickFileRelative(string rootPath);
    }
}

