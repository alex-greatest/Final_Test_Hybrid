namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

public class ScanInputStateManager : IDisposable
{
    private readonly SemaphoreSlim _processLock = new(1, 1);
    public bool IsProcessing { get; private set; }
    public event Action? OnStateChanged;

    public bool TryAcquireProcessLock()
    {
        return _processLock.Wait(0);
    }

    public void ReleaseProcessLock()
    {
        _processLock.Release();
    }

    public void SetProcessing(bool isProcessing)
    {
        IsProcessing = isProcessing;
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }

    public void Dispose()
    {
        _processLock.Dispose();
    }
}
