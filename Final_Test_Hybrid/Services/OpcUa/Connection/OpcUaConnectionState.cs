namespace Final_Test_Hybrid.Services.OpcUa.Connection;

public class OpcUaConnectionState
{
    private TaskCompletionSource? _connectionTcs;
    public bool IsConnected { get; private set; }
    public event Action<bool>? ConnectionStateChanged;

    public void SetConnected(bool connected)
    {
        IsConnected = connected;
        if (connected)
        {
            _connectionTcs?.TrySetResult();
        }
        ConnectionStateChanged?.Invoke(connected);
    }

    public async Task WaitForConnectionAsync(CancellationToken ct = default)
    {
        if (IsConnected)
        {
            return;
        }
        _connectionTcs ??= new TaskCompletionSource();
        await _connectionTcs.Task.WaitAsync(ct).ConfigureAwait(false);
    }
}
