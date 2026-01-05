using Final_Test_Hybrid.Services.Common.UI;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

public class ScanErrorHandler(INotificationService notificationService)
{
    public void ShowSuccess(string title, string message)
    {
        notificationService.ShowSuccess(title, message);
    }

    public void ShowError(string title, string message)
    {
        notificationService.ShowError(title, message);
    }
}
