using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;

/// <summary>
/// Behavior for boiler lock interrupt (status 1005 == 1).
/// </summary>
public sealed class BoilerLockBehavior : IInterruptBehavior
{
    public InterruptReason Reason => InterruptReason.BoilerLock;
    public string Message => "Блокировка котла";
    public ErrorDefinition? AssociatedError => null;

    public Task ExecuteAsync(IInterruptContext context, CancellationToken ct)
    {
        context.Notifications.ShowWarning(Message, "Ожидание восстановления...");
        context.Pause();
        return Task.CompletedTask;
    }
}
