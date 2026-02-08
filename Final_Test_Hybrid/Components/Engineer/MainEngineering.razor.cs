using Final_Test_Hybrid.Components.Engineer.Modals;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class MainEngineering : IDisposable
{
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

    private bool IsMainSettingsDisabled => PreExecution.IsProcessing
        || !SettingsAccessState.CanInteract
        || PlcResetCoordinator.IsActive
        || ErrorCoordinator.CurrentInterrupt != null;

    protected override void OnInitialized()
    {
        PreExecution.OnStateChanged += HandleStateChanged;
        SettingsAccessState.OnStateChanged += HandleStateChanged;
        PlcResetCoordinator.OnActiveChanged += HandleStateChanged;
        ErrorCoordinator.OnInterruptChanged += HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task OnButtonClick(Func<Task> action)
    {
        var result = await ShowPasswordDialog();
        if (!result)
        {
            return;
        }
        await action();
    }

    private async Task<bool> ShowPasswordDialog()
    {
        var result = await DialogService.OpenAsync<PasswordDialog>("Введите пароль",
            new Dictionary<string, object>(),
            new DialogOptions { Width = "350px", CloseDialogOnOverlayClick = false });
        return result is true;
    }

    private async Task OnHandProgram()
    {
        if (IsMainSettingsDisabled)
        {
            return;
        }

        await DialogService.OpenAsync<HandProgramDialog>("Hand Program",
            new Dictionary<string, object>(),
            new DialogOptions
            {
                Width = "95vw",
                Height = "95vh",
                Resizable = true,
                Draggable = true,
                CssClass = "hand-program-dialog",
                CloseDialogOnOverlayClick = false
            });
    }

    private async Task OnIoEditor()
    {
        if (IsMainSettingsDisabled)
        {
            return;
        }

        await DialogService.OpenAsync<IoEditorDialog>("IO Editor",
            new Dictionary<string, object>(),
            new DialogOptions
            {
                Width = "95vw",
                Height = "95vh",
                Resizable = true,
                Draggable = true,
                CssClass = "io-editor-dialog",
                CloseDialogOnOverlayClick = false
            });
    }

    private async Task OpenMainSettingsDialog()
    {
        await DialogService.OpenAsync<MainSettingsDialog>("Основные настройки",
            new Dictionary<string, object>(),
            new DialogOptions
            {
                Width = "760px",
                Height = "520px",
                Resizable = false,
                Draggable = true,
                CssClass = "main-settings-dialog",
                CloseDialogOnOverlayClick = false
            });
    }

    private Task OnAiRtdCorrection()
    {
        return Task.CompletedTask;
    }

    private Task OnDisableTestLog()
    {
        return Task.CompletedTask;
    }

    private Task OnTestLogViewer()
    {
        return Task.CompletedTask;
    }

    private async Task OpenTestSequenceEditor()
    {
        await DialogService.OpenAsync<Sequence.TestSequenceEditor>("Редактор шагов теста",
            new Dictionary<string, object>(),
            new DialogOptions
            {
                Width = "95vw",
                Height = "95vh",
                Resizable = true,
                Draggable = true,
                CssClass = "test-sequence-editor-dialog",
                CloseDialogOnOverlayClick = false
            });
    }

    private Task OnViewCharts()
    {
        return Task.CompletedTask;
    }

    private async Task OpenStandDatabase()
    {
        await DialogService.OpenAsync<StandDatabase.StandDatabaseDialog>("База данных стенда",
            new Dictionary<string, object>(),
            new DialogOptions
            {
                Width = "95vw",
                Height = "95vh",
                Resizable = true,
                Draggable = true,
                CssClass = "stand-database-dialog",
                CloseDialogOnOverlayClick = false
            });
    }

    public void Dispose()
    {
        PreExecution.OnStateChanged -= HandleStateChanged;
        SettingsAccessState.OnStateChanged -= HandleStateChanged;
        PlcResetCoordinator.OnActiveChanged -= HandleStateChanged;
        ErrorCoordinator.OnInterruptChanged -= HandleStateChanged;
    }
}
