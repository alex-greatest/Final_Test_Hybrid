using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Services.Common;

namespace Final_Test_Hybrid.Services.OpcUa;

public class PausableOpcUaTagService(
    OpcUaTagService inner,
    PauseTokenSource pauseToken)
{
    public async Task<ReadResult<T>> ReadAsync<T>(string nodeId, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadAsync<T>(nodeId, ct);
    }

    public async Task<WriteResult> WriteAsync<T>(string nodeId, T value, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.WriteAsync(nodeId, value, ct);
    }
}
