using Radzen;

namespace Final_Test_Hybrid.Services
{
    public class NotificationServiceWrapper(NotificationService notificationService) : INotificationService
    {
        private readonly Lock _lock = new();

        public void ShowSuccess(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null)
        {
            Notify(NotificationSeverity.Success, summary, detail, duration, closeOnClick, id);
        }

        public void ShowError(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null)
        {
            Notify(NotificationSeverity.Error, summary, detail, duration, closeOnClick, id);
        }

        public void ShowWarning(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null)
        {
            Notify(NotificationSeverity.Warning, summary, detail, duration, closeOnClick, id);
        }

        public void ShowInfo(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null)
        {
            Notify(NotificationSeverity.Info, summary, detail, duration, closeOnClick, id);
        }

        private void Notify(NotificationSeverity severity, string summary, string detail, double? duration, bool closeOnClick, string? id)
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    RemoveMessageById(id);
                }

                notificationService.Notify(new NotificationMessage
                {
                    Severity = severity,
                    Summary = summary,
                    Detail = detail,
                    Duration = duration ?? 4000,
                    CloseOnClick = closeOnClick,
                    Payload = id
                });
            }
        }

        private void RemoveMessageById(string id)
        {
            try
            {
                var messages = notificationService.Messages;
                if (messages == null) return;

                var existingMessages = messages
                    .Where(m => m.Payload is string payloadId && payloadId == id)
                    .ToList();

                foreach (var msg in existingMessages)
                {
                    messages.Remove(msg);
                }
            }
            catch
            {
                // Safe suppression: concurrent modification or UI thread issues should not crash the app
            }
        }
    }
}
