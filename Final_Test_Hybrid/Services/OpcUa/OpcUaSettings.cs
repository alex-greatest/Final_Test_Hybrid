namespace Final_Test_Hybrid.Services.OpcUa;

public sealed class OpcUaSettings
{
    public string EndpointUrl { get; init; } = "opc.tcp://localhost:4840";
    public string ApplicationName { get; init; } = "OpcUaClient";
    public int ReconnectIntervalMs { get; init; } = 5000;
    public int SessionTimeoutMs { get; init; } = 60000;
    public int MaxStringLength { get; init; } = 1048576;
    public int MaxByteStringLength { get; init; } = 1048576;
    public int MaxArrayLength { get; init; } = 65535;
    public int MaxMessageSize { get; init; } = 4194304;
    public int MaxBufferSize { get; init; } = 65535;
    public int ChannelLifetimeMs { get; init; } = 300000;
    public int SecurityTokenLifetimeMs { get; init; } = 3600000;
}
