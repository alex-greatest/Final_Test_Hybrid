namespace Final_Test_Hybrid.Services
{
    public interface INotificationService
    {
        void ShowSuccess(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null);
        void ShowError(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null);
        void ShowWarning(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null);
        void ShowInfo(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null);
    }
}


