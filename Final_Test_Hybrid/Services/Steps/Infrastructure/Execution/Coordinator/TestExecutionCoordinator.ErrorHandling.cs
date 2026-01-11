using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    private void HandleExecutorStateChanged()
    {
        OnStateChanged?.Invoke();
        EnqueueFailedExecutors();
    }

    private void EnqueueFailedExecutors()
    {
        if (StateManager.State != ExecutionState.Running)
        {
            return;
        }

        lock (_enqueueLock)
        {
            foreach (var executor in _executors.Where(e => e.HasFailed))
            {
                var error = CreateErrorFromExecutor(executor);
                StateManager.EnqueueError(error);
            }
        }
    }

    private static StepError CreateErrorFromExecutor(ColumnExecutor executor)
    {
        return new StepError(
            executor.ColumnIndex,
            executor.CurrentStepName ?? "Неизвестный шаг",
            executor.CurrentStepDescription ?? "",
            executor.ErrorMessage ?? "Неизвестная ошибка",
            DateTime.Now,
            Guid.Empty,
            executor.FailedStep);
    }

    private async Task HandleErrorsIfAny()
    {
        if (_cts == null)
        {
            _logger.LogWarning("HandleErrorsIfAny вызван без активного CancellationTokenSource");
            return;
        }

        while (StateManager.HasPendingErrors && !IsCancellationRequested)
        {
            var error = StateManager.CurrentError;
            if (error == null)
            {
                break;
            }
            StateManager.TransitionTo(ExecutionState.PausedOnError);
            await SetSelectedAsync(error, true);
            OnErrorOccurred?.Invoke(error);
            ErrorResolution resolution;
            try
            {
                resolution = await _errorCoordinator.WaitForResolutionAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                await SetSelectedAsync(error, false);
                break;
            }

            if (resolution == ErrorResolution.Timeout)
            {
                await _errorCoordinator.HandleInterruptAsync(InterruptReason.TagTimeout);
                await _cts?.CancelAsync()!;
                break;
            }
            await ProcessErrorResolution(error, resolution, _cts.Token);
            await SetSelectedAsync(error, false);
        }

        if (!IsCancellationRequested)
        {
            StateManager.TransitionTo(ExecutionState.Running);
        }
    }

    private async Task SetSelectedAsync(StepError error, bool value)
    {
        if (error.FailedStep is not IHasPlcBlock plcStep)
        {
            return;
        }
        var selectedTag = $"ns=3;s=\"{plcStep.PlcBlockPath}\".\"Selected\"";
        _logger.LogDebug("Установка Selected={Value} для {BlockPath}", value, plcStep.PlcBlockPath);

        var result = await _plcService.WriteAsync(selectedTag, value);
        if (result.Error != null)
        {
            _logger.LogWarning("Ошибка записи Selected: {Error}", result.Error);
        }
    }

    private async Task ProcessErrorResolution(StepError error, ErrorResolution resolution, CancellationToken ct)
    {
        ColumnExecutor executor;
        lock (_enqueueLock)
        {
            executor = _executors[error.ColumnIndex];
        }

        if (resolution == ErrorResolution.Retry)
        {
            await ProcessRetryAsync(executor, ct);
        }
        else
        {
            ProcessSkip(executor);
        }
    }

    private async Task ProcessRetryAsync(ColumnExecutor executor, CancellationToken ct)
    {
        await _errorCoordinator.SendAskRepeatAsync(ct);
        await executor.RetryLastFailedStepAsync(ct);
        if (!executor.HasFailed)
        {
            StateManager.DequeueError();
        }
        // Если снова ошибка — останется в очереди, покажем снова
    }

    private void ProcessSkip(ColumnExecutor executor)
    {
        executor.ClearFailedState();
        StateManager.DequeueError();
    }
}
