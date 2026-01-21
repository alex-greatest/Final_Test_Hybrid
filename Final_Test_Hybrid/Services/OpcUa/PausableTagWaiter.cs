using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.OpcUa.WaitGroup;

namespace Final_Test_Hybrid.Services.OpcUa;

/// <summary>
/// Обёртка над TagWaiter, передающая PauseTokenSource во все методы.
/// При паузе события игнорируются и таймер замораживается.
/// При Resume — перепроверка условий, таймер продолжает с остатка.
/// </summary>
public class PausableTagWaiter(
    TagWaiter inner,
    PauseTokenSource pauseToken)
{
    /// <summary>
    /// Ожидает значение тега, удовлетворяющее условию (pause-aware).
    /// </summary>
    public Task<T> WaitForValueAsync<T>(
        string nodeId,
        Func<T, bool> condition,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
        => inner.WaitForValueAsync(nodeId, condition, pauseToken, timeout, ct);

    /// <summary>
    /// Ожидает любое изменение значения тега (pause-aware).
    /// </summary>
    public Task<T> WaitForChangeAsync<T>(
        string nodeId,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
        => inner.WaitForChangeAsync<T>(nodeId, pauseToken, timeout, ct);

    /// <summary>
    /// Ожидает пока тег станет true (pause-aware).
    /// </summary>
    public Task WaitForTrueAsync(string nodeId, TimeSpan? timeout = null, CancellationToken ct = default)
        => inner.WaitForTrueAsync(nodeId, pauseToken, timeout, ct);

    /// <summary>
    /// Ожидает пока тег станет false (pause-aware).
    /// </summary>
    public Task WaitForFalseAsync(string nodeId, TimeSpan? timeout = null, CancellationToken ct = default)
        => inner.WaitForFalseAsync(nodeId, pauseToken, timeout, ct);

    /// <summary>
    /// Создаёт билдер для ожидания нескольких условий (без результата).
    /// </summary>
    public WaitGroupBuilder CreateWaitGroup() => inner.CreateWaitGroup();

    /// <summary>
    /// Создаёт билдер для ожидания нескольких условий (с результатом).
    /// </summary>
    public WaitGroupBuilder<TResult> CreateWaitGroup<TResult>() => inner.CreateWaitGroup<TResult>();

    /// <summary>
    /// Ожидает первое сработавшее условие из группы (pause-aware).
    /// </summary>
    public Task<TagWaitResult> WaitAnyAsync(WaitGroupBuilder builder, CancellationToken ct = default)
        => inner.WaitAnyAsync(builder, pauseToken, ct);

    /// <summary>
    /// Ожидает первое сработавшее условие из группы с типизированным результатом (pause-aware).
    /// </summary>
    public Task<TagWaitResult<TResult>> WaitAnyAsync<TResult>(
        WaitGroupBuilder<TResult> builder,
        CancellationToken ct = default)
        => inner.WaitAnyAsync(builder, pauseToken, ct);
}
