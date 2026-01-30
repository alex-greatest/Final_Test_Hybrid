namespace Final_Test_Hybrid.Services.Main;

using Common;
using Models.Plc.Tags;
using OpcUa.Subscription;

public class AutoReadySubscription(OpcUaSubscription opcUaSubscription) : INotifyStateChanged
{
    private volatile bool _isReady;
    private volatile bool _hasEverBeenReady;

    public bool IsReady => _isReady;
    public bool HasEverBeenReady => _hasEverBeenReady;

    public event Action? OnStateChanged;
    public event Action? OnFirstAutoReceived;

    public async Task SubscribeAsync()
    {
        await opcUaSubscription.SubscribeAsync(BaseTags.TestAskAuto, async value =>
        {
            var wasReady = _isReady;
            _isReady = value is true;

            if (_isReady && !_hasEverBeenReady)
            {
                _hasEverBeenReady = true;
                OnFirstAutoReceived?.Invoke();
            }

            OnStateChanged?.Invoke();
            await Task.CompletedTask;
        });
    }

    /// <summary>
    /// Сбрасывает флаг первого получения автомата.
    /// Вызывается при сбросе или завершении теста.
    /// </summary>
    public void ResetFirstAutoFlag()
    {
        _hasEverBeenReady = false;
    }
}
