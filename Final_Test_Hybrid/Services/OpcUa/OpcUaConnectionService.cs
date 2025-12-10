using Final_Test_Hybrid.Models.Plc.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public class OpcUaConnectionService(IOptions<OpcUaSettings> settings, ILogger<OpcUaConnectionService> logger)
{
    private readonly OpcUaSettings _settings = settings.Value;
    private Opc.Ua.ApplicationConfiguration? _appConfig;
    private Session? Session { get; set; }
    public bool IsConnected => Session is { Connected: true };

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _appConfig = await CreateApplicationConfigurationAsync();
            await _appConfig.ValidateAsync(ApplicationType.Client, cancellationToken);
            var endpoint = await SelectEndpointAsync(_settings.EndpointUrl, cancellationToken);
            Session = await CreateSessionAsync(endpoint, cancellationToken);
            logger.LogInformation("Подключено к OPC UA серверу: {Endpoint}", _settings.EndpointUrl);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка подключения к OPC UA серверу: {Endpoint}", _settings.EndpointUrl);
            return false;
        }
    }

    private Task<Opc.Ua.ApplicationConfiguration> CreateApplicationConfigurationAsync()
    {
        var config = new Opc.Ua.ApplicationConfiguration
        {
            ApplicationName = _settings.ApplicationName,
            ApplicationUri = $"urn:{Utils.GetHostName()}:{_settings.ApplicationName}",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                AutoAcceptUntrustedCertificates = true,
                RejectSHA1SignedCertificates = false,
                MinimumCertificateKeySize = 1024
            },
            TransportQuotas = new TransportQuotas { OperationTimeout = _settings.SessionTimeoutMs },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = _settings.SessionTimeoutMs }
        };
        return Task.FromResult(config);
    }

    private async Task<EndpointDescription> SelectEndpointAsync(string endpointUrl, CancellationToken cancellationToken)
    {
        var endpointConfiguration = EndpointConfiguration.Create(_appConfig);
        endpointConfiguration.OperationTimeout = _settings.SessionTimeoutMs;
        using var client = DiscoveryClient.Create(new Uri(endpointUrl), endpointConfiguration);
        var endpoints = await client.GetEndpointsAsync(null, cancellationToken);
        var endpoint = endpoints
            .Where(e => e.EndpointUrl.StartsWith("opc.tcp", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.SecurityLevel)
            .FirstOrDefault();
        return endpoint ?? throw new InvalidOperationException($"Не найден подходящий endpoint: {endpointUrl}");
    }

    private async Task<Session> CreateSessionAsync(EndpointDescription endpoint, CancellationToken cancellationToken)
    {
        var endpointConfiguration = EndpointConfiguration.Create(_appConfig);
        var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, endpointConfiguration);
        var session = await Session.Create(
            _appConfig,
            configuredEndpoint,
            false,
            _settings.ApplicationName,
            (uint)_settings.SessionTimeoutMs,
            new UserIdentity(new AnonymousIdentityToken()),
            null,
            cancellationToken);
        session.KeepAlive += OnSessionKeepAlive;
        return session;
    }

    private void OnSessionKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (ServiceResult.IsBad(e.Status))
        {
            logger.LogWarning("OPC UA KeepAlive failed: {Status}", e.Status);
        }
    }

    public async Task DisconnectAsync()
    {
        if (Session == null)
        {
            return;
        }
        try
        {
            Session.KeepAlive -= OnSessionKeepAlive;
            await Session.CloseAsync();
            logger.LogInformation("Отключено от OPC UA сервера");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при отключении от OPC UA сервера");
        }
        finally
        {
            Session?.Dispose();
            Session = null;
        }
    }
}
