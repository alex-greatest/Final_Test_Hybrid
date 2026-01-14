using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;

/// <summary>
/// Behavior for PLC connection lost interrupt.
/// </summary>
public sealed class PlcConnectionLostBehavior : IInterruptBehavior
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    public InterruptReason Reason => InterruptReason.PlcConnectionLost;
    public string Message => "Потеря связи с PLC";
    public ErrorDefinition? AssociatedError => ErrorDefinitions.OpcConnectionLost;

    public async Task ExecuteAsync(IInterruptContext context, CancellationToken ct)
    {
        context.Notifications.ShowWarning(Message, $"Сброс через {ReconnectDelay.TotalSeconds:0} сек");
        await Task.Delay(ReconnectDelay, ct);
        context.Reset();
    }
}
