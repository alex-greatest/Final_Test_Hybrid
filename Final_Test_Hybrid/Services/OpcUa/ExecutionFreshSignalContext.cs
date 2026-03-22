namespace Final_Test_Hybrid.Services.OpcUa;

/// <summary>
/// Хранит execution-scoped fresh-barrier для terminal-сигналов текущего PLC-шага.
/// </summary>
internal static class ExecutionFreshSignalContext
{
    private static readonly AsyncLocal<ScopeState?> Current = new();

    public static IDisposable Enter(string startTag, string endTag, string errorTag)
    {
        var previous = Current.Value;
        Current.Value = new ScopeState(startTag, endTag, errorTag);
        return new RestoreScope(previous);
    }

    public static void MarkAttemptStarted(string nodeId, bool value, ulong barrier)
    {
        var scope = Current.Value;
        if (scope == null || !value || !string.Equals(scope.StartTag, nodeId, StringComparison.Ordinal))
        {
            return;
        }

        scope.AttemptBarrier = barrier;
    }

    public static bool TryGetBarrier(string nodeId, out ulong barrier)
    {
        var scope = Current.Value;
        if (scope?.AttemptBarrier is not { } attemptBarrier)
        {
            barrier = 0;
            return false;
        }

        var isTerminalTag = string.Equals(scope.EndTag, nodeId, StringComparison.Ordinal)
            || string.Equals(scope.ErrorTag, nodeId, StringComparison.Ordinal);
        if (!isTerminalTag)
        {
            barrier = 0;
            return false;
        }

        barrier = attemptBarrier;
        return true;
    }

    private sealed class ScopeState(string startTag, string endTag, string errorTag)
    {
        public string StartTag { get; } = startTag;
        public string EndTag { get; } = endTag;
        public string ErrorTag { get; } = errorTag;
        public ulong? AttemptBarrier { get; set; }
    }

    private readonly struct RestoreScope(ScopeState? previous) : IDisposable
    {
        public void Dispose()
        {
            Current.Value = previous;
        }
    }
}
