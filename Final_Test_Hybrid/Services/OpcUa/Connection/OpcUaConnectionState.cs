namespace Final_Test_Hybrid.Services.OpcUa.Connection;

public class OpcUaConnectionState
{
    private readonly Lock _lock = new();
    private TaskCompletionSource? _connectionTcs;
    private volatile bool _isConnected;
    public bool IsConnected => _isConnected;
    public event Action<bool>? ConnectionStateChanged;

    public void SetConnected(bool connected)
    {
        Action<bool>? handler;
        lock (_lock)
        {
            if (_isConnected == connected)
            {
                return;
            }
            _isConnected = connected;
            if (connected)
            {
                _connectionTcs?.TrySetResult();
                _connectionTcs = null;
            }
            handler = ConnectionStateChanged;
        }

        handler?.Invoke(connected);
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
}
