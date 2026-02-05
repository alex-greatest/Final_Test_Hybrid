using Final_Test_Hybrid.Models.Steps;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    /// <summary>
    /// Логирует необработанное исключение.
    /// </summary>
    private void LogUnhandledException(Exception ex)
    {
        _logger.LogError(ex, "Необработанная ошибка в TestExecutionCoordinator");
        _testLogger.LogError(ex, "Критическая ошибка выполнения тестов");
    }

    /// <summary>
    /// Обрабатывает необработанное исключение как критическую остановку.
    /// </summary>
    private void HandleUnhandledException(Exception ex, string context)
    {
        LogUnhandledException(ex);
        StopAsFailure(ExecutionStopReason.Operator, $"Необработанное исключение в {context}");
    }

    /// <summary>
    /// Запускает событийный цикл выполнения.
    /// </summary>
    private async Task RunEventLoopAsync()
    {
        var channel = StartEventChannel();
        await PublishEventCritical(new ExecutionEvent(ExecutionEventKind.StartRequested));
        await DispatchStartEventAsync(channel.Reader);
        var readerTask = RunEventReaderAsync(channel.Reader);
        var executionTask = RunAllMaps();
        await AwaitExecutionAsync(executionTask);
        var token = GetCancellationToken();
        if (!_flowState.IsStopRequested && !token.IsCancellationRequested)
        {
            try
            {
                await WaitForMapSettlementAsync(token);
            }
            catch (OperationCanceledException)
            {
            }
        }
        CompleteEventChannel();
        await AwaitReaderAsync(readerTask);
        await AwaitPendingRetriesSafelyAsync();
        await AwaitErrorDrainSafelyAsync();
        Complete();
    }

    /// <summary>
    /// Обрабатывает событие выполнения.
    /// </summary>
    private void DispatchEvent(ExecutionEvent evt)
    {
        switch (evt.Kind)
        {
            case ExecutionEventKind.StartRequested:
                ApplyStartRequested();
                break;
            case ExecutionEventKind.StateChanged:
                InvokeStateChangedSafely();
                break;
            case ExecutionEventKind.ErrorOccurred:
                InvokeErrorOccurredSafely(evt.StepError!);
                break;
            case ExecutionEventKind.ErrorDetected:
                EnsureErrorDrainStarted();
                break;
            case ExecutionEventKind.RetryStarted:
                InvokeRetryStartedSafely();
                break;
            case ExecutionEventKind.RetryRequested:
                HandleRetryRequested(evt);
                break;
            case ExecutionEventKind.RetryCompleted:
                break;
            case ExecutionEventKind.SequenceCompleted:
                InvokeSequenceCompletedSafely();
                break;
        }
    }

    /// <summary>
    /// Обрабатывает запрос повтора шага из event loop.
    /// </summary>
    private void HandleRetryRequested(ExecutionEvent evt)
    {
        if (evt.StepError == null || evt.ColumnExecutor == null)
        {
            return;
        }

        var token = GetCancellationToken();
        var retryTask = ExecuteRetryInBackgroundAsync(evt.StepError, evt.ColumnExecutor, token);
        TrackRetryTask(retryTask);
    }

    /// <summary>
    /// Считывает первое событие и синхронно применяет его.
    /// </summary>
    private async Task DispatchStartEventAsync(ChannelReader<ExecutionEvent> reader)
    {
        try
        {
            var evt = await reader.ReadAsync();
            DispatchEvent(evt);
        }
        catch (Exception ex)
        {
            HandleUnhandledException(ex, "обработке старта");
        }
    }

    /// <summary>
    /// Читает канал и обрабатывает события до завершения.
    /// </summary>
    private async Task RunEventReaderAsync(ChannelReader<ExecutionEvent> reader)
    {
        await foreach (var evt in reader.ReadAllAsync())
        {
            DispatchEvent(evt);
        }
    }

    /// <summary>
    /// Ожидает завершения выполнения с перехватом ошибок.
    /// </summary>
    private async Task AwaitExecutionAsync(Task executionTask)
    {
        var cts = _cts;
        try
        {
            await executionTask;
        }
        catch (OperationCanceledException) when (_flowState.IsStopRequested)
        {
            // Ожидаемая отмена при штатной остановке (operator/PLC/reset/timeout).
        }
        catch (OperationCanceledException) when (cts?.IsCancellationRequested == true)
        {
            // Ожидаемая отмена по CTS текущего прогона.
        }
        catch (Exception ex)
        {
            HandleUnhandledException(ex, "выполнении карт");
        }
    }

    /// <summary>
    /// Ожидает завершения чтения событий с перехватом ошибок.
    /// </summary>
    private async Task AwaitReaderAsync(Task readerTask)
    {
        try
        {
            await readerTask;
        }
        catch (Exception ex)
        {
            HandleUnhandledException(ex, "чтении событий");
        }
    }

    /// <summary>
    /// Ожидает завершения всех повторов с перехватом ошибок.
    /// </summary>
    private async Task AwaitPendingRetriesSafelyAsync()
    {
        try
        {
            await AwaitPendingRetriesAsync();
        }
        catch (Exception ex)
        {
            HandleUnhandledException(ex, "ожидании повторов");
        }
    }

    /// <summary>
    /// Ожидает завершения фоновой обработки ошибок с перехватом ошибок.
    /// </summary>
    private async Task AwaitErrorDrainSafelyAsync()
    {
        var drainTask = _errorDrainTask;
        if (drainTask == null)
        {
            return;
        }
        try
        {
            await drainTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HandleUnhandledException(ex, "ожидании обработки ошибок");
        }
    }

    /// <summary>
    /// Применяет логику старта перед выполнением.
    /// </summary>
    private void ApplyStartRequested()
    {
        _flowState.ClearStop();
        Interlocked.Exchange(ref _errorDetectedQueued, 0);
        _errorDrainTask = null;
        _retryState.Reset();
        StateManager.ClearErrors();
        StateManager.ResetErrorTracking();
        StateManager.TransitionTo(ExecutionState.Running);
    }
}
