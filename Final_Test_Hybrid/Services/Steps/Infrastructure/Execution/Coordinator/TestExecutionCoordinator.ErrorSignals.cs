using System.Threading;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    private int _errorDetectedQueued;
    private Task? _errorDrainTask;
    private readonly Lock _errorDrainLock = new();

    /// <summary>
    /// Публикует сигнал об ошибках с коалесцированием.
    /// </summary>
    private void QueueErrorDetected()
    {
        if (Interlocked.Exchange(ref _errorDetectedQueued, 1) == 1)
        {
            return;
        }
        TryPublishEvent(new ExecutionEvent(ExecutionEventKind.ErrorDetected));
    }

    /// <summary>
    /// Запускает фоновой drain обработки ошибок, если он ещё не работает.
    /// </summary>
    private void EnsureErrorDrainStarted()
    {
        lock (_errorDrainLock)
        {
            if (_errorDrainTask == null || _errorDrainTask.IsCompleted)
            {
                _errorDrainTask = DrainErrorsSafelyAsync();
            }
        }
    }

    /// <summary>
    /// Фоново обрабатывает ошибки из очереди.
    /// </summary>
    private async Task DrainErrorsSafelyAsync()
    {
        try
        {
            await HandleErrorsIfAny();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HandleUnhandledException(ex, "обработке ошибок");
        }
        finally
        {
            FinalizeErrorDrain();
        }
    }

    /// <summary>
    /// Завершает drain и при необходимости запускает его снова.
    /// </summary>
    private void FinalizeErrorDrain()
    {
        Interlocked.Exchange(ref _errorDetectedQueued, 0);
        if (StateManager.HasPendingErrors && !_flowState.IsStopRequested && !IsCancellationRequested)
        {
            QueueErrorDetected();
        }
    }
}
