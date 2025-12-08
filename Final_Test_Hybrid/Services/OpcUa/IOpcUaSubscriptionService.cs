namespace Final_Test_Hybrid.Services.OpcUa
{
    public interface IOpcUaSubscriptionService
    {
        Task SubscribeAsync(string nodeId, Action<object?> callback);
        Task UnsubscribeAsync(string nodeId);
    }
}
