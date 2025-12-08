using Microsoft.Extensions.Logging;
using Opc.Ua;

namespace Final_Test_Hybrid.Services.OpcUa
{
    public class OpcUaReadWriteService(
        IOpcUaConnectionService connection,
        ILogger<OpcUaReadWriteService> logger)
        : IOpcUaReadWriteService
    {
        public async Task<T?> ReadNodeAsync<T>(string nodeId)
        {
            var session = connection.Session;
            if (session is not { Connected: true })
            {
                return default;
            }
            try
            {
                var node = new NodeId(nodeId);
                var value = await session.ReadValueAsync(node);
                return (T?)value.Value;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to read node {NodeId}", nodeId);
                return default;
            }
        }

        public async Task WriteNodeAsync<T>(string nodeId, T value)
        {
            var session = connection.Session;
            if (session is not { Connected: true })
            {
                return;
            }
            try
            {
                var node = new NodeId(nodeId);
                var writeValue = CreateWriteValue(node, value);
                var request = new WriteValueCollection { writeValue };
                await session.WriteAsync(null, request, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write node {NodeId}", nodeId);
            }
        }

        private WriteValue CreateWriteValue<T>(NodeId node, T value)
        {
            return new WriteValue
            {
                NodeId = node,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value))
            };
        }
    }
}
