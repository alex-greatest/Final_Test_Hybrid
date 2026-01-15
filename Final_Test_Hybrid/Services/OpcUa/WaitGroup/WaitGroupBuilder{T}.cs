namespace Final_Test_Hybrid.Services.OpcUa.WaitGroup;

public class WaitGroupBuilder<TResult>
{
    private readonly List<TagWaitCondition> _conditions = [];
    private readonly List<Func<object?, TResult>> _resultCallbacks = [];
    internal IReadOnlyList<TagWaitCondition> Conditions => _conditions;
    internal IReadOnlyList<Func<object?, TResult>> ResultCallbacks => _resultCallbacks;
    internal TimeSpan? Timeout { get; private set; }

    public WaitGroupBuilder<TResult> WithTimeout(TimeSpan timeout)
    {
        Timeout = timeout;
        return this;
    }

    public WaitGroupBuilder<TResult> WaitFor<TValue>(
        string nodeId,
        Func<TValue, bool> condition,
        Func<TValue, TResult> onTriggered,
        string? name = null)
    {
        _conditions.Add(new TagWaitCondition
        {
            NodeId = nodeId,
            Condition = value => value is TValue typed && condition(typed),
            Name = name
        });
        _resultCallbacks.Add(value => value is TValue typed ? onTriggered(typed) : default!);
        return this;
    }

    public WaitGroupBuilder<TResult> WaitForTrue(string nodeId, Func<TResult> onTriggered, string? name = null)
    {
        return WaitFor<bool>(nodeId, v => v, _ => onTriggered(), name);
    }

    public WaitGroupBuilder<TResult> WaitForFalse(string nodeId, Func<TResult> onTriggered, string? name = null)
    {
        return WaitFor<bool>(nodeId, v => !v, _ => onTriggered(), name);
    }

    public WaitGroupBuilder<TResult> WaitForAllTrue(
        IReadOnlyList<string> nodeIds,
        Func<TResult> resultFactory,
        string? name = null)
    {
        if (nodeIds.Count == 0)
        {
            throw new ArgumentException("nodeIds cannot be empty", nameof(nodeIds));
        }

        _conditions.Add(new TagWaitCondition
        {
            NodeId = nodeIds[0],
            AdditionalNodeIds = nodeIds.Count > 1 ? nodeIds.Skip(1).ToList() : null,
            Condition = value => value is bool b && b,
            Name = name
        });
        _resultCallbacks.Add(_ => resultFactory());
        return this;
    }

    internal WaitGroupBuilder<TResult> AddCondition(
        TagWaitCondition condition,
        Func<object?, TResult> resultCallback)
    {
        _conditions.Add(condition);
        _resultCallbacks.Add(resultCallback);
        return this;
    }
}
