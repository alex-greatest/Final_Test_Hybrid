using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa.Connection;

public class OpcUaConnectionState(ILogger<OpcUaConnectionState> logger)
{
    private readonly Lock _lock = new();
    private TaskCompletionSource? _connectionTcs;
    private volatile bool _isConnected;

    public bool IsConnected => _isConnected;

    public event Action<bool>? ConnectionStateChanged;

    public void SetConnected(bool connected, string source)
    {
        var handler = UpdateConnectionState(connected);
        if (handler == null)
        {
            return;
        }

        LogTransition(connected, source);
        InvokeConnectionChangedSafe(handler, connected);
    }

    public Task WaitForConnectionAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_isConnected)
            {
                return Task.CompletedTask;
            }

            _connectionTcs ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _connectionTcs.Task.WaitAsync(ct);
        }
    }

    private Action<bool>? UpdateConnectionState(bool connected)
    {
        lock (_lock)
        {
            if (_isConnected == connected)
            {
                return null;
            }

            _isConnected = connected;
            if (connected)
            {
                _connectionTcs?.TrySetResult();
                _connectionTcs = null;
            }

            return ConnectionStateChanged;
        }
    }

    private void LogTransition(bool connected, string source)
    {
        logger.LogInformation(
            "OPC UA connection {State}. Source={Source}",
            connected ? "connected" : "lost",
            source);
    }

    private void InvokeConnectionChangedSafe(Action<bool> handler, bool connected)
    {
        try
        {
            handler(connected);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка в обработчике ConnectionStateChanged. State={State}",
                connected);
        }
    }
}
