namespace Final_Test_Hybrid.Services.Main;

using Common;
using Models.Plc.Tags;
using OpcUa.Subscription;

public class AutoReadySubscription(OpcUaSubscription opcUaSubscription) : INotifyStateChanged
{
    private volatile bool _isReady;
    public bool IsReady => _isReady;
    public event Action? OnStateChanged;

    public async Task SubscribeAsync()
    {
        await opcUaSubscription.SubscribeAsync(BaseTags.TestAskAuto, async value =>
        {
            _isReady = value is true;
            OnStateChanged?.Invoke();
            await Task.CompletedTask;
        });
    }
}
