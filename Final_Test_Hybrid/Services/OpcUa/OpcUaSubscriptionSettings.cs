namespace Final_Test_Hybrid.Services.OpcUa;

public sealed class OpcUaSubscriptionSettings
{
    public int PublishingIntervalMs { get; init; } = 500;
    public int SamplingIntervalMs { get; init; } = 250;
    public uint QueueSize { get; init; } = 10;
}
