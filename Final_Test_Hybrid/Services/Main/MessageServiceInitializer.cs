namespace Final_Test_Hybrid.Services.Main;

using Common;
using SpringBoot.Operator;

public class MessageServiceInitializer(
    MessageService messageService,
    OperatorState operatorState,
    AutoReadySubscription autoReadySubscription)
{
    private readonly StateChangeAggregator _stateAggregator = new();

    public async Task InitializeAsync()
    {
        RegisterLoginProvider();
        RegisterAutoReadyProvider();
        await autoReadySubscription.SubscribeAsync();
        SubscribeToStateChanges();
    }

    private void SubscribeToStateChanges()
    {
        _stateAggregator.Subscribe(operatorState, autoReadySubscription);
        _stateAggregator.OnAnyStateChanged += messageService.NotifyChanged;
    }

    private void RegisterLoginProvider()
    {
        messageService.RegisterProvider(50, () =>
            !operatorState.IsAuthenticated ? "Войдите в систему" : null);
    }

    private void RegisterAutoReadyProvider()
    {
        messageService.RegisterProvider(40, () =>
            operatorState.IsAuthenticated && !autoReadySubscription.IsReady
                ? "Ожидание автомата"
                : null);
    }
}
