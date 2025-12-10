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
    public event Action<bool>? ConnectionStateChanged;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _settings.Validate();
        _appConfig = await AppConfigurator.CreateApplicationConfigurationAsync(_settings);
        await _appConfig.ValidateAsync(ApplicationType.Client, cancellationToken);
        await ConnectWithRetryAsync(cancellationToken);
    }

    private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var endpoint = await AppConfigurator.SelectEndpointAsync(_appConfig!, _settings.EndpointUrl, _settings, cancellationToken);
                Session = await AppConfigurator.CreateSessionAsync(_appConfig!, _settings, endpoint, OnKeepAlive, cancellationToken);
                logger.LogInformation("Подключено к OPC UA серверу: {Endpoint}", _settings.EndpointUrl);
                ConnectionStateChanged?.Invoke(true);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось подключиться к OPC UA серверу. Повтор через {Interval} мс", _settings.ReconnectIntervalMs);
                await Task.Delay(_settings.ReconnectIntervalMs, cancellationToken);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void OnKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (ServiceResult.IsGood(e.Status))
        {
            return;
        }
        logger.LogWarning("OPC UA KeepAlive failed: {Status}. Запуск переподключения...", e.Status);
        ConnectionStateChanged?.Invoke(false);
        StartReconnect(session);
    }

    private void StartReconnect(ISession session)
    {
        lock (_lock)
        {
            ReconnectSafe(session);
        }
    }

    private void ReconnectSafe(ISession session)
    {
        if (_reconnectHandler != null)
        {
            return;
        }
        _reconnectHandler = new SessionReconnectHandler(reconnectAbort: false);
        _reconnectHandler.BeginReconnect(
            session,
            _settings.ReconnectIntervalMs,
            OnReconnectComplete);
    }

    private void OnReconnectComplete(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            var newSession = _reconnectHandler?.Session;
            OnReconnectCompleteSafe(newSession);
        }
    }

    private void OnReconnectCompleteSafe(ISession? session)
    {
        if (session == null)
        {
            return;
        }
        Session = session;
        logger.LogInformation("Переподключение к OPC UA серверу выполнено успешно");
        ConnectionStateChanged?.Invoke(true);
        _reconnectHandler?.Dispose();
        _reconnectHandler = null;
    }
    
    public Task DisconnectAsync()
    {
        lock (_lock)
        {
            _reconnectHandler?.Dispose();
            _reconnectHandler = null;
            DisconnectAsyncSafe();
            return Task.CompletedTask;
        }
    }

    private async void DisconnectAsyncSafe()
    {
        try
        {
            if (Session == null)
            {
                return;
            }
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
            ConnectionStateChanged?.Invoke(false);
        }
    }
}
