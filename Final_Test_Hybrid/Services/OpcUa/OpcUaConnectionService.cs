using AsyncAwaitBestPractices;
using Final_Test_Hybrid.Models.Plc.Settings;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public class OpcUaConnectionService(
    IOptions<OpcUaSettings> settingsOptions,
    OpcUaSubscription subscription,
    ILogger<OpcUaConnectionService> logger)
{
    private readonly OpcUaSettings _settings = settingsOptions.Value;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private Opc.Ua.ApplicationConfiguration? _appConfig;
    private SessionReconnectHandler? _reconnectHandler;
    public bool IsConnected => Session is { Connected: true };
    public bool IsReconnecting => _reconnectHandler != null;
    public ISession? Session { get; private set; }
    public event Action<bool>? ConnectionStateChanged;

    public void ValidateSettings()
    {
        _settings.Validate();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _appConfig = await AppConfigurator.CreateApplicationConfigurationAsync(_settings)
            .ConfigureAwait(false);
        await ConnectWithRetryAsync(cancellationToken).ConfigureAwait(false);
        await CreateSubscriptionAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var endpoint = await AppConfigurator.SelectEndpointAsync(_appConfig!, _settings.EndpointUrl, _settings, cancellationToken)
                    .ConfigureAwait(false);
                Session = await AppConfigurator.CreateSessionAsync(_appConfig!, _settings, endpoint, OnKeepAlive, cancellationToken)
                    .ConfigureAwait(false);
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
                await Task.Delay(_settings.ReconnectIntervalMs, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task CreateSubscriptionAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Создание подписки OPC UA...");
            await subscription.CreateAsync(Session!, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Подписка OPC UA создана");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось создать подписку OPC UA");
            throw;
        }
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
        StartReconnectSafe(session).SafeFireAndForget(
            ex => logger.LogError(ex, "Ошибка при запуске переподключения к OPC UA серверу"));
    }

    private async Task StartReconnectSafe(ISession session)
    {
        await using var _ = await AsyncLock.AcquireAsync(_semaphore);
        if (_reconnectHandler != null)
        {
            return;
        }
        _reconnectHandler = new SessionReconnectHandler(reconnectAbort: false);
        _reconnectHandler.BeginReconnect(session, _settings.ReconnectIntervalMs, OnReconnectComplete);
    }
    
    private void OnReconnectComplete(object? sender, EventArgs e)
    {
        OnReconnectCompleteSafe().SafeFireAndForget(
            ex => logger.LogError(ex, "Ошибка при завершении переподключения к OPC UA серверу"));
    }

    private async Task OnReconnectCompleteSafe()
    {
        await using var _ = await AsyncLock.AcquireAsync(_semaphore);
        var newSession = _reconnectHandler?.Session;
        if (newSession == null)
        {
            return;
        }
        Session = newSession;
        logger.LogInformation("Переподключение к OPC UA серверу выполнено успешно");
        ConnectionStateChanged?.Invoke(true);
        _reconnectHandler?.Dispose();
        _reconnectHandler = null;
    }
    
    public async Task DisconnectAsync()
    {
        await using var _ = await AsyncLock.AcquireAsync(_semaphore)
            .ConfigureAwait(false);
        _reconnectHandler?.Dispose();
        _reconnectHandler = null;
        if (Session == null)
        {
            return;
        }
        await CloseSessionAsync().ConfigureAwait(false);
    }
    
    private async Task CloseSessionAsync()
    {
        try
        {
            Session!.KeepAlive -= OnKeepAlive;
            await Session.CloseAsync(CancellationToken.None)
                .ConfigureAwait(false);
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
