namespace Final_Test_Hybrid.Services.Main.PlcReset;

using Models.Plc.Tags;
using Common;
using OpcUa.Subscription;

/// <summary>
/// Подписка на сигнал Req_Reset от PLC.
/// Rising edge detection — срабатывает только на переход false→true.
/// </summary>
public sealed class ResetSubscription(OpcUaSubscription opcUaSubscription) : INotifyStateChanged, IAsyncDisposable
{
    private readonly Lock _subscriptionLock = new();
    private Func<object?, Task>? _callback;
    private bool _isSubscribed;
    private volatile bool _disposed;
    public bool IsResetRequested { get; private set; }
    public event Action? OnStateChanged;

    public async Task SubscribeAsync()
    {
        if (_disposed)
        {
            return;
        }

        var callback = EnsureCallback();
        if (!TryMarkSubscribed())
        {
            return;
        }

        try
        {
            await opcUaSubscription.SubscribeAsync(BaseTags.ReqReset, callback);
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
        if (_disposed) { return Task.CompletedTask; }

        var wasRequested = IsResetRequested;
        IsResetRequested = value is true;

        // Rising edge only — защита от повторных срабатываний
        if (!wasRequested && IsResetRequested)
        {
            OnStateChanged?.Invoke();
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        var callback = DetachCallback();
        if (callback != null)
        {
            await TryUnsubscribeAsync(callback);
        }

        OnStateChanged = null;
    }

    private Func<object?, Task>? DetachCallback()
    {
        lock (_subscriptionLock)
        {
            _isSubscribed = false;
            var callback = _callback;
            _callback = null;
            return callback;
        }
    }

    private async Task TryUnsubscribeAsync(Func<object?, Task> callback)
    {
        try
        {
            await opcUaSubscription.UnsubscribeAsync(BaseTags.ReqReset, callback, removeTag: false);
        }
        catch (Exception)
        {
            // Игнорируем ошибки при shutdown
        }
    }
}
