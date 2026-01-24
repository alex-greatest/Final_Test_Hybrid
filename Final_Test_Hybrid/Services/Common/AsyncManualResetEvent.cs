namespace Final_Test_Hybrid.Services.Common;

/// <summary>
/// Асинхронный аналог ManualResetEvent.
/// Позволяет ожидать сигнала в асинхронном контексте.
/// </summary>
public sealed class AsyncManualResetEvent
{
    private volatile TaskCompletionSource<bool> _tcs;

    /// <summary>
    /// Создаёт новый AsyncManualResetEvent.
    /// </summary>
    /// <param name="initialState">Начальное состояние: true = открыт (сигнал есть), false = закрыт.</param>
    public AsyncManualResetEvent(bool initialState)
    {
        _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (initialState)
        {
            _tcs.TrySetResult(true);
        }
    }

    /// <summary>
    /// Ожидает сигнала (открытия gate).
    /// </summary>
    public Task WaitAsync(CancellationToken ct = default)
    {
        var tcs = _tcs;
        if (tcs.Task.IsCompleted)
        {
            return Task.CompletedTask;
        }

        return ct.CanBeCanceled
            ? WaitWithCancellationAsync(tcs.Task, ct)
            : tcs.Task;
    }

    /// <summary>
    /// Открывает gate (устанавливает сигнал).
    /// </summary>
    public void Set()
    {
        _tcs.TrySetResult(true);
    }

    /// <summary>
    /// Закрывает gate (сбрасывает сигнал).
    /// </summary>
    public void Reset()
    {
        var currentTcs = _tcs;
        if (!currentTcs.Task.IsCompleted)
        {
            return;
        }

        Interlocked.CompareExchange(
            ref _tcs,
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            currentTcs);
    }

    /// <summary>
    /// Ожидание с поддержкой отмены.
    /// </summary>
    private static async Task WaitWithCancellationAsync(Task waitTask, CancellationToken ct)
    {
        var cancelTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (ct.Register(() => cancelTcs.TrySetCanceled(ct)))
        {
            var completed = await Task.WhenAny(waitTask, cancelTcs.Task);
            if (completed == cancelTcs.Task)
            {
                ct.ThrowIfCancellationRequested();
            }
        }
    }
}
