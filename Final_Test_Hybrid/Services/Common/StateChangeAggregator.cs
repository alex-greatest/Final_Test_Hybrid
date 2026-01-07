namespace Final_Test_Hybrid.Services.Common;

/// <summary>
/// Агрегатор событий изменения состояния.
/// Позволяет подписаться на несколько INotifyStateChanged источников
/// и получать единое событие при изменении любого из них.
/// </summary>
public sealed class StateChangeAggregator : IDisposable
{
    private readonly List<(INotifyStateChanged Source, Action Handler)> _subscriptions = [];
    private readonly Lock _lock = new();
    private bool _disposed;

    public event Action? OnAnyStateChanged;

    public void Subscribe(INotifyStateChanged source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            void Handler() => OnAnyStateChanged?.Invoke();
            source.OnStateChanged += Handler;
            _subscriptions.Add((source, Handler));
        }
    }

    public void Subscribe(params INotifyStateChanged[] sources)
    {
        foreach (var source in sources)
        {
            Subscribe(source);
        }
    }

    public void Unsubscribe(INotifyStateChanged source)
    {
        lock (_lock)
        {
            var subscription = _subscriptions.FirstOrDefault(s => s.Source == source);
            if (subscription.Source == null)
            {
                return;
            }
            subscription.Source.OnStateChanged -= subscription.Handler;
            _subscriptions.Remove(subscription);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        lock (_lock)
        {
            foreach (var (source, handler) in _subscriptions)
            {
                source.OnStateChanged -= handler;
            }
            _subscriptions.Clear();
        }
    }
}
