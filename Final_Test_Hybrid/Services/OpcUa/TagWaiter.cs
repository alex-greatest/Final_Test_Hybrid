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

        Task OnValueChanged(object? value)
        {
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

        Task OnValueChanged(object? value)
        {
            if (value is T typed)
            {
                tcs.TrySetResult(typed);
            }
            return Task.CompletedTask;
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

    public WaitGroupBuilder CreateWaitGroup() => new();

    public WaitGroupBuilder<TResult> CreateWaitGroup<TResult>() => new();

    public async Task<TagWaitResult> WaitAnyAsync(WaitGroupBuilder builder, CancellationToken ct = default)
    {
        if (builder.Conditions.Count == 0)
        {
            throw new ArgumentException("WaitGroup должен содержать хотя бы одно условие", nameof(builder));
        }

        var genericResult = await WaitAnyAsync(builder.ToGeneric(), ct);

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

    public async Task<TagWaitResult<TResult>> WaitAnyAsync<TResult>(
        WaitGroupBuilder<TResult> builder,
        CancellationToken ct = default)
    {
        if (builder.Conditions.Count == 0)
        {
            throw new ArgumentException("WaitGroup должен содержать хотя бы одно условие", nameof(builder));
        }
        var earlyResult = CheckCurrentValues(builder);
        if (earlyResult != null)
        {
            return earlyResult;
        }
        var tcs = new TaskCompletionSource<TagWaitResult<TResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlers = CreateHandlers(builder, tcs);
        try
        {
            await SubscribeAllAsync(builder.Conditions, handlers, ct);
            RecheckAfterSubscribe(builder, tcs);
            return await WaitWithTimeoutAsync(tcs.Task, builder.Timeout, ct);
        }
        finally
        {
            await UnsubscribeAllAsync(builder.Conditions, handlers);
        }
    }

    private TagWaitResult<TResult>? CheckCurrentValues<TResult>(WaitGroupBuilder<TResult> builder)
    {
        for (var i = 0; i < builder.Conditions.Count; i++)
        {
            var condition = builder.Conditions[i];
            var current = subscription.GetValue(condition.NodeId);
            if (current == null || !condition.Condition(current))
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

    private void RecheckAfterSubscribe<TResult>(
        WaitGroupBuilder<TResult> builder,
        TaskCompletionSource<TagWaitResult<TResult>> tcs)
    {
        var lateResult = CheckCurrentValues(builder);
        if (lateResult != null)
        {
            tcs.TrySetResult(lateResult);
        }
    }

    private List<Func<object?, Task>> CreateHandlers<TResult>(
        WaitGroupBuilder<TResult> builder,
        TaskCompletionSource<TagWaitResult<TResult>> tcs)
    {
        var handlers = new List<Func<object?, Task>>();
        for (var i = 0; i < builder.Conditions.Count; i++)
        {
            var condition = builder.Conditions[i];
            var resultCallback = builder.ResultCallbacks[i];
            handlers.Add(CreateHandler(tcs, condition, i, resultCallback));
        }
        return handlers;
    }

    private Func<object?, Task> CreateHandler<TResult>(
        TaskCompletionSource<TagWaitResult<TResult>> tcs,
        TagWaitCondition condition,
        int index,
        Func<object?, TResult> resultCallback)
    {
        return value =>
        {
            if (!condition.Condition(value))
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
            await subscription.SubscribeAsync(conditions[i].NodeId, handlers[i], ct);
        }
    }

    private async Task UnsubscribeAllAsync(
        IReadOnlyList<TagWaitCondition> conditions,
        List<Func<object?, Task>> handlers)
    {
        for (var i = 0; i < conditions.Count; i++)
        {
            await subscription.UnsubscribeAsync(
                conditions[i].NodeId,
                handlers[i],
                removeTag: false,
                ct: CancellationToken.None);
        }
    }

    private static async Task<T> WaitWithTimeoutAsync<T>(Task<T> task, TimeSpan? timeout, CancellationToken ct)
    {
        return timeout.HasValue
            ? await task.WaitAsync(timeout.Value, ct)
            : await task.WaitAsync(ct);
    }
}
