namespace Final_Test_Hybrid.Services.Main;

using Models.Plc.Tags;
using OpcUa.Subscription;

public class AutoReadySubscription(OpcUaSubscription opcUaSubscription)
{
    public bool IsReady { get; private set; }
    public event Action? OnChange;

    public async Task SubscribeAsync()
    {
        await opcUaSubscription.SubscribeAsync(BaseTags.TestAskAuto, async value =>
        {
            IsReady = value is true;
            OnChange?.Invoke();
            await Task.CompletedTask;
        });
    }
}
