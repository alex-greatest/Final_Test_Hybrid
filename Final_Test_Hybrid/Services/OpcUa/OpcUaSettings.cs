namespace Final_Test_Hybrid.Services.OpcUa;

public sealed class OpcUaSettings
{
    public string EndpointUrl { get; init; } = "opc.tcp://localhost:4840";
    public string ApplicationName { get; init; } = "OpcUaClient";
    public int ReconnectIntervalMs { get; init; } = 5000;
    public int SessionTimeoutMs { get; init; } = 60000;
}
