using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;

/// <summary>
/// Behavior for auto mode disabled interrupt.
/// </summary>
public sealed class AutoModeDisabledBehavior : IInterruptBehavior
{
    public InterruptReason Reason => InterruptReason.AutoModeDisabled;
    public string Message => "Нет автомата";
    public ErrorDefinition? AssociatedError => null;

    public Task ExecuteAsync(IInterruptContext context, CancellationToken ct)
    {
        context.Notifications.ShowWarning(Message, "Ожидание восстановления...");
        context.Pause();
        return Task.CompletedTask;
    }
}
