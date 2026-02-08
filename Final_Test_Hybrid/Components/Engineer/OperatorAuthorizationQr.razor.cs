using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Microsoft.AspNetCore.Components;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class OperatorAuthorizationQr : IDisposable
{
    [Inject]
    public required AppSettingsService AppSettingsService { get; set; }
    [Inject]
    public required SettingsAccessStateManager SettingsAccessState { get; set; }
    [Inject]
    public required PlcResetCoordinator PlcResetCoordinator { get; set; }
    [Inject]
    public required IErrorCoordinator ErrorCoordinator { get; set; }
    [Inject]
    public required PreExecutionCoordinator PreExecution { get; set; }

    private bool _useOperatorQrAuth;

    private bool IsDisabled => PreExecution.IsProcessing
        || !SettingsAccessState.CanInteract
        || PlcResetCoordinator.IsActive
        || ErrorCoordinator.CurrentInterrupt != null;

    protected override void OnInitialized()
    {
        _useOperatorQrAuth = AppSettingsService.UseOperatorQrAuth;
        PreExecution.OnStateChanged += HandleStateChanged;
        SettingsAccessState.OnStateChanged += HandleStateChanged;
        PlcResetCoordinator.OnActiveChanged += HandleStateChanged;
        ErrorCoordinator.OnInterruptChanged += HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private Task OnCheckboxClick()
    {
        if (IsDisabled)
        {
            return Task.CompletedTask;
        }
        _useOperatorQrAuth = !_useOperatorQrAuth;
        AppSettingsService.SaveUseOperatorQrAuth(_useOperatorQrAuth);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        PreExecution.OnStateChanged -= HandleStateChanged;
        SettingsAccessState.OnStateChanged -= HandleStateChanged;
        PlcResetCoordinator.OnActiveChanged -= HandleStateChanged;
        ErrorCoordinator.OnInterruptChanged -= HandleStateChanged;
    }
}
