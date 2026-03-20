using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Plc;

/// <summary>
/// Защищает retry PLC-блока от stale сигналов Error/End в OPC cache.
/// </summary>
internal static class PlcRetrySignalFreshnessGuard
{
    public static async Task EnsureSignalsFreshAsync(
        IHasPlcBlockPath? step,
        OpcUaSubscription subscription,
        Func<string, TimeSpan?, CancellationToken, Task> waitForFalseAsync,
        TimeSpan timeout,
        Action<string, string, string> logWait,
        string operation,
        CancellationToken ct)
    {
        if (step == null)
        {
            return;
        }

        await WaitIfKnownTrueAsync(
            PlcBlockTagHelper.GetErrorTag(step),
            "Block.Error",
            subscription,
            waitForFalseAsync,
            timeout,
            logWait,
            operation,
            ct);
        await WaitIfKnownTrueAsync(
            PlcBlockTagHelper.GetEndTag(step),
            "Block.End",
            subscription,
            waitForFalseAsync,
            timeout,
            logWait,
            operation,
            ct);
    }

    private static async Task WaitIfKnownTrueAsync(
        string? tag,
        string signalName,
        OpcUaSubscription subscription,
        Func<string, TimeSpan?, CancellationToken, Task> waitForFalseAsync,
        TimeSpan timeout,
        Action<string, string, string> logWait,
        string operation,
        CancellationToken ct)
    {
        if (tag == null || subscription.GetValue(tag) is not bool current || !current)
        {
            return;
        }

        logWait(signalName, operation, tag);
        await waitForFalseAsync(tag, timeout, ct);
    }
}
