using Final_Test_Hybrid.Components.Engineer.Modals;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Main;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class OperatorAuthorizationQr : IDisposable
{
    [Inject]
    public required AppSettingsService AppSettingsService { get; set; }
    [Inject]
    public required DialogService DialogService { get; set; }
    [Inject]
    public required SettingsInteractionState InteractionState { get; set; }
    private bool _useOperatorQrAuth;

    protected override void OnInitialized()
    {
        _useOperatorQrAuth = AppSettingsService.UseOperatorQrAuth;
        InteractionState.OnChange += HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task OnCheckboxClick()
    {
        if (!InteractionState.CanInteract)
        {
            return;
        }
        var result = await ShowPasswordDialog();
        if (!result)
        {
            return;
        }
        _useOperatorQrAuth = !_useOperatorQrAuth;
        AppSettingsService.SaveUseOperatorQrAuth(_useOperatorQrAuth);
    }

    private async Task<bool> ShowPasswordDialog()
    {
        var result = await DialogService.OpenAsync<PasswordDialog>("Введите пароль",
            new Dictionary<string, object>(),
            new DialogOptions { Width = "350px", CloseDialogOnOverlayClick = false });
        return result is true;
    }

    public void Dispose()
    {
        InteractionState.OnChange -= HandleStateChanged;
    }
}
