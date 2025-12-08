using Final_Test_Hybrid.Services.OpcUa.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa
{
    public class OpcUaConnectionService(
        ILogger<OpcUaConnectionService> logger,
        IOptions<OpcUaSettings> settings,
        ISessionFactory sessionFactory)
        : IOpcUaConnectionService
    {
        public bool IsConnected => Session?.Connected == true;
        public Session? Session { get; private set; }
        public event EventHandler<bool>? ConnectionChanged;
        private readonly OpcUaSettings _settings = settings.Value;
        private readonly SemaphoreSlim _sessionLock = new(1, 1);
        private readonly Lock _reconnectLock = new();
        private SessionReconnectHandler? _reconnectHandler;
        private Opc.Ua.ApplicationConfiguration? _appConfig;
        private CancellationTokenSource? _cts;
        private Task? _connectLoopTask;
        private bool _lastConnectionState;

        public async Task StartAsync(CancellationToken ct = default)
        {
            if (_connectLoopTask is { IsCompleted: false })
            {
                return;
            }
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            await InitializeApplicationConfigAsync();
            _connectLoopTask = ConnectLoopAsync(_cts.Token);
        }

        public async Task StopAsync()
        {
            await CancelConnectionAsync();
            DisposeReconnectHandler();
            await _sessionLock.WaitAsync();
            try
            {
                await CloseSessionAsync();
            }
            finally
            {
                _sessionLock.Release();
            }
            NotifyConnectionChanged(false);
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _sessionLock.Dispose();
        }

        private async Task InitializeApplicationConfigAsync()
        {
            _appConfig = new Opc.Ua.ApplicationConfiguration
            {
                ApplicationName = _settings.ApplicationName,
                ApplicationUri = $"urn:{_settings.ApplicationName}",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier(),
                    AutoAcceptUntrustedCertificates = true
                },
                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = _settings.SessionTimeoutMs
                },
                TransportConfigurations = [],
                TransportQuotas = new TransportQuotas { OperationTimeout = _settings.OperationTimeoutMs }
            };
            await _appConfig.ValidateAsync(ApplicationType.Client);
        }

        private async Task ConnectLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await TryReconnectIfNeededAsync(ct);
                    await Task.Delay(_settings.ReconnectIntervalMs, ct);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Connection loop canceled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Connection loop failed");
            }
        }

        private async Task TryReconnectIfNeededAsync(CancellationToken ct)
        {
            if (IsReconnectingOrConnected())
            {
                return;
            }
            await TryConnectAsync(ct);
        }

        private bool IsReconnectingOrConnected()
        {
            lock (_reconnectLock)
            {
                return _reconnectHandler != null || Session is { Connected: true };
            }
        }

        private async Task TryConnectAsync(CancellationToken ct)
        {
            await _sessionLock.WaitAsync(ct);
            try
            {
                DisposeReconnectHandler();
                await CloseSessionAsync();
                logger.LogInformation("Connecting to OPC UA server: {Endpoint}", _settings.EndpointUrl);
                var endpoint = await CoreClientUtils.SelectEndpointAsync(_appConfig, _settings.EndpointUrl, useSecurity: false, ct: ct);
                var configEndpoint = new ConfiguredEndpoint(null, endpoint);
                Session = await CreateSessionAsync(configEndpoint, ct);
                Session.KeepAlive += OnSessionKeepAlive;
                logger.LogInformation("Connected to OPC UA server");
                NotifyConnectionChanged(true);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected during shutdown
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to OPC UA server");
                NotifyConnectionChanged(false);
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private void OnSessionKeepAlive(ISession session, KeepAliveEventArgs e)
        {
            if (ShouldSkipKeepAlive(e))
            {
                return;
            }
            logger.LogWarning("Connection lost. Status: {Status}", e.Status);
            NotifyConnectionChanged(false);
            StartReconnectIfNeeded();
        }

        private bool ShouldSkipKeepAlive(KeepAliveEventArgs e)
        {
            return e.Status == null || ServiceResult.IsGood(e.Status);
        }

        private void StartReconnectIfNeeded()
        {
            lock (_reconnectLock)
            {
                if (_reconnectHandler != null || Session == null)
                {
                    return;
                }
                logger.LogInformation("Reconnecting...");
                _reconnectHandler = new SessionReconnectHandler();
                _reconnectHandler.BeginReconnect(
                    Session,
                    _settings.ReconnectIntervalMs,
                    OnReconnectComplete);
            }
        }

        private void OnReconnectComplete(object? sender, EventArgs e)
        {
            lock (_reconnectLock)
            {
                UpdateSessionIfReconnected();
                DisposeReconnectHandler();
            }
        }

        private void UpdateSessionIfReconnected()
        {
            if (_reconnectHandler?.Session is not Session newSession)
            {
                return;
            }
            Session?.KeepAlive -= OnSessionKeepAlive;
            Session = newSession;
            logger.LogInformation("Reconnected to OPC UA server");
            Session.KeepAlive += OnSessionKeepAlive;
            NotifyConnectionChanged(true);
        }

        private void NotifyConnectionChanged(bool connected)
        {
            if (_lastConnectionState == connected)
            {
                return;
            }
            _lastConnectionState = connected;
            ConnectionChanged?.Invoke(this, connected);
        }

        private async Task CancelConnectionAsync()
        {
            if (_cts == null)
            {
                return;
            }
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
            DisposeReconnectHandler();
            await WaitConnectLoopAsync();
        }

        private void DisposeReconnectHandler()
        {
            lock (_reconnectLock)
            {
                _reconnectHandler?.Dispose();
                _reconnectHandler = null;
            }
        }

        private async Task CloseSessionAsync()
        {
            if (Session == null)
            {
                return;
            }
            Session.KeepAlive -= OnSessionKeepAlive;
            try
            {
                await Session.CloseAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error closing session");
            }
            Session.Dispose();
            Session = null;
        }

        private async Task WaitConnectLoopAsync()
        {
            if (_connectLoopTask == null)
            {
                return;
            }
            try
            {
                await _connectLoopTask;
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Connection loop canceled");
            }
            _connectLoopTask = null;
        }

        private async Task<Session> CreateSessionAsync(ConfiguredEndpoint configEndpoint, CancellationToken ct)
        {
            var session = await sessionFactory.CreateAsync(
                _appConfig,
                configEndpoint,
                updateBeforeConnect: false,
                sessionName: _settings.ApplicationName,
                sessionTimeout: (uint)_settings.SessionTimeoutMs,
                identity: new UserIdentity(new AnonymousIdentityToken()),
                preferredLocales: null,
                ct).ConfigureAwait(false);
            return session switch
            {
                Session s => s,
                _ => throw new InvalidOperationException($"SessionFactory returned unexpected type: {session.GetType().Name}")
            };
        }
    }
}
