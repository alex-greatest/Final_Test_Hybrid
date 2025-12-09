using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public sealed class OpcUaConnectionService : IOpcUaConnectionService
{
    public bool IsConnected => _session?.Connected == true;
    public event Action<bool>? ConnectionChanged;
    public event Action? SessionRecreated;

    private readonly OpcUaSettings _settings;
    private readonly ILogger<OpcUaConnectionService> _logger;
    private readonly ISessionFactory _sessionFactory;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();

    private ISession? _session;
    private SessionReconnectHandler? _reconnectHandler;
    private volatile bool _isReconnecting;
    private PeriodicTimer? _connectTimer;
    private Task? _connectLoopTask;
    private volatile bool _isDisposed;

    public OpcUaConnectionService(
        IOptions<OpcUaSettings> settings,
        ILogger<OpcUaConnectionService> logger,
        ISessionFactory sessionFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _sessionFactory = sessionFactory;
        ValidateSettings();
    }

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

    public Task StopAsync()
    {
        return DisposeAsync().AsTask();
    }

    public async Task ExecuteWithSessionAsync(Func<ISession, Task> action, CancellationToken cancellationToken = default)
    {
        await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            await action(_session!).ConfigureAwait(false);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async Task<T> ExecuteWithSessionAsync<T>(Func<ISession, Task<T>> action, CancellationToken cancellationToken = default)
    {
        await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            return await action(_session!).ConfigureAwait(false);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        await _disposeCts.CancelAsync().ConfigureAwait(false);
        _connectTimer?.Dispose();
        await WaitForConnectLoopAsync().ConfigureAwait(false);

        await _sessionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            DisposeReconnectHandler();
            await CloseSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            _sessionLock.Release();
            _sessionLock.Dispose();
            _disposeCts.Dispose();
        }
    }

    private void EnsureConnected()
    {
        if (_session is null || !_session.Connected)
        {
            throw new InvalidOperationException("OPC UA session is not connected");
        }
    }

    private async Task ConnectLoopAsync(CancellationToken cancellationToken)
    {
        await TryConnectAsync().ConfigureAwait(false);

        if (_connectTimer is null)
        {
            return;
        }

        while (await WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await TryConnectIfNotConnectedAsync().ConfigureAwait(false);
        }
    }

    private async Task<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _connectTimer!.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task TryConnectIfNotConnectedAsync()
    {
        if (_isReconnecting)
        {
            return;
        }

        await TryConnectAsync().ConfigureAwait(false);
    }

    private async Task WaitForConnectLoopAsync()
    {
        if (_connectLoopTask is null)
        {
            return;
        }

        try
        {
            await _connectLoopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CloseSessionAsync()
    {
        if (_session is null)
        {
            return;
        }

        _session.KeepAlive -= OnKeepAlive;

        try
        {
            await _session.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing OPC UA session");
        }

        _session.Dispose();
        _session = null;
    }

    private async Task TryConnectAsync()
    {
        await _sessionLock.WaitAsync(_disposeCts.Token).ConfigureAwait(false);
        try
        {
            await TryConnectCoreAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to OPC UA server at {Endpoint}", _settings.EndpointUrl);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task TryConnectCoreAsync()
    {
        if (IsConnected)
        {
            return;
        }

        var config = await CreateApplicationConfigurationAsync().ConfigureAwait(false);
        var endpointDescription = new EndpointDescription(_settings.EndpointUrl);
        var endpointConfiguration = EndpointConfiguration.Create();
        endpointConfiguration.OperationTimeout = _settings.SessionTimeoutMs;
        var configuredEndpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);

        _session = await _sessionFactory.CreateAsync(
            config,
            configuredEndpoint,
            false,
            _settings.ApplicationName,
            (uint)_settings.SessionTimeoutMs,
            new UserIdentity(new AnonymousIdentityToken()),
            null,
            CancellationToken.None).ConfigureAwait(false);

        _session.KeepAlive += OnKeepAlive;
        _logger.LogInformation("Connected to OPC UA server at {Endpoint}", _settings.EndpointUrl);
        RaiseConnectionChangedAsync(true);
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
                AutoAcceptUntrustedCertificates = true,
                RejectSHA1SignedCertificates = false
            },
            TransportConfigurations = [],
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = _settings.SessionTimeoutMs,
                MaxStringLength = _settings.MaxStringLength,
                MaxByteStringLength = _settings.MaxByteStringLength,
                MaxArrayLength = _settings.MaxArrayLength,
                MaxMessageSize = _settings.MaxMessageSize,
                MaxBufferSize = _settings.MaxBufferSize,
                ChannelLifetime = _settings.ChannelLifetimeMs,
                SecurityTokenLifetime = _settings.SecurityTokenLifetimeMs
            },
            ClientConfiguration = new ClientConfiguration
            {
                DefaultSessionTimeout = _settings.SessionTimeoutMs,
                MinSubscriptionLifetime = 10000
            },
            DisableHiResClock = false
        };
        await config.ValidateAsync(ApplicationType.Client, CancellationToken.None).ConfigureAwait(false);
        return config;
    }

    private void OnKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (ServiceResult.IsGood(e.Status))
        {
            return;
        }

        HandleKeepAliveError(session);
    }

    private void HandleKeepAliveError(ISession session)
    {
        _logger.LogWarning("Connection lost to OPC UA server");
        RaiseConnectionChangedAsync(false);
        _ = StartReconnectHandlerAsync(session);
    }

    private async Task StartReconnectHandlerAsync(ISession session)
    {
        try
        {
            await _sessionLock.WaitAsync(_disposeCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            if (_isReconnecting || _isDisposed)
            {
                return;
            }

            _isReconnecting = true;
            _reconnectHandler = new SessionReconnectHandler();
            _reconnectHandler.BeginReconnect(session, _settings.ReconnectIntervalMs, OnReconnectComplete);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private void OnReconnectComplete(object? sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        _ = HandleReconnectCompleteAsync().ContinueWith(
            t => _logger.LogError(t.Exception, "Unhandled exception in reconnect handler"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private async Task HandleReconnectCompleteAsync()
    {
        await _sessionLock.WaitAsync(_disposeCts.Token).ConfigureAwait(false);
        try
        {
            UpdateSessionIfReconnected();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling reconnect completion");
        }
        finally
        {
            _isReconnecting = false;
            _sessionLock.Release();
        }
    }

    private void UpdateSessionIfReconnected()
    {
        var reconnectedSession = _reconnectHandler?.Session;
        if (reconnectedSession is null)
        {
            return;
        }

        if (IsSessionRecreated(reconnectedSession))
        {
            HandleSessionRecreate(reconnectedSession);
            RaiseSessionRecreatedAsync();
        }

        DisposeReconnectHandler();
        _logger.LogInformation("Reconnected to OPC UA server");
        RaiseConnectionChangedAsync(true);
    }

    private bool IsSessionRecreated(ISession newSession)
    {
        return !ReferenceEquals(newSession, _session);
    }

    private void HandleSessionRecreate(ISession newSession)
    {
        _logger.LogWarning("Session recreated, old subscriptions lost");
        var oldSession = _session;
        if (oldSession is not null)
        {
            oldSession.KeepAlive -= OnKeepAlive;
            oldSession.Dispose();
        }
        _session = newSession;
        _session.KeepAlive += OnKeepAlive;
    }

    private void RaiseSessionRecreatedAsync()
    {
        var handler = SessionRecreated;
        if (handler is null)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                handler.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SessionRecreated handler");
            }
        });
    }

    private void DisposeReconnectHandler()
    {
        _reconnectHandler?.Dispose();
        _reconnectHandler = null;
    }

    private void RaiseConnectionChangedAsync(bool isConnected)
    {
        var handler = ConnectionChanged;
        if (handler is null)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                handler.Invoke(isConnected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ConnectionChanged handler");
            }
        });
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.EndpointUrl))
        {
            throw new InvalidOperationException("OpcUa:EndpointUrl is required");
        }

        if (!_settings.EndpointUrl.StartsWith("opc.tcp://"))
        {
            throw new InvalidOperationException("OpcUa:EndpointUrl must start with 'opc.tcp://'");
        }

        if (_settings.ReconnectIntervalMs < 1000)
        {
            _logger.LogWarning("ReconnectIntervalMs < 1000ms may cause excessive retries");
        }

        if (_settings.SessionTimeoutMs < 10000)
        {
            _logger.LogWarning("SessionTimeoutMs < 10s may cause frequent reconnections");
        }
    }
}
