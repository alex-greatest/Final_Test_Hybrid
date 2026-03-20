using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class PlcConnectionLostBehaviorNotificationTests
{
    [Fact]
    public async Task ExecuteAsync_ShowsWarningWithStableTimingAndId()
    {
        var notifications = new RecordingNotificationService();
        var context = new RecordingInterruptContext(notifications);
        var behavior = new PlcConnectionLostBehavior();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => behavior.ExecuteAsync(context, cts.Token));

        var warning = Assert.Single(notifications.Warnings);
        Assert.Equal("Потеря связи с PLC", warning.Summary);
        Assert.Equal("Сброс через 5 сек", warning.Detail);
        Assert.Equal(5000d, warning.Duration);
        Assert.False(warning.CloseOnClick);
        Assert.Equal("interrupt-plc-connection-lost", warning.Id);
        Assert.False(context.ResetCalled);
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<WarningNotification> Warnings { get; } = [];

        public void ShowSuccess(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null)
        {
        }

        public void ShowError(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null)
        {
        }

        public void ShowWarning(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null)
        {
            Warnings.Add(new WarningNotification(summary, detail, duration, closeOnClick, id));
        }

        public void ShowInfo(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null)
        {
        }
    }

    private sealed class RecordingInterruptContext(RecordingNotificationService notifications) : IInterruptContext
    {
        public bool ResetCalled { get; private set; }

        public void Pause()
        {
        }

        public void Reset()
        {
            ResetCalled = true;
        }

        private TestErrorService ErrorServiceStub { get; } = new();

        IErrorService IInterruptContext.ErrorService => ErrorServiceStub;

        public INotificationService Notifications => notifications;
    }

    private sealed record WarningNotification(
        string Summary,
        string Detail,
        double? Duration,
        bool CloseOnClick,
        string? Id);
}
