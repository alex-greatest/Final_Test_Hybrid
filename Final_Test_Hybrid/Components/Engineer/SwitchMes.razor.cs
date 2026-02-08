using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class SwitchMes
{
    [Inject]
    public required AppSettingsService AppSettingsService { get; set; }
    [Inject]
    public required OperatorState OperatorState { get; set; }
    [Inject]
    public required OperatorAuthService OperatorAuthService { get; set; }
    [Inject]
    public required NotificationService NotificationService { get; set; }
    [Inject]
    public required PreExecutionCoordinator PreExecution { get; set; }
    [Inject]
    public required SettingsAccessStateManager SettingsAccessState { get; set; }
    [Inject]
    public required PlcResetCoordinator PlcResetCoordinator { get; set; }
    [Inject]
    public required IErrorCoordinator ErrorCoordinator { get; set; }
    private bool _useMes;

    private bool IsDisabled => PreExecution.IsProcessing
        || !SettingsAccessState.CanInteract
        || PlcResetCoordinator.IsActive
        || ErrorCoordinator.CurrentInterrupt != null;

    protected override void OnInitialized()
    {
        _useMes = AppSettingsService.UseMes;
        PreExecution.OnStateChanged += HandleStateChanged;
        SettingsAccessState.OnStateChanged += HandleStateChanged;
        PlcResetCoordinator.OnActiveChanged += HandleStateChanged;
        ErrorCoordinator.OnInterruptChanged += HandleStateChanged;
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

    public void Dispose()
    {
        PreExecution.OnStateChanged -= HandleStateChanged;
        SettingsAccessState.OnStateChanged -= HandleStateChanged;
        PlcResetCoordinator.OnActiveChanged -= HandleStateChanged;
        ErrorCoordinator.OnInterruptChanged -= HandleStateChanged;
    }
}
