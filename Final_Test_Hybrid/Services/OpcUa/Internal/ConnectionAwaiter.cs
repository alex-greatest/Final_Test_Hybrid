using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa.Internal;

internal sealed class ConnectionAwaiter(
    OpcUaConnectionService connectionService,
    ILogger<ConnectionAwaiter> logger)
{
    private TaskCompletionSource<bool>? _tcs;

    public async Task WaitAsync(CancellationToken ct)
    {
        if (connectionService.IsConnected)
        {
            return;
        }
        await WaitForConnectionSignalAsync(ct).ConfigureAwait(false);
    }

    private async Task WaitForConnectionSignalAsync(CancellationToken ct)
    {
        _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connectionService.ConnectionStateChanged += OnConnected;
        try
        {
            logger.LogInformation("Ожидание подключения к OPC UA серверу...");
            await _tcs.Task.WaitAsync(ct).ConfigureAwait(false);
            logger.LogInformation("Подключение установлено");
        }
        finally
        {
            connectionService.ConnectionStateChanged -= OnConnected;
            _tcs = null;
        }
    }

    private void OnConnected(bool connected)
    {
        if (connected)
        {
            _tcs?.TrySetResult(true);
        }
    }
}
