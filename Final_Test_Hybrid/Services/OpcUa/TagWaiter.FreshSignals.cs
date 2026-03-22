namespace Final_Test_Hybrid.Services.OpcUa;

public partial class TagWaiter
{
    private bool TryGetCurrentValue<T>(string nodeId, out T value)
    {
        if (!ExecutionFreshSignalContext.TryGetBarrier(nodeId, out var barrier))
        {
            return subscription.TryGetValue(nodeId, out value);
        }

        if (subscription.TryGetValueEntry(nodeId, out var entry)
            && entry.UpdateSequence > barrier
            && entry.Value is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    private bool TryGetCurrentValue(string nodeId, out object? value)
    {
        if (!ExecutionFreshSignalContext.TryGetBarrier(nodeId, out var barrier))
        {
            value = subscription.GetValue(nodeId);
            return value != null;
        }

        if (subscription.TryGetValueEntry(nodeId, out var entry)
            && entry.UpdateSequence > barrier)
        {
            value = entry.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static bool IsFreshEnough(string nodeId, ulong updateSequence)
    {
        return !ExecutionFreshSignalContext.TryGetBarrier(nodeId, out var barrier)
            || updateSequence > barrier;
    }
}
