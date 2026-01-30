using Final_Test_Hybrid.Models;

namespace Final_Test_Hybrid.Services.Main;

public class MessageServiceInitializer(AutoReadySubscription autoReadySubscription, BoilerState boilerState)
{
    public async Task InitializeAsync()
    {
        autoReadySubscription.OnFirstAutoReceived += () => boilerState.StartChangeoverTimer();
        await autoReadySubscription.SubscribeAsync();
    }
}
