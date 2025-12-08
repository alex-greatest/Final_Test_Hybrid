using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa
{
    public class OpcUaConnectionService(ILogger<OpcUaConnectionService> logger, IOptions<OpcUaSettings> settings)
        : IOpcUaConnectionService
    {
        public bool IsConnected => _session?.Connected == true;
        public Session? Session => _session;
        public event EventHandler<bool>? ConnectionChanged;
        private readonly OpcUaSettings _settings = settings.Value;
        private Session? _session;
        private SessionReconnectHandler? _reconnectHandler;
        private Opc.Ua.ApplicationConfiguration? _appConfig;
        private CancellationTokenSource? _cts;
        private bool _lastConnectionState;

        [Obsolete("Obsolete")]
        public async Task StartAsync(CancellationToken ct = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            await InitializeApplicationConfigAsync();
            _ = ConnectLoopAsync(_cts.Token);
        }

        public async Task StopAsync()
        {
            await CancelConnectionAsync();
            DisposeReconnectHandler();
            await CloseSessionAsync();
            NotifyConnectionChanged(false);
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            GC.SuppressFinalize(this);
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
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 }
            };
            await _appConfig.ValidateAsync(ApplicationType.Client);
        }

        [Obsolete("Obsolete")]
        private async Task ConnectLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await TryReconnectIfNeededAsync(ct);
                await Task.Delay(_settings.ReconnectIntervalMs, ct);
            }
        }

        private async Task TryReconnectIfNeededAsync(CancellationToken ct)
        {
            if (_session == null || !_session.Connected)
            {
                await TryConnectAsync(ct);
            }
        }

        [Obsolete("Obsolete")]
        private async Task TryConnectAsync(CancellationToken ct)
        {
            try
            {
                logger.LogInformation("Connecting to OPC UA server: {Endpoint}", _settings.EndpointUrl);
                var endpoint = await CoreClientUtils.SelectEndpointAsync(_appConfig, _settings.EndpointUrl, useSecurity: false, ct: ct);
                var configEndpoint = new ConfiguredEndpoint(null, endpoint);
                _session = await Session.Create(
                    _appConfig,
                    configEndpoint,
                    updateBeforeConnect: false,
                    sessionName: _settings.ApplicationName,
                    sessionTimeout: (uint)_settings.SessionTimeoutMs,
                    identity: new UserIdentity(new AnonymousIdentityToken()),
                    preferredLocales: null,
                    ct);
                _session.KeepAlive += OnSessionKeepAlive;
                logger.LogInformation("Connected to OPC UA server");
                NotifyConnectionChanged(true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to OPC UA server");
                NotifyConnectionChanged(false);
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
            if (_reconnectHandler != null)
            {
                return;
            }
            logger.LogInformation("Reconnecting...");
            _reconnectHandler = new SessionReconnectHandler();
            _reconnectHandler.BeginReconnect(
                _session,
                _settings.ReconnectIntervalMs,
                OnReconnectComplete);
        }

        private void OnReconnectComplete(object? sender, EventArgs e)
        {
            UpdateSessionIfReconnected();
            DisposeReconnectHandler();
        }

        private void UpdateSessionIfReconnected()
        {
            if (_reconnectHandler?.Session != null)
            {
                _session = (Session)_reconnectHandler.Session;
                logger.LogInformation("Reconnected to OPC UA server");
                NotifyConnectionChanged(true);
            }
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
            await _cts?.CancelAsync()!;
        }

        private void DisposeReconnectHandler()
        {
            _reconnectHandler?.Dispose();
            _reconnectHandler = null;
        }

        private async Task CloseSessionAsync()
        {
            if (_session != null)
            {
                await _session.CloseAsync();
                _session.Dispose();
                _session = null;
            }
        }
    }
}
