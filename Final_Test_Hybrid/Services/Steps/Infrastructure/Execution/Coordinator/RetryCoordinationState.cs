namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

internal sealed class RetryCoordinationState
{
    private readonly Lock _stateLock = new();
    private readonly HashSet<int> _suppressedColumns = [];
    private int _activeCount;

    public bool IsActive => Volatile.Read(ref _activeCount) > 0;

    public bool IsColumnSuppressed(int columnIndex)
    {
        lock (_stateLock)
        {
            return _suppressedColumns.Contains(columnIndex);
        }
    }

    public void MarkRequested(int columnIndex)
    {
        lock (_stateLock)
        {
            if (!_suppressedColumns.Add(columnIndex))
            {
                return;
            }

            Interlocked.Increment(ref _activeCount);
        }
    }

    public void MarkCompleted(int columnIndex)
    {
        lock (_stateLock)
        {
            if (!_suppressedColumns.Remove(columnIndex))
            {
                return;
            }

            Interlocked.Decrement(ref _activeCount);
        }
    }

    public void Reset()
    {
        lock (_stateLock)
        {
            _suppressedColumns.Clear();
        }

        Interlocked.Exchange(ref _activeCount, 0);
    }
}
