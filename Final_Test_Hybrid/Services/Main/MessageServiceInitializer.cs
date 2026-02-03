using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

namespace Final_Test_Hybrid.Services.Main;

public class MessageServiceInitializer(AutoReadySubscription autoReadySubscription, IChangeoverStartGate changeoverStartGate)
{
    public async Task InitializeAsync()
    {
        autoReadySubscription.OnFirstAutoReceived += changeoverStartGate.RequestStartFromAutoReady;
        await autoReadySubscription.SubscribeAsync();
    }
}
