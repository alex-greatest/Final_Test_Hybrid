using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Microsoft.AspNetCore.Components;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class SwitchExcelExport
{
    [Inject]
    public required AppSettingsService AppSettingsService { get; set; }

    [Inject]
    public required PreExecutionCoordinator PreExecution { get; set; }

    [Inject]
    public required SettingsAccessStateManager SettingsAccessState { get; set; }

    [Inject]
    public required PlcResetCoordinator PlcResetCoordinator { get; set; }

    [Inject]
    public required IErrorCoordinator ErrorCoordinator { get; set; }

    private bool _exportEnabled;

    private bool IsDisabled => PreExecution.IsProcessing
        || !SettingsAccessState.CanInteract
        || PlcResetCoordinator.IsActive
        || ErrorCoordinator.CurrentInterrupt != null;

    protected override void OnInitialized()
    {
        _exportEnabled = AppSettingsService.ExportStepsToExcel;
        PreExecution.OnStateChanged += HandleStateChanged;
        SettingsAccessState.OnStateChanged += HandleStateChanged;
        PlcResetCoordinator.OnActiveChanged += HandleStateChanged;
        ErrorCoordinator.OnInterruptChanged += HandleStateChanged;
    }

    /// <summary>
    /// Обработчик изменения состояния.
    /// </summary>
    private void HandleStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Обработчик клика по переключателю.
    /// </summary>
    private Task OnSwitchClick()
    {
        if (IsDisabled)
        {
            return Task.CompletedTask;
        }
        _exportEnabled = !_exportEnabled;
        AppSettingsService.SaveExportStepsToExcel(_exportEnabled);
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
