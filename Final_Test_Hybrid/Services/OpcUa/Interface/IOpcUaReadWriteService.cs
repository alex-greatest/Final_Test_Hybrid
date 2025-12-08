namespace Final_Test_Hybrid.Services.OpcUa.Interface
{
    public interface IOpcUaReadWriteService
    {
        Task<T?> ReadNodeAsync<T>(string nodeId, CancellationToken ct = default);
        Task WriteNodeAsync<T>(string nodeId, T value, CancellationToken ct = default);
    }
}
