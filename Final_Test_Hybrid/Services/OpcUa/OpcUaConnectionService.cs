using Final_Test_Hybrid.Models.Plc.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public class OpcUaConnectionService(IOptions<OpcUaSettings> settingsOptions, ILogger<OpcUaConnectionService> logger)
{
    private readonly OpcUaSettings _settings = settingsOptions.Value;
    private readonly Lock _lock = new();
    private Opc.Ua.ApplicationConfiguration? _appConfig;
    private SessionReconnectHandler? _reconnectHandler;
    private ISession? Session { get; set; }
    public bool IsConnected => Session is { Connected: true };
    public bool IsReconnecting => _reconnectHandler != null;
    public event EventHandler? Reconnecting;
    public event EventHandler? Reconnected;

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _settings.Validate();
            _appConfig = await AppConfigurator.CreateApplicationConfigurationAsync(_settings);
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

    private async Task<ISession> CreateSessionAsync(EndpointDescription endpoint, CancellationToken cancellationToken)
    {
        var endpointConfiguration = EndpointConfiguration.Create(_appConfig);
        var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, endpointConfiguration);
        var sessionFactory = DefaultSessionFactory.Instance;
        var session = await sessionFactory.CreateAsync(
            _appConfig,
            configuredEndpoint,
            updateBeforeConnect: false,
            sessionName: _settings.ApplicationName,
            sessionTimeout: (uint)_settings.SessionTimeoutMs,
            identity: new UserIdentity(new AnonymousIdentityToken()),
            preferredLocales: null,
            cancellationToken);
        session.KeepAlive += OnKeepAlive;
        return session;
    }

    private void OnKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (ServiceResult.IsGood(e.Status))
        {
            return;
        }
        logger.LogWarning("OPC UA KeepAlive failed: {Status}. Запуск переподключения...", e.Status);
        StartReconnect(session);
    }

    private void StartReconnect(ISession session)
    {
        lock (_lock)
        {
            if (_reconnectHandler != null)
            {
                return;
            }
            Reconnecting?.Invoke(this, EventArgs.Empty);
            _reconnectHandler = new SessionReconnectHandler(reconnectAbort: false);
            _reconnectHandler.BeginReconnect(
                session,
                _settings.ReconnectIntervalMs,
                OnReconnectComplete);
        }
    }

    private void OnReconnectComplete(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            var newSession = _reconnectHandler?.Session;
            if (newSession == null)
            {
                return;
            }
            Session = newSession;
            logger.LogInformation("Переподключение к OPC UA серверу выполнено успешно");
            Reconnected?.Invoke(this, EventArgs.Empty);
            _reconnectHandler?.Dispose();
            _reconnectHandler = null;
        }
    }

    public async Task DisconnectAsync()
    {
        lock (_lock)
        {
            _reconnectHandler?.Dispose();
            _reconnectHandler = null;
        }
        if (Session == null)
        {
            return;
        }
        try
        {
            Session.KeepAlive -= OnKeepAlive;
            await Session.CloseAsync(CancellationToken.None);
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
