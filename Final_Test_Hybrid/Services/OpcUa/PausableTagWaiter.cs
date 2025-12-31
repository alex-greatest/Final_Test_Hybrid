using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa;

public class PausableTagWaiter(
    OpcUaSubscription subscription,
    PauseTokenSource pauseToken,
    ILogger<PausableTagWaiter> logger)
{
    public async Task<T> WaitForValueAsync<T>(
        string nodeId,
        Func<T, bool> condition,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var current = subscription.GetValue<T>(nodeId);
        if (current != null && condition(current))
        {
            return current;
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task OnValueChanged(object? value)
        {
            try
            {
                await pauseToken.WaitWhilePausedAsync(ct);
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled(ct);
                return;
            }

            if (value is T typed && condition(typed))
            {
                logger.LogInformation("Тег {NodeId} = {Value}", nodeId, typed);
                tcs.TrySetResult(typed);
            }
        }

        await subscription.SubscribeAsync(nodeId, OnValueChanged, ct);

        try
        {
            return timeout.HasValue
                ? await tcs.Task.WaitAsync(timeout.Value, ct)
                : await tcs.Task.WaitAsync(ct);
        }
        finally
        {
            await subscription.UnsubscribeAsync(nodeId, OnValueChanged, removeTag: false, ct: CancellationToken.None);
        }
    }

    public async Task<T> WaitForChangeAsync<T>(
        string nodeId,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task OnValueChanged(object? value)
        {
            try
            {
                await pauseToken.WaitWhilePausedAsync(ct);
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled(ct);
                return;
            }

            if (value is T typed)
            {
                tcs.TrySetResult(typed);
            }
        }

        await subscription.SubscribeAsync(nodeId, OnValueChanged, ct);

        try
        {
            return timeout.HasValue
                ? await tcs.Task.WaitAsync(timeout.Value, ct)
                : await tcs.Task.WaitAsync(ct);
        }
        finally
        {
            await subscription.UnsubscribeAsync(nodeId, OnValueChanged, removeTag: false, ct: CancellationToken.None);
        }
    }

    public Task WaitForTrueAsync(string nodeId, TimeSpan? timeout = null, CancellationToken ct = default)
        => WaitForValueAsync<bool>(nodeId, v => v, timeout, ct);

    public Task WaitForFalseAsync(string nodeId, TimeSpan? timeout = null, CancellationToken ct = default)
        => WaitForValueAsync<bool>(nodeId, v => !v, timeout, ct);
}
