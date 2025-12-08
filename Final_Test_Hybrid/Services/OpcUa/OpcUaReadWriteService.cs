using Final_Test_Hybrid.Services.OpcUa.Interface;
using Microsoft.Extensions.Logging;
using Opc.Ua;

namespace Final_Test_Hybrid.Services.OpcUa
{
    public class OpcUaReadWriteService(
        IOpcUaConnectionService connection,
        ILogger<OpcUaReadWriteService> logger)
        : IOpcUaReadWriteService, IAsyncDisposable
    {
        private readonly SemaphoreSlim _sessionLock = new(1, 1);
        private bool _disposed;

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }
            _disposed = true;
            _sessionLock.Dispose();
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }

        public async Task<T?> ReadNodeAsync<T>(string nodeId, CancellationToken ct = default)
        {
            await _sessionLock.WaitAsync(ct);
            try
            {
                var session = connection.Session;
                if (session is not { Connected: true })
                {
                    logger.LogWarning("Cannot read node {NodeId}: Session not connected", nodeId);
                    return default;
                }
                var node = new NodeId(nodeId);
                var value = await session.ReadValueAsync(node, ct);
                return GetValueOrDefault<T>(nodeId, value);
            }
            catch (OperationCanceledException)
            {
                return default;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to read node {NodeId}", nodeId);
                return default;
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private T? GetValueOrDefault<T>(string nodeId, DataValue value)
        {
            if (!StatusCode.IsBad(value.StatusCode))
            {
                return value.Value is T typedValue
                    ? typedValue
                    : LogAndReturnDefault<T>(nodeId);
            }
            logger.LogWarning("Read node {NodeId} returned bad status {Status}", nodeId, value.StatusCode);
            return default;
        }

        private T? LogAndReturnDefault<T>(string nodeId)
        {
            logger.LogWarning("Cannot cast value from node {NodeId} to type {Type}", nodeId, typeof(T).Name);
            return default;
        }

        public async Task WriteNodeAsync<T>(string nodeId, T value, CancellationToken ct = default)
        {
            await _sessionLock.WaitAsync(ct);
            try
            {
                var session = connection.Session;
                if (session is not { Connected: true })
                {
                    logger.LogWarning("Cannot write node {NodeId}: Session not connected", nodeId);
                    return;
                }
                var node = new NodeId(nodeId);
                var writeValue = CreateWriteValue(node, value);
                var request = new WriteValueCollection { writeValue };
                var response = await session.WriteAsync(null, request, ct);
                LogWriteResult(nodeId, response.Results);
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write node {NodeId}", nodeId);
            }
            finally
            {
                _sessionLock.Release();
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