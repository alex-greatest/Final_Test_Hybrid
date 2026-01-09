using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.OpcUa.WaitGroup;

namespace Final_Test_Hybrid.Services.OpcUa;

public class PausableTagWaiter(
    TagWaiter inner,
    PauseTokenSource pauseToken)
{
    public async Task<T> WaitForValueAsync<T>(
        string nodeId,
        Func<T, bool> condition,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.WaitForValueAsync(nodeId, condition, timeout, ct);
    }

    public async Task<T> WaitForChangeAsync<T>(
        string nodeId,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.WaitForChangeAsync<T>(nodeId, timeout, ct);
    }

    public async Task WaitForTrueAsync(string nodeId, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        await inner.WaitForTrueAsync(nodeId, timeout, ct);
    }

    public async Task WaitForFalseAsync(string nodeId, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        await inner.WaitForFalseAsync(nodeId, timeout, ct);
    }

    public WaitGroupBuilder CreateWaitGroup() => inner.CreateWaitGroup();

    public WaitGroupBuilder<TResult> CreateWaitGroup<TResult>() => inner.CreateWaitGroup<TResult>();

    public async Task<TagWaitResult> WaitAnyAsync(WaitGroupBuilder builder, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.WaitAnyAsync(builder, ct);
    }

    public async Task<TagWaitResult<TResult>> WaitAnyAsync<TResult>(
        WaitGroupBuilder<TResult> builder,
        CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.WaitAnyAsync(builder, ct);
    }
}
