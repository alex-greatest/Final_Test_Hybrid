namespace Final_Test_Hybrid.Models.Plc.Settings;

public class OpcUaSettings
{
    public string EndpointUrl { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public int ReconnectIntervalMs { get; set; }
    public int SessionTimeoutMs { get; set; }
    private OpcUaSubscriptionSettings Subscription { get; set; } = new();

    public void Validate()
    {
        ValidateEndpointUrl();
        ValidateApplicationName();
        ValidateReconnectInterval();
        ValidateSessionTimeout();
        Subscription.Validate();
    }

    private void ValidateEndpointUrl()
    {
        if (string.IsNullOrWhiteSpace(EndpointUrl))
        {
            throw new InvalidOperationException("OpcUa:EndpointUrl не задан");
        }
        if (!EndpointUrl.StartsWith("opc.tcp://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"OpcUa:EndpointUrl должен начинаться с opc.tcp:// (получено: {EndpointUrl})");
        }
    }

    private void ValidateApplicationName()
    {
        if (string.IsNullOrWhiteSpace(ApplicationName))
        {
            throw new InvalidOperationException("OpcUa:ApplicationName не задан");
        }
    }

    private void ValidateReconnectInterval()
    {
        if (ReconnectIntervalMs is < 1000 or > 60000)
        {
            throw new InvalidOperationException($"OpcUa:ReconnectIntervalMs должен быть 1000-60000 мс (получено: {ReconnectIntervalMs})");
        }
    }

    private void ValidateSessionTimeout()
    {
        if (SessionTimeoutMs is < 10000 or > 300000)
        {
            throw new InvalidOperationException($"OpcUa:SessionTimeoutMs должен быть 10000-300000 мс (получено: {SessionTimeoutMs})");
        }
    }
}
