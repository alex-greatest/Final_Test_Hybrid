using Final_Test_Hybrid.Components.Engineer.Modals;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class AdminAuthorizationQr : IDisposable
{
    [Inject]
    public required AppSettingsService AppSettingsService { get; set; }
    [Inject]
    public required DialogService DialogService { get; set; }
    [Inject]
    public required SettingsAccessStateManager SettingsAccessState { get; set; }
    [Inject]
    public required PlcResetCoordinator PlcResetCoordinator { get; set; }
    [Inject]
    public required ErrorCoordinator ErrorCoordinator { get; set; }

    private bool _useAdminQrAuth;

    private bool IsDisabled => !SettingsAccessState.CanInteract
        || PlcResetCoordinator.IsActive
        || ErrorCoordinator.CurrentInterrupt != null;

    protected override void OnInitialized()
    {
        _useAdminQrAuth = AppSettingsService.UseAdminQrAuth;
        SettingsAccessState.OnStateChanged += HandleStateChanged;
        PlcResetCoordinator.OnActiveChanged += HandleStateChanged;
        ErrorCoordinator.OnInterruptChanged += HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task OnCheckboxClick()
    {
        if (IsDisabled)
        {
            return;
        }
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

    public void Dispose()
    {
        SettingsAccessState.OnStateChanged -= HandleStateChanged;
        PlcResetCoordinator.OnActiveChanged -= HandleStateChanged;
        ErrorCoordinator.OnInterruptChanged -= HandleStateChanged;
    }
}
