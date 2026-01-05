using Microsoft.Extensions.Logging;
using Radzen;

namespace Final_Test_Hybrid.Services.Common.UI
{
    public class NotificationServiceWrapper(
        NotificationService notificationService,
        IUiDispatcher uiDispatcher,
        ILogger<NotificationServiceWrapper> logger) : INotificationService
    {
        private readonly Lock _lock = new();

        public void ShowSuccess(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null)
        {
            Notify(NotificationSeverity.Success, summary, detail, duration, closeOnClick, id, style);
        }

        public void ShowError(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null)
        {
            logger.LogError("UI Error: {Summary} - {Detail}", summary, detail);
            Notify(NotificationSeverity.Error, summary, detail, duration, closeOnClick, id, style);
        }

        public void ShowWarning(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null)
        {
            Notify(NotificationSeverity.Warning, summary, detail, duration, closeOnClick, id, style);
        }

        public void ShowInfo(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null)
        {
            Notify(NotificationSeverity.Info, summary, detail, duration, closeOnClick, id, style);
        }

        private void Notify(NotificationSeverity severity, string summary, string detail, double? duration, bool closeOnClick, string? id, string? style)
        {
            uiDispatcher.Dispatch(() => NotifyCore(severity, summary, detail, duration, closeOnClick, id, style));
        }

        private void NotifyCore(NotificationSeverity severity, string summary, string detail, double? duration, bool closeOnClick, string? id, string? style)
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
                    Payload = id,
                    Style = style
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
