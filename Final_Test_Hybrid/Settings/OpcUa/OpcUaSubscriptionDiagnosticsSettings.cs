namespace Final_Test_Hybrid.Settings.OpcUa;

/// <summary>
/// Настройки диагностического логирования runtime OPC-подписок.
/// </summary>
public class OpcUaSubscriptionDiagnosticsSettings
{
    public bool Enabled { get; set; }
    public int SnapshotIntervalSec { get; set; } = 30;
    public int LogTopNodeIds { get; set; } = 10;

    public void Validate()
    {
        ValidateSnapshotInterval();
        ValidateLogTopNodeIds();
    }

    private void ValidateSnapshotInterval()
    {
        if (SnapshotIntervalSec is < 5 or > 300)
        {
            throw new InvalidOperationException(
                $"OpcUa:SubscriptionDiagnostics:SnapshotIntervalSec должен быть 5-300 сек (получено: {SnapshotIntervalSec})");
        }
    }

    private void ValidateLogTopNodeIds()
    {
        if (LogTopNodeIds is < 1 or > 100)
        {
            throw new InvalidOperationException(
                $"OpcUa:SubscriptionDiagnostics:LogTopNodeIds должен быть 1-100 (получено: {LogTopNodeIds})");
        }
    }
}
