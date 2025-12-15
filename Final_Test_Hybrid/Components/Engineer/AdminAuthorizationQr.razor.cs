using Final_Test_Hybrid.Components.Engineer.Modals;
using Final_Test_Hybrid.Services.Common.Settings;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class AdminAuthorizationQr
{
    [Inject]
    public required AppSettingsService AppSettingsService { get; set; }
    [Inject]
    public required DialogService DialogService { get; set; }
    private bool _useAdminQrAuth;

    protected override void OnInitialized()
    {
        _useAdminQrAuth = AppSettingsService.UseAdminQrAuth;
    }

    private async Task OnCheckboxClick()
    {
        var result = await ShowPasswordDialog();
        if (!result)
        {
            return;
        }
        _useAdminQrAuth = !_useAdminQrAuth;
        AppSettingsService.SaveUseAdminQrAuth(_useAdminQrAuth);
    }

    private async Task<bool> ShowPasswordDialog()
    {
        var result = await DialogService.OpenAsync<PasswordDialog>("Введите пароль",
            new Dictionary<string, object>(),
            new DialogOptions { Width = "350px", CloseDialogOnOverlayClick = false });
        return result is true;
    }
}
