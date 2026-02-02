using Final_Test_Hybrid.Components.Engineer.Modals;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class SwitchInterruptReason
{
    [Inject]
    public required AppSettingsService AppSettingsService { get; set; }
    [Inject]
    public required DialogService DialogService { get; set; }
    [Inject]
    public required PreExecutionCoordinator PreExecution { get; set; }
    [Inject]
    public required SettingsAccessStateManager SettingsAccessState { get; set; }
    [Inject]
    public required PlcResetCoordinator PlcResetCoordinator { get; set; }
    [Inject]
    public required IErrorCoordinator ErrorCoordinator { get; set; }

    private bool _useInterruptReason;

    private bool IsDisabled => PreExecution.IsProcessing
        || !SettingsAccessState.CanInteract
        || PlcResetCoordinator.IsActive
        || ErrorCoordinator.CurrentInterrupt != null;

    protected override void OnInitialized()
    {
        _useInterruptReason = AppSettingsService.UseInterruptReason;
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
        var result = await ShowPasswordDialog();
        if (!result)
        {
            return;
        }
        _useInterruptReason = !_useInterruptReason;
        AppSettingsService.SaveUseInterruptReason(_useInterruptReason);
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
        PreExecution.OnStateChanged -= HandleStateChanged;
        SettingsAccessState.OnStateChanged -= HandleStateChanged;
        PlcResetCoordinator.OnActiveChanged -= HandleStateChanged;
        ErrorCoordinator.OnInterruptChanged -= HandleStateChanged;
    }
}
