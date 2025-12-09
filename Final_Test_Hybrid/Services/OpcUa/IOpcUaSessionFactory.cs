using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

/// <summary>
/// Factory for creating configured OPC UA sessions.
/// Encapsulates ApplicationConfiguration and endpoint setup.
/// </summary>
public interface IOpcUaSessionFactory
{
    Task<ISession> CreateAsync(OpcUaSettings settings, CancellationToken cancellationToken = default);
}
