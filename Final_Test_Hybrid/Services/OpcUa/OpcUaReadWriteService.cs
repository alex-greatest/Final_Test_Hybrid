using Opc.Ua;

namespace Final_Test_Hybrid.Services.OpcUa
{
    public class OpcUaReadWriteService(IOpcUaConnectionService connection)
        : IOpcUaReadWriteService
    {
        public async Task<T?> ReadNodeAsync<T>(string nodeId)
        {
            if (!IsSessionConnected())
            {
                return default;
            }
            var node = new NodeId(nodeId);
            var session = connection.Session;
            var value = await session.ReadValueAsync(node);
            return (T?)value.Value;
        }

        public async Task WriteNodeAsync<T>(string nodeId, T value)
        {
            if (!IsSessionConnected())
            {
                return;
            }
            var node = new NodeId(nodeId);
            var writeValue = CreateWriteValue(node, value);
            var request = new WriteValueCollection { writeValue };
            var session = connection.Session;
            await session.WriteAsync(null, request, CancellationToken.None);
        }

        private bool IsSessionConnected()
        {
            var session = connection.Session;
            return session != null && session.Connected;
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
