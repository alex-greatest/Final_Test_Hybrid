using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;

/// <summary>
/// Behavior for boiler BlockA interrupt (status 1005 == 2).
/// </summary>
public sealed class BoilerBlockABehavior : IInterruptBehavior
{
    public InterruptReason Reason => InterruptReason.BoilerBlockA;
    public string Message => "Блокировка А";
    public ErrorDefinition? AssociatedError => null;

    public Task ExecuteAsync(IInterruptContext context, CancellationToken ct)
    {
        context.Notifications.ShowWarning(Message, "Остановите тест");
        context.Pause();
        return Task.CompletedTask;
    }
}
