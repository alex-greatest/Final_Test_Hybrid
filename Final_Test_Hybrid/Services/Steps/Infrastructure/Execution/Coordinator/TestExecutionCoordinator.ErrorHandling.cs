using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    private void HandleExecutorStateChanged()
    {
        UpdateHasErrorsState();
        OnStateChanged?.Invoke();
        CheckForErrors();
    }

    private void UpdateHasErrorsState()
    {
        StateManager.SetHasErrors(_executors.Any(e => e.HasFailed));
    }

    private void CheckForErrors()
    {
        var failedExecutor = GetFirstFailedExecutorIfRunning();
        if (failedExecutor == null)
        {
            return;
        }
        ReportError(failedExecutor);
    }

    private ColumnExecutor? GetFirstFailedExecutorIfRunning()
    {
        return StateManager.State != ExecutionState.Running ? null : _executors.FirstOrDefault(e => e.HasFailed);
    }

    private void ReportError(ColumnExecutor executor)
    {
        var error = new StepError(
            executor.ColumnIndex,
            executor.CurrentStepName ?? "Неизвестный шаг",
            executor.CurrentStepDescription ?? "",
            executor.ErrorMessage ?? "Неизвестная ошибка",
            DateTime.Now,
            Guid.Empty);
        StateManager.TransitionTo(ExecutionState.PausedOnError, error);
        lock (_stateLock)
        {
            _errorResolutionTcs = new TaskCompletionSource<ErrorResolution>();
        }
        OnErrorOccurred?.Invoke(error);
    }

    private void HandleErrorResolution(ErrorResolution resolution)
    {
        lock (_stateLock)
        {
            _errorResolutionTcs?.TrySetResult(resolution);
        }
    }

    private async Task HandleErrorsIfAny()
    {
        while (ShouldContinueErrorHandling())
        {
            var resolution = await WaitForErrorResolution();
            await ProcessErrorResolution(resolution);
        }
    }

    private bool ShouldContinueErrorHandling()
    {
        return HasErrors && !IsCancellationRequested;
    }

    private async Task<ErrorResolution> WaitForErrorResolution()
    {
        Task<ErrorResolution>? taskToAwait;
        lock (_stateLock)
        {
            taskToAwait = _errorResolutionTcs?.Task;
        }
        if (taskToAwait == null)
        {
            return ErrorResolution.None;
        }
        return await taskToAwait;
    }

    private async Task ProcessErrorResolution(ErrorResolution resolution)
    {
        switch (resolution)
        {
            case ErrorResolution.Retry:
                await RetryFailedSteps();
                break;
            case ErrorResolution.Skip:
                SkipFailedSteps();
                break;
            case ErrorResolution.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null);
        }
    }

    private async Task RetryFailedSteps()
    {
        var failedExecutors = _executors.Where(e => e.HasFailed).ToList();
        var retryTasks = failedExecutors.Select(e => e.RetryLastFailedStepAsync(_cts!.Token));
        await Task.WhenAll(retryTasks);
    }

    private void SkipFailedSteps()
    {
        foreach (var executor in _executors.Where(e => e.HasFailed))
        {
            executor.ClearFailedState();
        }
        StateManager.TransitionTo(ExecutionState.Running);
    }
}
