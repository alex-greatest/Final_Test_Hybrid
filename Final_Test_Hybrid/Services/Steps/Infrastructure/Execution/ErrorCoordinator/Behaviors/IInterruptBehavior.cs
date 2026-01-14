using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;

/// <summary>
/// Strategy interface for handling specific interrupt reasons.
/// </summary>
public interface IInterruptBehavior
{
    InterruptReason Reason { get; }
    string Message { get; }
    ErrorDefinition? AssociatedError { get; }
    Task ExecuteAsync(IInterruptContext context, CancellationToken ct);
}
