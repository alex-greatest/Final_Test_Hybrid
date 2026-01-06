using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Microsoft.AspNetCore.Components;

namespace Final_Test_Hybrid.Components.Errors;

public partial class ErrorHandlingDialog : ComponentBase, IDisposable
{
    private const string DefaultStepName = "Неизвестный шаг";
    private const string DefaultErrorMessage = "Неизвестная ошибка";
    [Parameter] public StepError? Error { get; set; }
    [Parameter] public ExecutionStateManager? StateManager { get; set; }
    [Inject] private Radzen.DialogService DialogService { get; set; } = null!;
    private ExecutionState _currentState = ExecutionState.PausedOnError;
    public string StepName => Error?.StepName ?? DefaultStepName;
    public string ErrorMessage => Error?.ErrorMessage ?? DefaultErrorMessage;
    public int ColumnIndex => Error?.ColumnIndex ?? 0;

    protected override void OnInitialized()
    {
        if (StateManager == null)
        {
            return;
        }

        _currentState = StateManager.State;
        StateManager.OnStateChanged += HandleStateChanged;
    }

    public void Dispose()
    {
        if (StateManager != null)
        {
            StateManager.OnStateChanged -= HandleStateChanged;
        }
    }

    private async void HandleStateChanged(ExecutionState newState)
    {
        try
        {
            _currentState = newState;
            if (IsDialogClosingState(newState))
            {
                DialogService.Close(this);
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

    private string GetRetryClass()
    {
        return "";
    }

    private string GetSkipClass()
    {
        return "";
    }
}
