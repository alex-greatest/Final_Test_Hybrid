namespace Final_Test_Hybrid.Services.Common;

public sealed class AsyncLock : IAsyncDisposable
{
    private readonly SemaphoreSlim _semaphore;

    private AsyncLock(SemaphoreSlim semaphore) => _semaphore = semaphore;

    public static async Task<AsyncLock> AcquireAsync(
        SemaphoreSlim semaphore,
        CancellationToken ct = default)
    {
        await semaphore.WaitAsync(ct);
        return new AsyncLock(semaphore);
    }

    public ValueTask DisposeAsync()
    {
        _semaphore.Release();
        return ValueTask.CompletedTask;
    }
}