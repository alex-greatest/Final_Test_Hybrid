using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public sealed class OpcUaConnectionService(
    IOptions<OpcUaSettings> settings,
    ILogger<OpcUaConnectionService> logger,
    ISessionFactory sessionFactory)
    : IOpcUaConnectionService
{
    public bool IsConnected => Session?.Connected == true;
    public ISession? Session { get; private set; }
    public event Action<bool>? ConnectionChanged;
    private readonly OpcUaSettings _settings = settings.Value;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private SessionReconnectHandler? _reconnectHandler;
    private PeriodicTimer? _connectTimer;
    private Task? _connectLoopTask;
    private volatile bool _isReconnecting;
    private volatile bool _isDisposed;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return Task.CompletedTask;
        }
        _connectTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.ReconnectIntervalMs));
        _connectLoopTask = ConnectLoopAsync(_disposeCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        await _disposeCts.CancelAsync();
        _connectTimer?.Dispose();
        await WaitForConnectLoopAsync();
        await _sessionLock.WaitAsync();
        try
        {
            DisposeReconnectHandler();
            await CloseSessionAsync();
        }
        finally
        {
            _sessionLock.Release();
            _sessionLock.Dispose();
            _disposeCts.Dispose();
        }
    }

    private async Task ConnectLoopAsync(CancellationToken cancellationToken)
    {
        await TryConnectAsync();
        if (_connectTimer is null)
        {
            return;
        }
        while (await WaitForNextTickAsync(cancellationToken))
        {
            await TryConnectIfNotConnectedAsync();
        }
    }

    private async Task<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _connectTimer!.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task TryConnectIfNotConnectedAsync()
    {
        if (IsConnected || _isReconnecting)
        {
            return;
        }
        await TryConnectAsync();
    }

    private async Task TryConnectAsync()
    {
        await _sessionLock.WaitAsync(_disposeCts.Token);
        try
        {
            await TryConnectCoreAsync();
        }
        catch (OperationCanceledException)
        {
            // Dispose requested
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to connect to OPC UA server at {Endpoint}", _settings.EndpointUrl);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task TryConnectCoreAsync()
    {
        if (IsConnected || _isDisposed)
        {
            return;
        }
        logger.LogInformation("Attempting to connect to {Endpoint}", _settings.EndpointUrl);
        var config = await CreateApplicationConfigurationAsync();
        var endpoint = await CoreClientUtils.SelectEndpointAsync(config, _settings.EndpointUrl, useSecurity: false);
        var session = await sessionFactory.CreateAsync(
            config,
            new ConfiguredEndpoint(null, endpoint),
            updateBeforeConnect: false,
            sessionName: _settings.ApplicationName,
            sessionTimeout: (uint)_settings.SessionTimeoutMs,
            identity: new UserIdentity(new AnonymousIdentityToken()),
            preferredLocales: null);
        Session = session;
        Session.KeepAlive += OnKeepAlive;
        logger.LogInformation("Connected to OPC UA server");
        RaiseConnectionChanged(true);
    }

    private async Task<Opc.Ua.ApplicationConfiguration> CreateApplicationConfigurationAsync()
    {
        var config = new Opc.Ua.ApplicationConfiguration
        {
            ApplicationName = _settings.ApplicationName,
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier(),
                AutoAcceptUntrustedCertificates = true
            },
            ClientConfiguration = new ClientConfiguration
            {
                DefaultSessionTimeout = _settings.SessionTimeoutMs
            }
        };
        await config.ValidateAsync(ApplicationType.Client);
        return config;
    }

    private void OnKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }
        if (ServiceResult.IsGood(e.Status))
        {
            return;
        }
        HandleKeepAliveError(session);
    }

    private void HandleKeepAliveError(ISession session)
    {
        logger.LogWarning("Connection lost, starting reconnect handler");
        RaiseConnectionChanged(false);
        StartReconnectHandler(session);
    }

    private void StartReconnectHandler(ISession session)
    {
        if (_reconnectHandler is not null)
        {
            return;
        }
        _isReconnecting = true;
        _reconnectHandler = new SessionReconnectHandler();
        _reconnectHandler.BeginReconnect(
            session,
            _settings.ReconnectIntervalMs,
            OnReconnectComplete);
    }

    private void OnReconnectComplete(object? sender, EventArgs e)
    {
        if (_isDisposed || _reconnectHandler is null)
        {
            return;
        }
        _ = HandleReconnectCompleteAsync();
    }

    private async Task HandleReconnectCompleteAsync()
    {
        await _sessionLock.WaitAsync();
        try
        {
            UpdateSessionIfReconnected();
        }
        finally
        {
            _sessionLock.Release();
            _isReconnecting = false;
        }
    }

    private void UpdateSessionIfReconnected()
    {
        if (_reconnectHandler?.Session is null)
        {
            return;
        }
        var reconnectedSession = _reconnectHandler.Session;
        var isRecreated = !Equals(reconnectedSession, Session);
        if (isRecreated)
        {
            HandleSessionRecreate(reconnectedSession);
        }
        DisposeReconnectHandler();
        logger.LogInformation("Reconnected to OPC UA server (recreated: {IsRecreated})", isRecreated);
        RaiseConnectionChanged(true);
    }

    private void HandleSessionRecreate(ISession newSession)
    {
        logger.LogWarning("Session was recreated, old subscriptions lost");
        var oldSession = Session;
        oldSession?.Dispose();
        Session = newSession;
        Session.KeepAlive += OnKeepAlive;
    }

    private void DisposeReconnectHandler()
    {
        _reconnectHandler?.Dispose();
        _reconnectHandler = null;
    }

    private async Task CloseSessionAsync()
    {
        if (Session is null)
        {
            return;
        }
        Session.KeepAlive -= OnKeepAlive;
        await Session.CloseAsync();
        Session.Dispose();
        Session = null;
    }

    private async Task WaitForConnectLoopAsync()
    {
        if (_connectLoopTask is null)
        {
            return;
        }
        try
        {
            await _connectLoopTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    private void RaiseConnectionChanged(bool isConnected)
    {
        try
        {
            ConnectionChanged?.Invoke(isConnected);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ConnectionChanged handler");
        }
    }
}
