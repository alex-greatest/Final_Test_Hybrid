using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    /// <summary>
    /// Обрабатывает изменение состояния executor'а.
    /// </summary>
    private void HandleExecutorStateChanged()
    {
        TryPublishEvent(new ExecutionEvent(ExecutionEventKind.StateChanged));
        EnqueueFailedExecutors();
    }

    /// <summary>
    /// Безопасно вызывает событие OnStateChanged.
    /// </summary>
    private void InvokeStateChangedSafely()
    {
        try
        {
            OnStateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка в обработчике OnStateChanged");
        }
    }

    /// <summary>
    /// Добавляет ошибки упавших колонок в очередь.
    /// </summary>
    private void EnqueueFailedExecutors()
    {
        var state = StateManager.State;
        if (state != ExecutionState.Running && state != ExecutionState.PausedOnError)
        {
            return;
        }

        lock (_enqueueLock)
        {
            var hadErrors = StateManager.HasPendingErrors;
            foreach (var executor in _executors.Where(e => e.HasFailed))
            {
                var error = CreateErrorFromExecutor(executor);
                StateManager.EnqueueError(error);
            }
            if (!hadErrors && StateManager.HasPendingErrors)
            {
                QueueErrorDetected();
            }
        }
    }

    /// <summary>
    /// Создаёт объект ошибки из состояния executor'а.
    /// </summary>
    private static StepError CreateErrorFromExecutor(ColumnExecutor executor)
    {
        return new StepError(
            ColumnIndex: executor.ColumnIndex,
            StepName: executor.CurrentStepName ?? "Неизвестный шаг",
            StepDescription: executor.CurrentStepDescription ?? "",
            ErrorMessage: executor.ErrorMessage ?? "Неизвестная ошибка",
            ErrorSourceTitle: executor.FailedStep?.ErrorSourceTitle ?? ErrorSourceDefaults.Stand,
            OccurredAt: DateTime.Now,
            UiStepId: executor.UiStepId,
            FailedStep: executor.FailedStep,
            CanSkip: executor.CanSkip);
    }
}
