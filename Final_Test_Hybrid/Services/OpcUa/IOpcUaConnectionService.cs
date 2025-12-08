using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa
{
    public interface IOpcUaConnectionService : IAsyncDisposable
    {
        bool IsConnected { get; }
        Session? Session { get; }
        event EventHandler<bool>? ConnectionChanged;

        Task StartAsync(CancellationToken ct = default);
        Task StopAsync();
    }
}
