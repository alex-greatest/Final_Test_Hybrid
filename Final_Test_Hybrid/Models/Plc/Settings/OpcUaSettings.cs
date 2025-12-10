namespace Final_Test_Hybrid.Models.Plc.Settings;

public class OpcUaSettings
{
    public string EndpointUrl { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public int ReconnectIntervalMs { get; set; }
    public int SessionTimeoutMs { get; set; }
    public OpcUaSubscriptionSettings Subscription { get; set; } = new();
}
