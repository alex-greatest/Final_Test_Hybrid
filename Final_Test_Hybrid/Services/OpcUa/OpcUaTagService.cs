using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

/// <summary>
/// Сервис для чтения и записи OPC-UA тегов.
/// </summary>
public class OpcUaTagService(
    OpcUaConnectionService connectionService,
    DualLogger<OpcUaTagService> logger)
{
    private ISession Session => connectionService.Session
        ?? throw new InvalidOperationException("Нет подключения к OPC UA серверу");

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

    public async Task<WriteResult> WriteAsync<T>(string nodeId, T value, CancellationToken ct = default, bool silent = false)
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

    /// <summary>
    /// Записывает несколько тегов одним запросом (батчинг).
    /// </summary>
    /// <param name="items">Список пар (nodeId, value) для записи.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Список результатов записи для каждого тега.</returns>
    public async Task<List<WriteResult>> WriteBatchAsync(
        IReadOnlyList<(string nodeId, object value)> items,
        CancellationToken ct = default)
    {
        if (items.Count == 0)
        {
            return [];
        }

        try
        {
            var nodesToWrite = new WriteValueCollection();
            foreach (var (nodeId, value) in items)
            {
                nodesToWrite.Add(new WriteValue
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
                });
            }

            var response = await Session.WriteAsync(null, nodesToWrite, ct);

            if (response.Results.Count != items.Count)
            {
                logger.LogError("Несоответствие количества результатов: ожидалось {Expected}, получено {Actual}",
                    items.Count, response.Results.Count);
                return items.Select(i => new WriteResult(i.nodeId, "Ошибка: несоответствие количества результатов")).ToList();
            }

            return items.Zip(response.Results, (item, status) =>
                new WriteResult(
                    item.nodeId,
                    StatusCode.IsBad(status) ? OpcUaErrorMapper.ToHumanReadable(status) : null
                )).ToList();
        }
        catch (ServiceResultException ex)
        {
            logger.LogError(ex, "Ошибка батч-записи {Count} тегов", items.Count);
            return items.Select(i => new WriteResult(i.nodeId, OpcUaErrorMapper.ToHumanReadable(ex.StatusCode))).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка батч-записи {Count} тегов", items.Count);
            return items.Select(i => new WriteResult(i.nodeId, $"Ошибка записи: {ex.Message}")).ToList();
        }
    }
}
