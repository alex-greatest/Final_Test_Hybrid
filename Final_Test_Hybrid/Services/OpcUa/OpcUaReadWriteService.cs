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
                logger.LogWarning("Cannot read node {NodeId}: Session not connected", nodeId);
                return default;
            }
            try
            {
                var node = new NodeId(nodeId);
                var value = await session.ReadValueAsync(node);
                return GetValueOrDefault<T>(nodeId, value);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to read node {NodeId}", nodeId);
                return default;
            }
        }

        private T? GetValueOrDefault<T>(string nodeId, DataValue value)
        {
            if (!StatusCode.IsBad(value.StatusCode))
            {
                return (T?)value.Value;
            }
            logger.LogWarning("Read node {NodeId} returned bad status {Status}", nodeId, value.StatusCode);
            return default;
        }

        public async Task WriteNodeAsync<T>(string nodeId, T value)
        {
            var session = connection.Session;
            if (session is not { Connected: true })
            {
                logger.LogWarning("Cannot write node {NodeId}: Session not connected", nodeId);
                return;
            }
            try
            {
                var node = new NodeId(nodeId);
                var writeValue = CreateWriteValue(node, value);
                var request = new WriteValueCollection { writeValue };
                var response = await session.WriteAsync(null, request, CancellationToken.None);
                LogWriteResult(nodeId, response.Results);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write node {NodeId}", nodeId);
            }
        }

        private void LogWriteResult(string nodeId, StatusCodeCollection? results)
        {
            if (results == null || results.Count == 0)
            {
                logger.LogWarning("Write node {NodeId}: empty results", nodeId);
                return;
            }
            LogBadStatusIfNeeded(nodeId, results[0]);
        }

        private void LogBadStatusIfNeeded(string nodeId, StatusCode status)
        {
            if (StatusCode.IsBad(status))
            {
                logger.LogWarning("Write node {NodeId} failed with status {Status}", nodeId, status);
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