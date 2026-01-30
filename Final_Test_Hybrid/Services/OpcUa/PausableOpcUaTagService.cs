using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Services.Common;

namespace Final_Test_Hybrid.Services.OpcUa;

/// <summary>
/// Обёртка над OpcUaTagService с поддержкой паузы.
/// </summary>
public class PausableOpcUaTagService(
    OpcUaTagService inner,
    PauseTokenSource pauseToken)
{
    /// <summary>
    /// Читает значение тега с учётом паузы.
    /// </summary>
    public async Task<ReadResult<T>> ReadAsync<T>(string nodeId, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadAsync<T>(nodeId, ct);
    }

    /// <summary>
    /// Записывает значение тега с учётом паузы.
    /// </summary>
    public async Task<WriteResult> WriteAsync<T>(string nodeId, T value, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.WriteAsync(nodeId, value, ct);
    }

    /// <summary>
    /// Записывает несколько тегов одним запросом (батчинг) с учётом паузы.
    /// </summary>
    /// <param name="items">Список пар (nodeId, value) для записи.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Список результатов записи для каждого тега.</returns>
    public async Task<List<WriteResult>> WriteBatchAsync(
        IReadOnlyList<(string nodeId, object value)> items,
        CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.WriteBatchAsync(items, ct);
    }
}
