using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public interface IOpcUaConnectionService : IAsyncDisposable
{
    bool IsConnected { get; }
    event Action<bool>? ConnectionChanged;
    event Action? SessionRecreated;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();

    /// <summary>
    /// Executes an action with the session under lock.
    /// Throws InvalidOperationException if not connected.
    /// </summary>
    Task ExecuteWithSessionAsync(Func<ISession, Task> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a function with the session under lock and returns the result.
    /// Throws InvalidOperationException if not connected.
    /// </summary>
    Task<T> ExecuteWithSessionAsync<T>(Func<ISession, Task<T>> action, CancellationToken cancellationToken = default);
}
