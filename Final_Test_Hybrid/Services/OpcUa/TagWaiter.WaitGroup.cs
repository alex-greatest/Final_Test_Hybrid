using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.OpcUa.WaitGroup;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa;

public partial class TagWaiter
{
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
            await SubscribeAllWithReconnectRetryAsync(builder.Conditions, handlers, ct);
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
            if (condition.AdditionalNodeIds != null)
            {
                if (!CheckAllConditionTags(condition))
                {
                    continue;
                }
            }
            else
            {
                if (current == null || !condition.Condition(current))
                {
                    continue;
                }
            }

            var result = builder.ResultCallbacks[i](current);
            logger.LogInformation(
                "WaitGroup: условие [{Index}] {Name} уже выполнено, тег {NodeId} = {Value}",
                i,
                condition.Name ?? "unnamed",
                condition.NodeId,
                current);
            testLogger.LogInformation(
                "  Сигнал уже активен: {Name} → {Result}",
                condition.Name ?? condition.NodeId,
                result);
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

    private bool CheckAllConditionTags(TagWaitCondition condition)
    {
        var mainValue = subscription.GetValue<bool>(condition.NodeId);
        if (mainValue != true)
        {
            return false;
        }
        return CheckAdditionalTags(condition);
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

    #endregion
}
