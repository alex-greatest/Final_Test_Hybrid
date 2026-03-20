using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Main.Messages;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;

/// <summary>
/// Behavior for PLC connection lost interrupt.
/// </summary>
public sealed class PlcConnectionLostBehavior : IInterruptBehavior
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private const string NotificationId = "interrupt-plc-connection-lost";

    public InterruptReason Reason => InterruptReason.PlcConnectionLost;
    public string Message => MessageTextResources.PlcConnectionLostTitle;
    public ErrorDefinition AssociatedError => ErrorDefinitions.OpcConnectionLost;

    public async Task ExecuteAsync(IInterruptContext context, CancellationToken ct)
    {
        context.Notifications.ShowWarning(
            Message,
            MessageTextResources.PlcConnectionLostToastDetail(ReconnectDelay.TotalSeconds),
            ReconnectDelay.TotalMilliseconds,
            closeOnClick: false,
            id: NotificationId);
        await Task.Delay(ReconnectDelay, ct);
        context.Reset();
    }
}
