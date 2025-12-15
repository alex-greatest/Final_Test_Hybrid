using Final_Test_Hybrid.Components.Engineer.Modals;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class MainEngineering
{
    [Inject]
    public required DialogService DialogService { get; set; }

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

    private Task OnHandProgram()
    {
        return Task.CompletedTask;
    }

    private Task OnIoEditor()
    {
        return Task.CompletedTask;
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
}