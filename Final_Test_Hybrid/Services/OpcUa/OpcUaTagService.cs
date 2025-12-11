using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public class OpcUaTagService(
    OpcUaConnectionService connectionService,
    ILogger<OpcUaTagService> logger)
{
    private ISession Session => connectionService.Session
        ?? throw new InvalidOperationException("Not connected to OPC UA server");

    public async Task<ReadResult<T>> ReadAsync<T>(string nodeId, CancellationToken ct = default)
    {
        try
        {
            var nodesToRead = new ReadValueIdCollection
            {
                new ReadValueId { NodeId = new NodeId(nodeId), AttributeId = Attributes.Value }
            };
            var response = await Session.ReadAsync(null, 0, TimestampsToReturn.Neither, nodesToRead, ct);
            var result = response.Results[0];
            if (StatusCode.IsBad(result.StatusCode))
            {
                return new ReadResult<T>(nodeId, default, OpcUaErrorMapper.ToHumanReadable(result.StatusCode));
            }
            var value = result.Value is T typed ? typed : default;
            return new ReadResult<T>(nodeId, value, null);
        }
        catch (ServiceResultException ex)
        {
            logger.LogError(ex, "Ошибка чтения тега {NodeId}", nodeId);
            return new ReadResult<T>(nodeId, default, OpcUaErrorMapper.ToHumanReadable(ex.StatusCode));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка чтения тега {NodeId}", nodeId);
            return new ReadResult<T>(nodeId, default, $"Ошибка чтения: {ex.Message}");
        }
    }

    public async Task<WriteResult> WriteAsync<T>(string nodeId, T value, CancellationToken ct = default)
    {
        try
        {
            var nodesToWrite = new WriteValueCollection
            {
                new WriteValue
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
                }
            };
            var response = await Session.WriteAsync(null, nodesToWrite, ct);
            var status = response.Results[0];
            var error = StatusCode.IsBad(status) ? OpcUaErrorMapper.ToHumanReadable(status) : null;
            return new WriteResult(nodeId, error);
        }
        catch (ServiceResultException ex)
        {
            logger.LogError(ex, "Ошибка записи тега {NodeId}", nodeId);
            return new WriteResult(nodeId, OpcUaErrorMapper.ToHumanReadable(ex.StatusCode));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка записи тега {NodeId}", nodeId);
            return new WriteResult(nodeId, $"Ошибка записи: {ex.Message}");
        }
    }
}
