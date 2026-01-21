using System.Diagnostics;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.OpcUa.WaitGroup;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa;

public class TagWaiter(
    OpcUaSubscription subscription,
    ILogger<TagWaiter> logger,
    ITestStepLogger testLogger)
{
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
            var current = subscription.GetValue<T>(nodeId);
            if (current != null && condition(current))
            {
                return current;
            }
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task OnValueChanged(object? value)
        {
            if (pauseGate?.IsPaused == true)
            {
                return Task.CompletedTask;
            }
            if (value is not T typed || !condition(typed))
            {
                return Task.CompletedTask;
            }
            logger.LogInformation("Тег {NodeId} = {Value}", nodeId, typed);
            testLogger.LogInformation("  Получен сигнал: {Value}", typed);
            tcs.TrySetResult(typed);
            return Task.CompletedTask;
        }

        await subscription.SubscribeAsync(nodeId, OnValueChanged, ct);

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

        await subscription.SubscribeAsync(nodeId, OnValueChanged, ct);

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
    public Task WaitForTrueAsync(string nodeId, PauseTokenSource? pauseGate, TimeSpan? timeout = null, CancellationToken ct = default)
        => WaitForValueAsync<bool>(nodeId, v => v, pauseGate, timeout, ct);

    /// <summary>
    /// Ожидает пока тег станет false.
    /// </summary>
    public Task WaitForFalseAsync(string nodeId, TimeSpan? timeout = null, CancellationToken ct = default)
        => WaitForFalseAsync(nodeId, pauseGate: null, timeout, ct);

    /// <summary>
    /// Ожидает пока тег станет false (pause-aware).
    /// </summary>
    public Task WaitForFalseAsync(string nodeId, PauseTokenSource? pauseGate, TimeSpan? timeout = null, CancellationToken ct = default)
        => WaitForValueAsync<bool>(nodeId, v => !v, pauseGate, timeout, ct);

    #endregion

    #region WaitGroup Factory

    /// <summary>
    /// Создаёт билдер для ожидания нескольких условий (без результата).
    /// </summary>
    public WaitGroupBuilder CreateWaitGroup() => new();

    /// <summary>
    /// Создаёт билдер для ожидания нескольких условий (с результатом).
    /// </summary>
    public WaitGroupBuilder<TResult> CreateWaitGroup<TResult>() => new();

    #endregion

    #region WaitAnyAsync (non-generic)

    /// <summary>
    /// Ожидает первое сработавшее условие из группы.
    /// </summary>
    public Task<TagWaitResult> WaitAnyAsync(WaitGroupBuilder builder, CancellationToken ct = default)
        => WaitAnyAsync(builder, pauseGate: null, ct);

    /// <summary>
    /// Ожидает первое сработавшее условие из группы (pause-aware).
    /// </summary>
    public async Task<TagWaitResult> WaitAnyAsync(
        WaitGroupBuilder builder,
        PauseTokenSource? pauseGate,
        CancellationToken ct = default)
    {
        if (builder.Conditions.Count == 0)
        {
            throw new ArgumentException("WaitGroup должен содержать хотя бы одно условие", nameof(builder));
        }

        var genericResult = await WaitAnyAsync(builder.ToGeneric(), pauseGate, ct);

        var callback = builder.Callbacks[genericResult.WinnerIndex];
        if (callback != null)
        {
            await callback(genericResult.RawValue);
        }

        return new TagWaitResult
        {
            WinnerIndex = genericResult.WinnerIndex,
            NodeId = genericResult.NodeId,
            Value = genericResult.RawValue,
            Name = genericResult.Name
        };
    }

    #endregion

    #region WaitAnyAsync (generic)

    /// <summary>
    /// Ожидает первое сработавшее условие из группы с типизированным результатом.
    /// </summary>
    public Task<TagWaitResult<TResult>> WaitAnyAsync<TResult>(
        WaitGroupBuilder<TResult> builder,
        CancellationToken ct = default)
        => WaitAnyAsync(builder, pauseGate: null, ct);

    /// <summary>
    /// Ожидает первое сработавшее условие из группы с типизированным результатом (pause-aware).
    /// </summary>
    public async Task<TagWaitResult<TResult>> WaitAnyAsync<TResult>(
        WaitGroupBuilder<TResult> builder,
        PauseTokenSource? pauseGate,
        CancellationToken ct = default)
    {
        if (builder.Conditions.Count == 0)
        {
            throw new ArgumentException("WaitGroup должен содержать хотя бы одно условие", nameof(builder));
        }

        var earlyResult = CheckCurrentValues(builder, pauseGate);
        if (earlyResult != null)
        {
            return earlyResult;
        }

        var tcs = new TaskCompletionSource<TagWaitResult<TResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlers = CreateHandlers(builder, tcs, pauseGate);

        Action? onResumed = null;
        try
        {
            await SubscribeAllAsync(builder.Conditions, handlers, ct);

            if (pauseGate != null)
            {
                onResumed = () => RecheckAfterSubscribe(builder, tcs, pauseGate);
                pauseGate.OnResumed += onResumed;
            }

            RecheckAfterSubscribe(builder, tcs, pauseGate);
            return await WaitWithTimeoutAsync(tcs.Task, builder.Timeout, pauseGate, ct);
        }
        finally
        {
            if (onResumed != null)
            {
                pauseGate!.OnResumed -= onResumed;
            }
            await UnsubscribeAllAsync(builder.Conditions, handlers);
        }
    }

    #endregion

    #region Private Helpers

    private TagWaitResult<TResult>? CheckCurrentValues<TResult>(
        WaitGroupBuilder<TResult> builder,
        PauseTokenSource? pauseGate)
    {
        if (pauseGate?.IsPaused == true)
        {
            return null;
        }

        for (var i = 0; i < builder.Conditions.Count; i++)
        {
            var condition = builder.Conditions[i];
            var current = subscription.GetValue(condition.NodeId);

            // ОТЛАДКА: Логируем что видит CheckCurrentValues
            logger.LogWarning(">>> CheckCurrentValues: {NodeId} = {Value}",
                condition.NodeId, current);

            if (current == null || !condition.Condition(current))
            {
                continue;
            }
            if (!CheckAdditionalTags(condition))
            {
                continue;
            }
            var result = builder.ResultCallbacks[i](current);
            logger.LogInformation("WaitGroup: условие [{Index}] {Name} уже выполнено, тег {NodeId} = {Value}",
                i, condition.Name ?? "unnamed", condition.NodeId, current);
            testLogger.LogInformation("  Сигнал уже активен: {Name} → {Result}",
                condition.Name ?? condition.NodeId, result);
            return new TagWaitResult<TResult>
            {
                WinnerIndex = i,
                NodeId = condition.NodeId,
                RawValue = current,
                Result = result,
                Name = condition.Name
            };
        }
        return null;
    }

    private bool CheckAdditionalTags(TagWaitCondition condition)
    {
        if (condition.AdditionalNodeIds == null)
        {
            return true;
        }
        foreach (var additionalNodeId in condition.AdditionalNodeIds)
        {
            var value = subscription.GetValue<bool>(additionalNodeId);
            if (value != true)
            {
                return false;
            }
        }
        return true;
    }

    private void RecheckAfterSubscribe<TResult>(
        WaitGroupBuilder<TResult> builder,
        TaskCompletionSource<TagWaitResult<TResult>> tcs,
        PauseTokenSource? pauseGate)
    {
        if (pauseGate?.IsPaused == true)
        {
            return;
        }
        var lateResult = CheckCurrentValues(builder, pauseGate);
        if (lateResult != null)
        {
            tcs.TrySetResult(lateResult);
        }
    }

    private void RecheckValue<T>(
        string nodeId,
        Func<T, bool> condition,
        TaskCompletionSource<T> tcs,
        PauseTokenSource? pauseGate)
    {
        if (pauseGate?.IsPaused == true)
        {
            return;
        }
        var current = subscription.GetValue<T>(nodeId);
        if (current != null && condition(current))
        {
            tcs.TrySetResult(current);
        }
    }

    private List<Func<object?, Task>> CreateHandlers<TResult>(
        WaitGroupBuilder<TResult> builder,
        TaskCompletionSource<TagWaitResult<TResult>> tcs,
        PauseTokenSource? pauseGate)
    {
        var handlers = new List<Func<object?, Task>>();
        for (var i = 0; i < builder.Conditions.Count; i++)
        {
            var condition = builder.Conditions[i];
            var resultCallback = builder.ResultCallbacks[i];
            handlers.Add(CreateHandler(tcs, condition, i, resultCallback, pauseGate));
        }
        return handlers;
    }

    private Func<object?, Task> CreateHandler<TResult>(
        TaskCompletionSource<TagWaitResult<TResult>> tcs,
        TagWaitCondition condition,
        int index,
        Func<object?, TResult> resultCallback,
        PauseTokenSource? pauseGate)
    {
        return value =>
        {
            if (pauseGate?.IsPaused == true)
            {
                return Task.CompletedTask;
            }
            if (!condition.Condition(value))
            {
                return Task.CompletedTask;
            }
            if (!CheckAdditionalTags(condition))
            {
                return Task.CompletedTask;
            }
            var result = resultCallback(value);
            logger.LogInformation("WaitGroup: условие [{Index}] {Name} сработало, тег {NodeId} = {Value}, результат = {Result}",
                index, condition.Name ?? "unnamed", condition.NodeId, value, result);
            testLogger.LogInformation("  Получен сигнал: {Name} → {Result}",
                condition.Name ?? condition.NodeId, result);
            tcs.TrySetResult(new TagWaitResult<TResult>
            {
                WinnerIndex = index,
                NodeId = condition.NodeId,
                RawValue = value,
                Result = result,
                Name = condition.Name
            });
            return Task.CompletedTask;
        };
    }

    private async Task SubscribeAllAsync(
        IReadOnlyList<TagWaitCondition> conditions,
        List<Func<object?, Task>> handlers,
        CancellationToken ct)
    {
        for (var i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            await subscription.SubscribeAsync(condition.NodeId, handlers[i], ct);
            if (condition.AdditionalNodeIds is { } additionalNodeIds)
            {
                foreach (var additionalNodeId in additionalNodeIds)
                {
                    await subscription.SubscribeAsync(additionalNodeId, handlers[i], ct);
                }
            }
        }
    }

    private async Task UnsubscribeAllAsync(
        IReadOnlyList<TagWaitCondition> conditions,
        List<Func<object?, Task>> handlers)
    {
        for (var i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            await subscription.UnsubscribeAsync(
                condition.NodeId,
                handlers[i],
                removeTag: false,
                ct: CancellationToken.None);
            if (condition.AdditionalNodeIds is { } additionalNodeIds)
            {
                foreach (var additionalNodeId in additionalNodeIds)
                {
                    await subscription.UnsubscribeAsync(
                        additionalNodeId,
                        handlers[i],
                        removeTag: false,
                        ct: CancellationToken.None);
                }
            }
        }
    }

    #endregion

    #region Pause-Aware Timeout

    private static async Task<T> WaitWithTimeoutAsync<T>(
        Task<T> task,
        TimeSpan? timeout,
        PauseTokenSource? pauseGate,
        CancellationToken ct)
    {
        if (!timeout.HasValue)
        {
            return await task.WaitAsync(ct);
        }
        if (pauseGate == null)
        {
            return await task.WaitAsync(timeout.Value, ct);
        }

        var remaining = timeout.Value;
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            if (task.IsCompleted)
            {
                return await task;
            }

            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException();
            }

            if (pauseGate.IsPaused)
            {
                await pauseGate.WaitWhilePausedAsync(ct);
                stopwatch.Restart();
                continue;
            }

            using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var pauseWatcher = new PauseWatcher(pauseGate);

            var delayTask = Task.Delay(remaining, delayCts.Token);
            var pauseTask = pauseWatcher.Task;

            var completed = await Task.WhenAny(task, delayTask, pauseTask);

            // Check timeout BEFORE cancelling (delayCts.IsCancellationRequested would be wrong after Cancel)
            var timedOut = completed == delayTask && delayTask.IsCompletedSuccessfully;
            var cancelled = completed == delayTask && delayTask.IsCanceled;

            // Cancel delay to free timer resources
            delayCts.Cancel();

            if (completed == task)
            {
                return await task;
            }
            if (cancelled)
            {
                ct.ThrowIfCancellationRequested();
            }
            if (timedOut)
            {
                throw new TimeoutException();
            }

            remaining -= stopwatch.Elapsed;
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            await pauseGate.WaitWhilePausedAsync(ct);
            stopwatch.Restart();
        }
    }

    /// <summary>
    /// Disposable helper that subscribes to OnPaused and unsubscribes on dispose.
    /// </summary>
    private sealed class PauseWatcher : IDisposable
    {
        private readonly PauseTokenSource _pauseGate;
        private readonly TaskCompletionSource _tcs;
        private readonly Action _handler;

        public PauseWatcher(PauseTokenSource pauseGate)
        {
            _pauseGate = pauseGate;
            _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _handler = () => _tcs.TrySetResult();

            pauseGate.OnPaused += _handler;

            // Double-check after subscribing
            if (pauseGate.IsPaused)
            {
                _tcs.TrySetResult();
            }
        }

        public Task Task => _tcs.Task;

        public void Dispose()
        {
            _pauseGate.OnPaused -= _handler;
        }
    }

    #endregion
}
