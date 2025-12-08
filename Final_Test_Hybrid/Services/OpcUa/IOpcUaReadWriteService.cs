namespace Final_Test_Hybrid.Services.OpcUa
{
    public interface IOpcUaReadWriteService
    {
        Task<T?> ReadNodeAsync<T>(string nodeId);
        Task WriteNodeAsync<T>(string nodeId, T value);
    }
}
