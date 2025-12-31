namespace Final_Test_Hybrid.Services.OpcUa.WaitGroup;

public class WaitGroupBuilder
{
    private readonly List<TagWaitCondition> _conditions = [];
    private readonly List<Func<object?, Task>?> _callbacks = [];
    internal IReadOnlyList<TagWaitCondition> Conditions => _conditions;
    internal IReadOnlyList<Func<object?, Task>?> Callbacks => _callbacks;
    internal TimeSpan? Timeout { get; private set; }

    public WaitGroupBuilder WithTimeout(TimeSpan timeout)
    {
        Timeout = timeout;
        return this;
    }

    public WaitGroupBuilder WaitFor(
        string nodeId,
        Func<object?, bool> condition,
        Action? onTriggered = null,
        string? name = null)
    {
        _conditions.Add(new TagWaitCondition
        {
            NodeId = nodeId,
            Condition = condition,
            Name = name
        });
        _callbacks.Add(WrapAction(onTriggered));
        return this;
    }

    public WaitGroupBuilder WaitFor<T>(
        string nodeId,
        Func<T, bool> condition,
        Action<T>? onTriggered = null,
        string? name = null)
    {
        _conditions.Add(new TagWaitCondition
        {
            NodeId = nodeId,
            Condition = value => value is T typed && condition(typed),
            Name = name
        });
        _callbacks.Add(WrapTypedAction(onTriggered));
        return this;
    }

    public WaitGroupBuilder WaitForTrue(string nodeId, Action? onTriggered = null, string? name = null)
    {
        return WaitFor<bool>(nodeId, v => v, WrapSimpleAction(onTriggered), name);
    }

    public WaitGroupBuilder WaitForFalse(string nodeId, Action? onTriggered = null, string? name = null)
    {
        return WaitFor<bool>(nodeId, v => !v, WrapSimpleAction(onTriggered), name);
    }

    private static Func<object?, Task>? WrapAction(Action? action)
    {
        if (action == null)
        {
            return null;
        }
        return _ =>
        {
            action();
            return Task.CompletedTask;
        };
    }

    private static Func<object?, Task>? WrapTypedAction<T>(Action<T>? action)
    {
        if (action == null)
        {
            return null;
        }
        return value =>
        {
            if (value is T typed)
            {
                action(typed);
            }
            return Task.CompletedTask;
        };
    }

    private static Action<bool>? WrapSimpleAction(Action? action)
    {
        if (action == null)
        {
            return null;
        }
        return _ => action();
    }
}
