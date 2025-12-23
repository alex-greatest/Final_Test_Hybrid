namespace Final_Test_Hybrid.Services.Main;

using SpringBoot.Operator;

public class MessageServiceInitializer(
    MessageService messageService,
    OperatorState operatorState,
    AutoReadySubscription autoReadySubscription)
{
    public void Initialize()
    {
        RegisterLoginProvider();
        RegisterAutoReadyProvider();
        autoReadySubscription.Subscribe();
        operatorState.OnChange += messageService.NotifyChanged;
        autoReadySubscription.OnChange += messageService.NotifyChanged;
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
