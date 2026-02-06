using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa;

public partial class TagWaiter(
    OpcUaSubscription subscription,
    OpcUaConnectionState connectionState,
    ILogger<TagWaiter> logger,
    ITestStepLogger testLogger)
{
    private const int SubscribeRetryAttempts = 3;
    private static readonly TimeSpan SubscribeRetryDelay = TimeSpan.FromMilliseconds(250);

    #region WaitForValueAsync

    /// <summary>
    /// Ожидает значение тега, удовлетворяющее условию.
    /// </summary>
    public Task<T> WaitForValueAsync<T>(
        string nodeId,
        Func<T, bool> condition,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
        => WaitForValueAsync(nodeId, condition, pauseGate: null, timeout, ct);

    /// <summary>
    /// Ожидает значение тега, удовлетворяющее условию (pause-aware).
    /// </summary>
    public async Task<T> WaitForValueAsync<T>(
        string nodeId,
        Func<T, bool> condition,
        PauseTokenSource? pauseGate,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        if (pauseGate?.IsPaused != true)
        {
            var raw = subscription.GetValue(nodeId);
            if (raw is T current && condition(current))
            {
                return current;
            }
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task OnValueChanged(object? value)
        {
            if (pauseGate?.IsPaused == true || value is not T typed || !condition(typed))
            {
                return Task.CompletedTask;
            }

            testLogger.LogInformation("  Получен сигнал: {Value}", typed);
            tcs.TrySetResult(typed);
            return Task.CompletedTask;
        }

        await SubscribeWithReconnectRetryAsync(nodeId, OnValueChanged, ct);

        Action? onResumed = null;
        try
        {
            if (pauseGate != null)
            {
                onResumed = () => RecheckValue(nodeId, condition, tcs, pauseGate);
                pauseGate.OnResumed += onResumed;
            }

            RecheckValue(nodeId, condition, tcs, pauseGate);
            return await WaitWithTimeoutAsync(tcs.Task, timeout, pauseGate, ct);
        }
        finally
        {
            if (onResumed != null)
            {
                pauseGate!.OnResumed -= onResumed;
            }
            await subscription.UnsubscribeAsync(nodeId, OnValueChanged, removeTag: false, ct: CancellationToken.None);
        }
    }

    #endregion

    #region WaitForChangeAsync

    /// <summary>
    /// Ожидает любое изменение значения тега.
    /// </summary>
    public Task<T> WaitForChangeAsync<T>(
        string nodeId,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
        => WaitForChangeAsync<T>(nodeId, pauseGate: null, timeout, ct);

    /// <summary>
    /// Ожидает любое изменение значения тега (pause-aware).
    /// </summary>
    public async Task<T> WaitForChangeAsync<T>(
        string nodeId,
        PauseTokenSource? pauseGate,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task OnValueChanged(object? value)
        {
            if (pauseGate?.IsPaused == true)
            {
                return Task.CompletedTask;
            }
            if (value is T typed)
            {
                tcs.TrySetResult(typed);
            }
            return Task.CompletedTask;
        }

        await SubscribeWithReconnectRetryAsync(nodeId, OnValueChanged, ct);

        try
        {
            return await WaitWithTimeoutAsync(tcs.Task, timeout, pauseGate, ct);
        }
        finally
        {
            await subscription.UnsubscribeAsync(nodeId, OnValueChanged, removeTag: false, ct: CancellationToken.None);
        }
    }

    #endregion

    #region WaitForTrue/False

    /// <summary>
    /// Ожидает пока тег станет true.
    /// </summary>
    public Task WaitForTrueAsync(string nodeId, TimeSpan? timeout = null, CancellationToken ct = default)
        => WaitForTrueAsync(nodeId, pauseGate: null, timeout, ct);

    /// <summary>
    /// Ожидает пока тег станет true (pause-aware).
    /// </summary>
    public Task WaitForTrueAsync(
        string nodeId,
        PauseTokenSource? pauseGate,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
        => WaitForValueAsync<bool>(nodeId, v => v, pauseGate, timeout, ct);

    /// <summary>
    /// Ожидает пока тег станет false.
    /// </summary>
    public Task WaitForFalseAsync(string nodeId, TimeSpan? timeout = null, CancellationToken ct = default)
        => WaitForFalseAsync(nodeId, pauseGate: null, timeout, ct);

    /// <summary>
    /// Ожидает пока тег станет false (pause-aware).
    /// </summary>
    public Task WaitForFalseAsync(
        string nodeId,
        PauseTokenSource? pauseGate,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
        => WaitForValueAsync<bool>(nodeId, v => !v, pauseGate, timeout, ct);

    #endregion
}
