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
    public required SettingsAccessStateManager SettingsAccessState { get; set; }
    private bool _useOperatorQrAuth;

    protected override void OnInitialized()
    {
        _useOperatorQrAuth = AppSettingsService.UseOperatorQrAuth;
        SettingsAccessState.OnStateChanged += HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task OnCheckboxClick()
    {
        if (!SettingsAccessState.CanInteract)
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
        SettingsAccessState.OnStateChanged -= HandleStateChanged;
    }
}
