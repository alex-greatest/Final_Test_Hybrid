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
    private Func<object?, Task>? _callback;
    private volatile bool _disposed;
    public bool IsResetRequested { get; private set; }
    public event Action? OnStateChanged;

    public async Task SubscribeAsync()
    {
        if (_disposed) { return; }

        _callback = OnValueChanged;
        await opcUaSubscription.SubscribeAsync(BaseTags.ReqReset, _callback);
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
        if (_disposed) { return; }
        _disposed = true;

        if (_callback != null)
        {
            try
            {
                await opcUaSubscription.UnsubscribeAsync(BaseTags.ReqReset, _callback, removeTag: false);
            }
            catch (Exception)
            {
                // Игнорируем ошибки при shutdown
            }
            _callback = null;
        }

        OnStateChanged = null;
    }
}
