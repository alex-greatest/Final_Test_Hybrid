using Final_Test_Hybrid.Components.Engineer.Modals;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class SwitchMes
{
    [Inject]
    public required AppSettingsService AppSettingsService { get; set; }
    [Inject]
    public required DialogService DialogService { get; set; }
    [Inject]
    public required OperatorState OperatorState { get; set; }
    [Inject]
    public required OperatorAuthService OperatorAuthService { get; set; }
    [Inject]
    public required NotificationService NotificationService { get; set; }
    [Inject]
    public required ScanStepManager ScanStepManager { get; set; }
    [Inject]
    public required SettingsInteractionState InteractionState { get; set; }
    private bool _useMes;

    private bool IsDisabled => ScanStepManager.IsProcessing || !InteractionState.CanInteract;

    protected override void OnInitialized()
    {
        _useMes = AppSettingsService.UseMes;
        ScanStepManager.OnChange += HandleStateChanged;
        InteractionState.OnChange += HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task OnSwitchClick()
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
        if (!await TryLogoutBeforeModeSwitch())
        {
            return;
        }
        _useMes = !_useMes;
        AppSettingsService.SaveUseMes(_useMes);
    }

    private async Task<bool> TryLogoutBeforeModeSwitch()
    {
        if (!OperatorState.IsAuthenticated)
        {
            return true;
        }
        if (_useMes)
        {
            return await TryMesLogout();
        }
        ManualLogoutOnModeSwitch();
        return true;
    }

    private async Task<bool> TryMesLogout()
    {
        var logoutResult = await OperatorAuthService.LogoutAsync();
        if (logoutResult.Success)
        {
            return true;
        }
        NotificationService.Notify(NotificationSeverity.Error, "Не удалось выйти: " + (logoutResult.ErrorMessage ?? "Неизвестная ошибка"));
        return false;
    }

    private void ManualLogoutOnModeSwitch()
    {
        OperatorAuthService.ManualLogout();
        NotificationService.Notify(NotificationSeverity.Info, "Выход из под аккаунта администратора выполнен при смене режима");
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
        ScanStepManager.OnChange -= HandleStateChanged;
        InteractionState.OnChange -= HandleStateChanged;
    }
}
