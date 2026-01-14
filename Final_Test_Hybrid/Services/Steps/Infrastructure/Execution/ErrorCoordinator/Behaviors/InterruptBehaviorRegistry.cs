namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;

/// <summary>
/// Registry of all interrupt behaviors.
/// </summary>
public sealed class InterruptBehaviorRegistry
{
    private readonly Dictionary<InterruptReason, IInterruptBehavior> _behaviors;

    public InterruptBehaviorRegistry(IEnumerable<IInterruptBehavior> behaviors)
        => _behaviors = behaviors.ToDictionary(b => b.Reason);

    public IInterruptBehavior? Get(InterruptReason reason)
        => _behaviors.GetValueOrDefault(reason);
}
