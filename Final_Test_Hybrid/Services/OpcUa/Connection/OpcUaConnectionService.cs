using AsyncAwaitBestPractices;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Settings.OpcUa;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa.Connection;

public class OpcUaConnectionService(
    IOptions<OpcUaSettings> settingsOptions,
    OpcUaSubscription subscription,
    OpcUaConnectionState connectionState,
    PlcSubscriptionState subscriptionState,
    DualLogger<OpcUaConnectionService> logger)
{
    private readonly OpcUaSettings _settings = settingsOptions.Value;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private Opc.Ua.ApplicationConfiguration? _appConfig;
    private CancellationTokenSource? _reconnectCts;
    private bool _isReconnecting;
    public bool IsConnected => Session is { Connected: true };
    public bool IsReconnecting => _isReconnecting;
    public ISession? Session { get; private set; }

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
        connectionState.SetConnected(true);
    }

    private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Session = await CreateReconnectSessionAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Подключено к OPC UA серверу: {Endpoint}", _settings.EndpointUrl);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Не удалось подключиться к OPC UA серверу. Повтор через {Interval} мс. Ошибка: {Error}",
                    _settings.ReconnectIntervalMs,
                    ex.Message);
                await Task.Delay(_settings.ReconnectIntervalMs, cancellationToken).ConfigureAwait(false);
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

        if (!ReferenceEquals(session, Session))
        {
            return;
        }

        logger.LogError("OPC UA KeepAlive не удался: {Status}. Запуск переподключения...", e.Status);
        subscription.InvalidateValuesCache();
        connectionState.SetConnected(false);
        StartReconnect();
    }

    private void StartReconnect()
    {
        StartReconnectSafe().SafeFireAndForget(ex =>
            logger.LogError(ex, "Ошибка при запуске reconnect-loop к OPC UA серверу"));
    }

    private async Task StartReconnectSafe()
    {
        await using var _ = await AsyncLock.AcquireAsync(_semaphore);
        if (_isReconnecting)
        {
            return;
        }

        CancelReconnectLoopUnsafe();
        _reconnectCts = new CancellationTokenSource();
        _isReconnecting = true;
        Session?.KeepAlive -= OnKeepAlive;
        ReconnectLoopSafeAsync(_reconnectCts.Token).SafeFireAndForget(ex =>
            logger.LogError(ex, "Reconnect-loop OPC UA завершился с ошибкой"));
    }

    private async Task ReconnectLoopSafeAsync(CancellationToken ct)
    {
        try
        {
            await ReconnectLoopAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            await CompleteReconnectLoopAsync().ConfigureAwait(false);
        }
    }

    private async Task ReconnectLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var isConnected = await TryReconnectOnceAsync(ct).ConfigureAwait(false);
            if (isConnected)
            {
                return;
            }

            await Task.Delay(_settings.ReconnectIntervalMs, ct).ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();
    }

    private async Task<bool> TryReconnectOnceAsync(CancellationToken ct)
    {
        ISession? newSession = null;
        try
        {
            newSession = await CreateReconnectSessionAsync(ct).ConfigureAwait(false);
            await CompleteReconnectAttemptAsync(newSession, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await DisposeSessionAsync(newSession, logSuccess: false).ConfigureAwait(false);
            subscriptionState.SetCompleted();
            connectionState.SetConnected(false);
            logger.LogWarning(
                "Reconnect не завершён. Повтор через {Interval} мс. Ошибка: {Error}",
                _settings.ReconnectIntervalMs,
                ex.Message);
            return false;
        }
    }

    private async Task<ISession> CreateReconnectSessionAsync(CancellationToken ct)
    {
        var endpoint = await AppConfigurator.SelectEndpointAsync(
            _appConfig!,
            _settings.EndpointUrl,
            _settings,
            ct).ConfigureAwait(false);
        return await AppConfigurator.CreateSessionAsync(
            _appConfig!,
            _settings,
            endpoint,
            OnKeepAlive,
            ct).ConfigureAwait(false);
    }

    private async Task CompleteReconnectAttemptAsync(ISession newSession, CancellationToken ct)
    {
        await DetachCurrentSessionAsync().ConfigureAwait(false);
        subscriptionState.SetInitializing();
        await subscription.RecreateForSessionAsync(newSession, ct).ConfigureAwait(false);
        await SetActiveSessionAsync(newSession, ct).ConfigureAwait(false);
        connectionState.SetConnected(true);
        subscriptionState.SetCompleted();
        logger.LogInformation("Переподключение к OPC UA серверу выполнено успешно. Runtime-подписки пересозданы");
    }

    private async Task DetachCurrentSessionAsync()
    {
        ISession? oldSession;
        await using (await AsyncLock.AcquireAsync(_semaphore).ConfigureAwait(false))
        {
            oldSession = Session;
            Session = null;
        }

        await DisposeSessionAsync(oldSession, logSuccess: false).ConfigureAwait(false);
    }

    private async Task SetActiveSessionAsync(ISession newSession, CancellationToken ct)
    {
        await using (await AsyncLock.AcquireAsync(_semaphore, ct).ConfigureAwait(false))
        {
            Session = newSession;
        }
    }

    public async Task DisconnectAsync()
    {
        ISession? sessionToClose;
        await using (await AsyncLock.AcquireAsync(_semaphore).ConfigureAwait(false))
        {
            CancelReconnectLoopUnsafe();
            _isReconnecting = false;
            sessionToClose = Session;
            Session = null;
        }

        await DisposeSessionAsync(sessionToClose, logSuccess: true).ConfigureAwait(false);
        subscription.InvalidateValuesCache();
        connectionState.SetConnected(false);
    }

    private async Task CompleteReconnectLoopAsync()
    {
        await using (await AsyncLock.AcquireAsync(_semaphore).ConfigureAwait(false))
        {
            _isReconnecting = false;
            _reconnectCts?.Dispose();
            _reconnectCts = null;
        }
    }

    private void CancelReconnectLoopUnsafe()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
    }

    private async Task DisposeSessionAsync(ISession? session, bool logSuccess)
    {
        if (session == null)
        {
            return;
        }

        try
        {
            session.KeepAlive -= OnKeepAlive;
            await session.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            if (logSuccess)
            {
                logger.LogInformation("Отключено от OPC UA сервера");
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug("Не удалось корректно закрыть OPC UA сессию: {Error}", ex.Message);
        }
        finally
        {
            session.Dispose();
        }
    }
}
