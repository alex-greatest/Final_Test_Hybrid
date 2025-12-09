using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public interface IOpcUaConnectionService : IAsyncDisposable
{
    bool IsConnected { get; }
    ISession? Session { get; }
    event Action<bool>? ConnectionChanged;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}
