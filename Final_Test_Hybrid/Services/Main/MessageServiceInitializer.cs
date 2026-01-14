namespace Final_Test_Hybrid.Services.Main;

public class MessageServiceInitializer(AutoReadySubscription autoReadySubscription)
{
    public async Task InitializeAsync()
    {
        await autoReadySubscription.SubscribeAsync();
    }
}
