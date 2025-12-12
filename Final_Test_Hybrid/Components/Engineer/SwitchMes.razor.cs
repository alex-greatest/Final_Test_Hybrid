using Final_Test_Hybrid.Components.Engineer.Modals;
using Final_Test_Hybrid.Services.Common.Settings;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class SwitchMes
{
    [Inject]
    public required AppSettingsService AppSettingsService { get; set; }
    [Inject]
    public required DialogService DialogService { get; set; }
    private bool _useMes;

    protected override void OnInitialized()
    {
        _useMes = AppSettingsService.UseMes;
    }

    private async Task OnSwitchClick()
    {
        var result = await ShowPasswordDialog();
        if (!result)
        {
            return;
        }
        _useMes = !_useMes;
        AppSettingsService.SaveUseMes(_useMes);
    }

    private async Task<bool> ShowPasswordDialog()
    {
        var result = await DialogService.OpenAsync<PasswordDialog>("Введите пароль",
            new Dictionary<string, object>(),
            new DialogOptions { Width = "350px", CloseDialogOnOverlayClick = false });
        return result is true;
    }
}
