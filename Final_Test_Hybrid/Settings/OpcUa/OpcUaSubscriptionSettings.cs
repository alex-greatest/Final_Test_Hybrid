namespace Final_Test_Hybrid.Settings.OpcUa;

public class OpcUaSubscriptionSettings
{
    public int PublishingIntervalMs { get; set; }
    public int SamplingIntervalMs { get; set; }
    public int QueueSize { get; set; }
    public uint MaxNotificationsPerPublish { get; set; }
    public uint KeepAliveCount { get; set; } = 10;
    public uint LifetimeCount { get; set; } = 100;

    public void Validate()
    {
        ValidatePublishingInterval();
        ValidateSamplingInterval();
        ValidateQueueSize();
        ValidateMaxNotifications();
    }

    private void ValidatePublishingInterval()
    {
        if (PublishingIntervalMs is < 100 or > 10000)
        {
            throw new InvalidOperationException($"OpcUa:Subscription:PublishingIntervalMs должен быть 100-10000 мс (получено: {PublishingIntervalMs})");
        }
    }

    private void ValidateSamplingInterval()
    {
        if (SamplingIntervalMs is < 50 or > 5000)
        {
            throw new InvalidOperationException($"OpcUa:Subscription:SamplingIntervalMs должен быть 50-5000 мс (получено: {SamplingIntervalMs})");
        }
    }

    private void ValidateQueueSize()
    {
        if (QueueSize is < 1 or > 100)
        {
            throw new InvalidOperationException($"OpcUa:Subscription:QueueSize должен быть 1-100 (получено: {QueueSize})");
        }
    }

    private void ValidateMaxNotifications()
    {
        if (MaxNotificationsPerPublish is 0 or > 10000)
        {
            throw new InvalidOperationException($"OpcUa:Subscription:MaxNotificationsPerPublish должен быть 1-10000 (получено: {MaxNotificationsPerPublish})");
        }
    }
}
