using Final_Test_Hybrid.Models.Steps;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Final_Test_Hybrid.Components.Errors;

public partial class FloatingErrorPanel : ComponentBase, IAsyncDisposable
{
    private const string DefaultStepName = "Неизвестный шаг";

    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public string? StepName { get; set; }
    [Parameter] public string? ErrorMessage { get; set; }
    [Parameter] public int ColumnIndex { get; set; }
    [Parameter] public ExecutionStateManager? StateManager { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    private double _posX = -1;
    private double _posY = -1;

    public string DisplayStepName => StepName ?? DefaultStepName;

    public string PositionStyle
    {
        get
        {
            if (_posX < 0 || _posY < 0)
            {
                return "bottom: 20px; right: 20px;";
            }
            return $"left: {_posX}px; top: {_posY}px;";
        }
    }

    protected override void OnInitialized()
    {
        if (StateManager != null)
        {
            StateManager.OnStateChanged += HandleStateChanged;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (StateManager != null)
        {
            StateManager.OnStateChanged -= HandleStateChanged;
        }

        await ValueTask.CompletedTask;
    }

    private async void HandleStateChanged(ExecutionState newState)
    {
        try
        {
            if (IsDialogClosingState(newState))
            {
                await OnClose.InvokeAsync();
                return;
            }

            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            // ignored
        }
    }

    private static bool IsDialogClosingState(ExecutionState state)
    {
        return state is ExecutionState.Running
            or ExecutionState.Completed
            or ExecutionState.Failed;
    }

    private string GetStandName()
    {
        return $"[Стенд {ColumnIndex + 1}]";
    }

    private void StartDrag(Microsoft.AspNetCore.Components.Web.MouseEventArgs e)
    {
        _ = JSRuntime.InvokeVoidAsync("floatingPanel.startDrag", "floating-error-panel", e.ClientX, e.ClientY);
    }
}
