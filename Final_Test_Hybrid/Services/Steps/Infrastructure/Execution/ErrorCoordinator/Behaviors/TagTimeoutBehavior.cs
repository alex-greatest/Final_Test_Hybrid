using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;

/// <summary>
/// Behavior for tag timeout interrupt.
/// </summary>
public sealed class TagTimeoutBehavior : IInterruptBehavior
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    public InterruptReason Reason => InterruptReason.TagTimeout;
    public string Message => "Нет ответа от PLC";
    public ErrorDefinition? AssociatedError => ErrorDefinitions.TagReadTimeout;

    public async Task ExecuteAsync(IInterruptContext context, CancellationToken ct)
    {
        context.Notifications.ShowWarning(Message, $"Сброс через {ReconnectDelay.TotalSeconds:0} сек");
        await Task.Delay(ReconnectDelay, ct);
        context.Reset();
    }
}
