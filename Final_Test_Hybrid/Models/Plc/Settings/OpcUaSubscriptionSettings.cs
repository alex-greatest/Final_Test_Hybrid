namespace Final_Test_Hybrid.Models.Plc.Settings;

public class OpcUaSubscriptionSettings
{
    public int PublishingIntervalMs { get; set; }
    public int SamplingIntervalMs { get; set; }
    public int QueueSize { get; set; }

    public void Validate()
    {
        if (PublishingIntervalMs < 100 || PublishingIntervalMs > 10000)
        {
            throw new InvalidOperationException($"OpcUa:Subscription:PublishingIntervalMs должен быть 100-10000 мс (получено: {PublishingIntervalMs})");
        }
        if (SamplingIntervalMs < 50 || SamplingIntervalMs > 5000)
        {
            throw new InvalidOperationException($"OpcUa:Subscription:SamplingIntervalMs должен быть 50-5000 мс (получено: {SamplingIntervalMs})");
        }
        if (QueueSize < 1 || QueueSize > 100)
        {
            throw new InvalidOperationException($"OpcUa:Subscription:QueueSize должен быть 1-100 (получено: {QueueSize})");
        }
    }
}
