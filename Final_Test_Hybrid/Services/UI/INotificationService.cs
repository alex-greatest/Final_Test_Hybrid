namespace Final_Test_Hybrid.Services.UI
{
    public interface INotificationService
    {
        void ShowSuccess(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null);
        void ShowError(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null);
        void ShowWarning(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null);
        void ShowInfo(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null);
    }
}

