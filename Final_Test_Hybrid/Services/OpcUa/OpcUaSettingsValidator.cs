using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa;

/// <summary>
/// Validates OPC UA settings and logs warnings for suboptimal configurations.
/// </summary>
public static class OpcUaSettingsValidator
{
    public static void Validate(OpcUaSettings settings, ILogger? logger = null)
    {
        ValidateEndpointUrl(settings);
        LogWarningsIfNeeded(settings, logger);
    }

    private static void ValidateEndpointUrl(OpcUaSettings settings)
    {
        ValidateEndpointUrlNotEmpty(settings);
        ValidateEndpointUrlProtocol(settings);
    }

    private static void ValidateEndpointUrlNotEmpty(OpcUaSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.EndpointUrl))
        {
            throw new InvalidOperationException("OpcUa:EndpointUrl is required");
        }
    }

    private static void ValidateEndpointUrlProtocol(OpcUaSettings settings)
    {
        if (!settings.EndpointUrl.StartsWith("opc.tcp://"))
        {
            throw new InvalidOperationException("OpcUa:EndpointUrl must start with 'opc.tcp://'");
        }
    }

    private static void LogWarningsIfNeeded(OpcUaSettings settings, ILogger? logger)
    {
        if (logger is null)
        {
            return;
        }
        LogReconnectIntervalWarning(settings, logger);
        LogSessionTimeoutWarning(settings, logger);
    }

    private static void LogReconnectIntervalWarning(OpcUaSettings settings, ILogger logger)
    {
        if (settings.ReconnectIntervalMs < 1000)
        {
            logger.LogWarning("ReconnectIntervalMs < 1000ms may cause excessive retries");
        }
    }

    private static void LogSessionTimeoutWarning(OpcUaSettings settings, ILogger logger)
    {
        if (settings.SessionTimeoutMs < 10000)
        {
            logger.LogWarning("SessionTimeoutMs < 10s may cause frequent reconnections");
        }
    }
}
