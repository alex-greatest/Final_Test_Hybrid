using Final_Test_Hybrid.Models.Plc.Settings;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace Final_Test_Hybrid.Services.OpcUa;

public static class AppConfigurator
{
    public static async Task<Opc.Ua.ApplicationConfiguration> CreateApplicationConfigurationAsync(OpcUaSettings settings)
    {
        var pkiRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            settings.ApplicationName,
            "pki");

        var config = new Opc.Ua.ApplicationConfiguration
        {
            ApplicationName = settings.ApplicationName,
            ApplicationUri = $"urn:{Utils.GetHostName()}:{settings.ApplicationName}",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pkiRoot, "own"),
                    SubjectName = $"CN={settings.ApplicationName}, DC={Utils.GetHostName()}"
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pkiRoot, "trusted")
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pkiRoot, "issuer")
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pkiRoot, "rejected")
                },
                AutoAcceptUntrustedCertificates = true,
                RejectSHA1SignedCertificates = false,
                MinimumCertificateKeySize = 1024,
                AddAppCertToTrustedStore = true
            },
            TransportQuotas = new TransportQuotas { OperationTimeout = settings.SessionTimeoutMs },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = settings.SessionTimeoutMs }
        };

        await config.ValidateAsync(ApplicationType.Client, CancellationToken.None);
        var application = new ApplicationInstance(config);
        await application.CheckApplicationInstanceCertificatesAsync(silent: true, lifeTimeInMonths: 120);
        config.CertificateValidator.CertificateValidation += (_, e) => e.Accept = true;
        return config;
    }
    
    public static async Task<ISession> CreateSessionAsync(
        Opc.Ua.ApplicationConfiguration appConfig,
        OpcUaSettings settings,
        EndpointDescription endpoint,
        KeepAliveEventHandler keepAliveHandler,
        CancellationToken cancellationToken)
    {
        var endpointConfiguration = EndpointConfiguration.Create(appConfig);
        var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, endpointConfiguration);
        var sessionFactory = DefaultSessionFactory.Instance;
        var session = await sessionFactory.CreateAsync(
            appConfig,
            configuredEndpoint,
            updateBeforeConnect: false,
            sessionName: settings.ApplicationName,
            sessionTimeout: (uint)settings.SessionTimeoutMs,
            identity: new UserIdentity(new AnonymousIdentityToken()),
            preferredLocales: null,
            cancellationToken);
        session.KeepAlive += keepAliveHandler;
        return session;
    }
    
    public static async Task<EndpointDescription> SelectEndpointAsync(Opc.Ua.ApplicationConfiguration appConfig,
        string endpointUrl, OpcUaSettings settings,
        CancellationToken cancellationToken)
    {
        var endpointConfiguration = EndpointConfiguration.Create(appConfig);
        endpointConfiguration.OperationTimeout = settings.SessionTimeoutMs;
        using var client = DiscoveryClient.Create(new Uri(endpointUrl), endpointConfiguration);
        var endpoints = await client.GetEndpointsAsync(null, cancellationToken);
        var endpoint = endpoints
            .Where(e => e.EndpointUrl.StartsWith("opc.tcp", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(e => e.SecurityMode == MessageSecurityMode.None);
        return endpoint ?? throw new InvalidOperationException($"Не найден подходящий endpoint: {endpointUrl}");
    }
}