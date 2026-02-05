using System.Threading.Channels;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    private Channel<ExecutionEvent>? _eventChannel;
    private readonly List<Task> _pendingRetries = [];
    private readonly Lock _pendingRetriesLock = new();
    private readonly RetryState _retryState = new();

    private sealed class RetryState
    {
        private int _activeCount;

        public bool IsActive => Volatile.Read(ref _activeCount) > 0;

        public void MarkStarted()
        {
            Interlocked.Increment(ref _activeCount);
        }

        public void MarkCompleted()
        {
            Interlocked.Decrement(ref _activeCount);
        }

        public void Reset()
        {
            Interlocked.Exchange(ref _activeCount, 0);
        }
    }

    /// <summary>
    /// Запускает канал событий, если он еще не создан.
    /// </summary>
    private Channel<ExecutionEvent> StartEventChannel()
    {
        if (_eventChannel != null)
        {
            return _eventChannel;
        }
        var channel = Channel.CreateUnbounded<ExecutionEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _eventChannel = channel;
        return channel;
    }

    /// <summary>
    /// Публикует критическое событие с гарантированной доставкой.
    /// </summary>
    private Task PublishEventCritical(ExecutionEvent evt)
    {
        var channel = _eventChannel;
        return channel == null ? Task.CompletedTask : channel.Writer.WriteAsync(evt).AsTask();
    }

    /// <summary>
    /// Пытается опубликовать некритическое событие.
    /// </summary>
    private bool TryPublishEvent(ExecutionEvent evt)
    {
        var channel = _eventChannel;
        return channel != null && channel.Writer.TryWrite(evt);
    }

    /// <summary>
    /// Завершает канал событий для читателей.
    /// </summary>
    private void CompleteEventChannel()
    {
        var channel = Interlocked.Exchange(ref _eventChannel, null);
        channel?.Writer.TryComplete();
    }

    /// <summary>
    /// Отслеживает задачу повторения для корректного завершения.
    /// </summary>
    private void TrackRetryTask(Task retryTask)
    {
        lock (_pendingRetriesLock)
        {
            _pendingRetries.Add(retryTask);
        }
        _ = retryTask.ContinueWith(
            _ => UntrackRetryTask(retryTask),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Удаляет завершённую задачу повтора из списка ожидания.
    /// </summary>
    private void UntrackRetryTask(Task retryTask)
    {
        lock (_pendingRetriesLock)
        {
            _pendingRetries.Remove(retryTask);
        }
    }

    /// <summary>
    /// Ожидает завершения всех отслеживаемых задач повторения.
    /// </summary>
    private Task AwaitPendingRetriesAsync()
    {
        Task[] pending;
        lock (_pendingRetriesLock)
        {
            pending = _pendingRetries.ToArray();
        }
        return pending.Length == 0 ? Task.CompletedTask : Task.WhenAll(pending);
    }

    private bool HasPendingRetries()
    {
        lock (_pendingRetriesLock)
        {
            return _pendingRetries.Count > 0;
        }
    }
}
