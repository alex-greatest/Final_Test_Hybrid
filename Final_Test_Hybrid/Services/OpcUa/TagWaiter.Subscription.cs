using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.OpcUa.WaitGroup;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa;

public partial class TagWaiter
{
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
            if (condition.AdditionalNodeIds != null)
            {
                if (!CheckAllConditionTags(condition))
                {
                    return Task.CompletedTask;
                }
            }
            else
            {
                if (!condition.Condition(value))
                {
                    return Task.CompletedTask;
                }
            }

            var result = resultCallback(value);
            logger.LogInformation(
                "WaitGroup: условие [{Index}] {Name} сработало, тег {NodeId} = {Value}, результат = {Result}",
                index,
                condition.Name ?? "unnamed",
                condition.NodeId,
                value,
                result);
            testLogger.LogInformation(
                "  Получен сигнал: {Name} → {Result}",
                condition.Name ?? condition.NodeId,
                result);
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
            await SubscribeConditionAsync(conditions[i], handlers[i], ct);
        }
    }

    private async Task SubscribeConditionAsync(
        TagWaitCondition condition,
        Func<object?, Task> handler,
        CancellationToken ct)
    {
        await subscription.SubscribeAsync(condition.NodeId, handler, ct);
        if (condition.AdditionalNodeIds is not { } additionalNodeIds)
        {
            return;
        }
        await SubscribeAdditionalNodeIdsAsync(additionalNodeIds, handler, ct);
    }

    private async Task SubscribeAdditionalNodeIdsAsync(
        IReadOnlyList<string> additionalNodeIds,
        Func<object?, Task> handler,
        CancellationToken ct)
    {
        foreach (var additionalNodeId in additionalNodeIds)
        {
            await subscription.SubscribeAsync(additionalNodeId, handler, ct);
        }
    }

    private async Task SubscribeAllWithReconnectRetryAsync(
        IReadOnlyList<TagWaitCondition> conditions,
        List<Func<object?, Task>> handlers,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await SubscribeAllAsync(conditions, handlers, ct);
                return;
            }
            catch (Exception ex) when (ShouldRetrySubscribe(ex, attempt))
            {
                await TryUnsubscribeAllAsync(conditions, handlers);
                logger.LogWarning(
                    ex,
                    "Ошибка подписки WaitGroup во время reconnect. Попытка {Attempt}/{MaxAttempts}",
                    attempt,
                    SubscribeRetryAttempts);
                await connectionState.WaitForConnectionAsync(ct);
                await Task.Delay(SubscribeRetryDelay, ct);
            }
        }
    }

    private async Task SubscribeWithReconnectRetryAsync(
        string nodeId,
        Func<object?, Task> handler,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await subscription.SubscribeAsync(nodeId, handler, ct);
                return;
            }
            catch (Exception ex) when (ShouldRetrySubscribe(ex, attempt))
            {
                logger.LogWarning(
                    ex,
                    "Ошибка подписки на тег {NodeId} во время reconnect. Попытка {Attempt}/{MaxAttempts}",
                    nodeId,
                    attempt,
                    SubscribeRetryAttempts);
                await connectionState.WaitForConnectionAsync(ct);
                await Task.Delay(SubscribeRetryDelay, ct);
            }
        }
    }

    private static bool ShouldRetrySubscribe(Exception ex, int attempt)
    {
        return attempt < SubscribeRetryAttempts
            && OpcUaTransientErrorClassifier.IsTransientDisconnect(ex);
    }

    private async Task TryUnsubscribeAllAsync(
        IReadOnlyList<TagWaitCondition> conditions,
        List<Func<object?, Task>> handlers)
    {
        try
        {
            await UnsubscribeAllAsync(conditions, handlers);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Не удалось полностью отписать WaitGroup перед retry");
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

            if (condition.AdditionalNodeIds is not { } additionalNodeIds)
            {
                continue;
            }
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
