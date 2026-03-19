namespace Final_Test_Hybrid.Services.Main;

using Common;
using Models.Plc.Tags;
using Microsoft.Extensions.Logging;
using OpcUa.Connection;
using OpcUa.Subscription;

public class AutoReadySubscription(
    OpcUaSubscription opcUaSubscription,
    OpcUaConnectionState connectionState,
    ILogger<AutoReadySubscription> logger) : INotifyStateChanged
{
    private readonly Lock _subscriptionLock = new();
    private Func<object?, Task>? _callback;
    private volatile bool _isReady;
    private volatile bool _hasEverBeenReady;
    private bool _isSubscribed;

    public bool IsReady => _isReady;
    public bool HasEverBeenReady => _hasEverBeenReady;

    public event Action? OnStateChanged;
    public event Action? OnFirstAutoReceived;

    public async Task SubscribeAsync()
    {
        var callback = EnsureCallback();
        if (!TryMarkSubscribed())
        {
            return;
        }

        try
        {
            await opcUaSubscription.SubscribeAsync(BaseTags.TestAskAuto, callback);
        }
        catch
        {
            ResetSubscribedFlag();
            throw;
        }
    }

    private Func<object?, Task> EnsureCallback()
    {
        _callback ??= OnValueChanged;
        return _callback;
    }

    private bool TryMarkSubscribed()
    {
        lock (_subscriptionLock)
        {
            if (_isSubscribed)
            {
                return false;
            }
            _isSubscribed = true;
            return true;
        }
    }

    private void ResetSubscribedFlag()
    {
        lock (_subscriptionLock)
        {
            _isSubscribed = false;
        }
    }

    private Task OnValueChanged(object? value)
    {
        var isReady = value is true;
        var wasReady = _isReady;
        var shouldFireFirstAuto = isReady && !_hasEverBeenReady;
        _isReady = isReady;

        if (wasReady != isReady)
        {
            LogAutoReadyTransition(isReady, value);
        }

        if (shouldFireFirstAuto)
        {
            _hasEverBeenReady = true;
            InvokeActionSafe(OnFirstAutoReceived, nameof(OnFirstAutoReceived));
        }

        InvokeActionSafe(OnStateChanged, nameof(OnStateChanged));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Сбрасывает флаг первого получения автомата.
    /// Вызывается при сбросе или завершении теста.
    /// </summary>
    public void ResetFirstAutoFlag()
    {
        _hasEverBeenReady = false;
    }

    private void LogAutoReadyTransition(bool isReady, object? value)
    {
        logger.LogInformation(
            "AutoReady {State}. OpcConnected={OpcConnected}. RawValue={RawValue}",
            isReady ? "ON" : "OFF",
            connectionState.IsConnected,
            value);
    }

    private void InvokeActionSafe(Action? handler, string eventName)
    {
        try
        {
            handler?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка в обработчике {EventName}", eventName);
        }
    }
}
