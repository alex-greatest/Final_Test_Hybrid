namespace Final_Test_Hybrid.Services.OpcUa
{
    public interface IOpcUaSubscriptionService : IAsyncDisposable
    {
        Task SubscribeAsync(string nodeId, Action<object?> callback);
        Task UnsubscribeAsync(string nodeId);
    }
}
