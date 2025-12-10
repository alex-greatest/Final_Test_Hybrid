using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;
using Final_Test_Hybrid.Models.Plc;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa.Internal;

internal sealed class NotificationHub(ILogger<NotificationHub> logger) : IAsyncDisposable
{
    private readonly Channel<(string NodeId, OpcValue Value)> _channel = Channel.CreateUnbounded<(string, OpcValue)>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<string, OpcValue> _cache = new();
    private readonly ConcurrentDictionary<string, ImmutableList<Action<OpcValue>>> _subscribers = new();
    private CancellationTokenSource? _cts;
    private Task? _task;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _task = ProcessAsync(_cts.Token);
    }

    public void Enqueue(string nodeId, OpcValue value)
    {
        _channel.Writer.TryWrite((nodeId, value));
    }

    public void Subscribe(string nodeId, Action<OpcValue> callback)
    {
        _subscribers.AddOrUpdate(
            nodeId,
            _ => [callback],
            (_, list) => list.Add(callback));
    }

    public void Unsubscribe(string nodeId, Action<OpcValue> callback)
    {
        _subscribers.AddOrUpdate(
            nodeId,
            _ => [],
            (_, list) => list.Remove(callback));
    }

    public OpcValue? GetValue(string nodeId)
    {
        _cache.TryGetValue(nodeId, out var value);
        return value;
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        if (_cts != null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }
        if (_task != null)
        {
            await _task.ConfigureAwait(false);
        }
        _cts?.Dispose();
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var (nodeId, value) in _channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                ProcessSingle(nodeId, value);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Обработка уведомлений остановлена");
        }
    }

    private void ProcessSingle(string nodeId, OpcValue value)
    {
        try
        {
            _cache[nodeId] = value;
            NotifySubscribers(nodeId, value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обработки {NodeId}", nodeId);
        }
    }

    private void NotifySubscribers(string nodeId, OpcValue value)
    {
        if (!_subscribers.TryGetValue(nodeId, out var callbacks))
        {
            return;
        }
        foreach (var callback in callbacks)
        {
            InvokeCallback(callback, nodeId, value);
        }
    }

    private void InvokeCallback(Action<OpcValue> callback, string nodeId, OpcValue value)
    {
        try
        {
            callback(value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка в callback для {NodeId}", nodeId);
        }
    }

}
