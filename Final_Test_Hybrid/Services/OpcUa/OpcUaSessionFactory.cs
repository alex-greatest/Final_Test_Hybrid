using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

/// <summary>
/// Creates configured OPC UA sessions with proper ApplicationConfiguration.
/// </summary>
public sealed class OpcUaSessionFactory(ISessionFactory sdkSessionFactory)
    : IOpcUaSessionFactory
{
    public async Task<ISession> CreateAsync(OpcUaSettings settings, CancellationToken cancellationToken = default)
    {
        var config = await CreateApplicationConfigurationAsync(settings, cancellationToken).ConfigureAwait(false);
        var configuredEndpoint = CreateConfiguredEndpoint(settings);
        return await sdkSessionFactory.CreateAsync(
            config,
            configuredEndpoint,
            false,
            settings.ApplicationName,
            (uint)settings.SessionTimeoutMs,
            new UserIdentity(new AnonymousIdentityToken()),
            null,
            cancellationToken).ConfigureAwait(false);
    }

    private static ConfiguredEndpoint CreateConfiguredEndpoint(OpcUaSettings settings)
    {
        var endpointDescription = new EndpointDescription(settings.EndpointUrl);
        var endpointConfiguration = EndpointConfiguration.Create();
        endpointConfiguration.OperationTimeout = settings.SessionTimeoutMs;
        return new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
    }

    private static async Task<Opc.Ua.ApplicationConfiguration> CreateApplicationConfigurationAsync(
        OpcUaSettings settings,
        CancellationToken cancellationToken)
    {
        var config = new Opc.Ua.ApplicationConfiguration
        {
            ApplicationName = settings.ApplicationName,
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = CreateSecurityConfiguration(),
            TransportConfigurations = [],
            TransportQuotas = CreateTransportQuotas(settings),
            ClientConfiguration = CreateClientConfiguration(settings),
            DisableHiResClock = false
        };

        await config.ValidateAsync(ApplicationType.Client, cancellationToken).ConfigureAwait(false);
        return config;
    }

    private static SecurityConfiguration CreateSecurityConfiguration()
    {
        return new SecurityConfiguration
        {
            ApplicationCertificate = new CertificateIdentifier(),
            AutoAcceptUntrustedCertificates = true,
            RejectSHA1SignedCertificates = false
        };
    }

    private static TransportQuotas CreateTransportQuotas(OpcUaSettings settings)
    {
        return new TransportQuotas
        {
            OperationTimeout = settings.SessionTimeoutMs,
            MaxStringLength = settings.MaxStringLength,
            MaxByteStringLength = settings.MaxByteStringLength,
            MaxArrayLength = settings.MaxArrayLength,
            MaxMessageSize = settings.MaxMessageSize,
            MaxBufferSize = settings.MaxBufferSize,
            ChannelLifetime = settings.ChannelLifetimeMs,
            SecurityTokenLifetime = settings.SecurityTokenLifetimeMs
        };
    }

    private static ClientConfiguration CreateClientConfiguration(OpcUaSettings settings)
    {
        return new ClientConfiguration
        {
            DefaultSessionTimeout = settings.SessionTimeoutMs,
            MinSubscriptionLifetime = 10000
        };
    }
}
