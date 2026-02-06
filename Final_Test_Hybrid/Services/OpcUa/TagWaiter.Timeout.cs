using System.Diagnostics;
using Final_Test_Hybrid.Services.Common;

namespace Final_Test_Hybrid.Services.OpcUa;

public partial class TagWaiter
{
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
            var timedOut = completed == delayTask && delayTask.IsCompletedSuccessfully;
            var cancelled = completed == delayTask && delayTask.IsCanceled;

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
