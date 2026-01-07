namespace Final_Test_Hybrid.Services.Main;

using Common;
using Models.Plc.Tags;
using OpcUa.Subscription;

public class AutoReadySubscription(OpcUaSubscription opcUaSubscription) : INotifyStateChanged
{
    public bool IsReady { get; private set; }
    public event Action? OnStateChanged;

    public async Task SubscribeAsync()
    {
        await opcUaSubscription.SubscribeAsync(BaseTags.TestAskAuto, async value =>
        {
            IsReady = value is true;
            OnStateChanged?.Invoke();
            await Task.CompletedTask;
        });
    }
}
