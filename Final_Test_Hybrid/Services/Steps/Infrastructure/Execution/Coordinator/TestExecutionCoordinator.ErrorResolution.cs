using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    /// <summary>
    /// Обрабатывает все ошибки в очереди.
    /// </summary>
    private async Task HandleErrorsIfAny()
    {
        var cts = _cts;
        if (cts == null)
        {
            _logger.LogWarning("HandleErrorsIfAny вызван без активного CancellationTokenSource");
            return;
        }

        var interruptedByConnectionLoss = false;
        while (CanHandleNextError(cts))
        {
            var error = StateManager.CurrentError;
            if (error == null)
            {
                break;
            }

            await PrepareErrorProcessingAsync(error, cts.Token);
            var resolution = await WaitResolutionSafeAsync(error, cts.Token);

            if (resolution == ErrorResolution.None)
            {
                break;
            }
            if (resolution == ErrorResolution.ConnectionLost)
            {
                interruptedByConnectionLoss = true;
                await HandleTransientConnectionLossAsync(cts.Token);
                break;
            }
            if (cts.IsCancellationRequested || _flowState.IsStopRequested)
            {
                break;
            }
            if (resolution == ErrorResolution.Timeout)
            {
                await HandleTagTimeoutAsync("ожидание решения оператора", cts.Token);
                break;
            }
            await ProcessErrorResolution(error, resolution, cts.Token);
        }

        if (CanReturnToRunningState(cts, interruptedByConnectionLoss))
        {
            StateManager.TransitionTo(ExecutionState.Running);
        }
    }

    private bool CanHandleNextError(CancellationTokenSource cts)
    {
        return StateManager.HasPendingErrors && !cts.IsCancellationRequested;
    }

    private bool CanReturnToRunningState(CancellationTokenSource cts, bool interruptedByConnectionLoss)
    {
        return !cts.IsCancellationRequested && !_flowState.IsStopRequested && !interruptedByConnectionLoss;
    }

    private async Task PrepareErrorProcessingAsync(StepError error, CancellationToken ct)
    {
        StateManager.TransitionTo(ExecutionState.PausedOnError);
        await SetSelectedAsync(error, true);
        await SetFaultIfNoBlockAsync(error.FailedStep, ct);
        TryPublishEvent(new ExecutionEvent(ExecutionEventKind.ErrorOccurred, StepError: error));
    }

    private async Task<ErrorResolution> WaitResolutionSafeAsync(StepError error, CancellationToken ct)
    {
        try
        {
            var options = new WaitForResolutionOptions(
                BlockEndTag: GetBlockEndTag(error.FailedStep),
                BlockErrorTag: GetBlockErrorTag(error.FailedStep),
                EnableSkip: error.CanSkip);
            return await _errorCoordinator.WaitForResolutionAsync(options, ct);
        }
        catch (OperationCanceledException)
        {
            return ErrorResolution.None;
        }
        catch (Exception ex) when (IsTransientOpcDisconnect(ex))
        {
            _logger.LogWarning("Transient OPC disconnect при ожидании решения оператора: {Error}", ex.Message);
            return ErrorResolution.ConnectionLost;
        }
    }

    private async Task HandleTransientConnectionLossAsync(CancellationToken ct)
    {
        _logger.LogWarning("Потеря связи с PLC в error-resolution. Переход в контролируемый interrupt путь");
        await _errorCoordinator.HandleInterruptAsync(InterruptReason.PlcConnectionLost, ct);
    }

    private static bool IsTransientOpcDisconnect(Exception ex)
    {
        return OpcUaTransientErrorClassifier.IsTransientDisconnect(ex);
    }

    /// <summary>
    /// Обрабатывает таймаут ожидания PLC-тегов как жёсткий стоп теста.
    /// </summary>
    private async Task HandleTagTimeoutAsync(string context, CancellationToken ct)
    {
        var cts = _cts;
        if (cts == null)
        {
            _logger.LogWarning("TagTimeout во время {Context}, но нет активного CancellationTokenSource", context);
            return;
        }

        _logger.LogWarning("TagTimeout во время {Context} - жёсткий стоп теста", context);
        RequestStopAsFailure(ExecutionStopReason.Operator);
        await _errorCoordinator.HandleInterruptAsync(InterruptReason.TagTimeout, ct);
        await cts.CancelAsync();
    }

    /// <summary>
    /// Обрабатывает решение пользователя по ошибке.
    /// </summary>
    private async Task ProcessErrorResolution(StepError error, ErrorResolution resolution, CancellationToken ct)
    {
        ColumnExecutor executor;
        lock (_enqueueLock)
        {
            executor = _executors[error.ColumnIndex];
        }

        if (resolution == ErrorResolution.Retry)
        {
            await ProcessRetryAsync(error, executor, ct);
        }
        else
        {
            await ProcessSkipAsync(error, executor, ct);
        }
    }

    /// <summary>
    /// Безопасно вызывает событие OnRetryStarted.
    /// </summary>
    private void InvokeRetryStartedSafely()
    {
        try
        {
            OnRetryStarted?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка в обработчике OnRetryStarted");
        }
    }

    /// <summary>
    /// Безопасно вызывает событие OnErrorOccurred.
    /// </summary>
    private void InvokeErrorOccurredSafely(StepError error)
    {
        try
        {
            OnErrorOccurred?.Invoke(error);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка в обработчике OnErrorOccurred");
        }
    }

    /// <summary>
    /// Обрабатывает повтор шага.
    /// Retry запускается в фоне через event loop и отслеживается, чтобы диалог следующей ошибки появлялся сразу.
    /// </summary>
    private async Task ProcessRetryAsync(StepError error, ColumnExecutor executor, CancellationToken ct)
    {
        await _pauseToken.WaitWhilePausedAsync(ct);

        try
        {
            var blockErrorTag = GetBlockErrorTag(error.FailedStep);
            await _errorCoordinator.SendAskRepeatAsync(blockErrorTag, ct);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Block.Error не сброшен за 60 сек - жёсткий стоп");
            await HandleTagTimeoutAsync("Block.Error не сброшен", ct);
            return;
        }
        catch (Exception ex)
        {
            await HandleAskRepeatFailureAsync(error, ex, ct);
            return;
        }

        TryPublishEvent(new ExecutionEvent(ExecutionEventKind.RetryStarted));

        try
        {
            await _errorCoordinator.WaitForRetrySignalResetAsync(ct);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Req_Repeat не сброшен за 60 сек - жёсткий стоп");
            await HandleTagTimeoutAsync("Req_Repeat не сброшен", ct);
            return;
        }

        _retryState.MarkStarted();
        StateManager.DequeueError();

        await PublishEventCritical(new ExecutionEvent(
            ExecutionEventKind.RetryRequested,
            StepError: error,
            ColumnExecutor: executor));
    }

    private async Task HandleAskRepeatFailureAsync(StepError error, Exception ex, CancellationToken ct)
    {
        _logger.LogError(
            ex,
            "Критичная ошибка SendAskRepeatAsync для колонки {Column}. Выполняем fail-fast",
            error.ColumnIndex);

        RequestStopAsFailure(ExecutionStopReason.Operator);
        var interruptReason = IsTransientOpcDisconnect(ex)
            ? InterruptReason.PlcConnectionLost
            : InterruptReason.TagTimeout;
        await _errorCoordinator.HandleInterruptAsync(interruptReason, ct);
        var cts = _cts;
        if (cts != null)
        {
            await cts.CancelAsync();
        }
    }

    /// <summary>
    /// Выполняет retry шага в фоне.
    /// Открывает gate после успешного завершения.
    /// </summary>
    private async Task ExecuteRetryInBackgroundAsync(StepError error, ColumnExecutor executor, CancellationToken ct)
    {
        try
        {
            await executor.RetryLastFailedStepAsync(ct);

            await ResetFaultIfNoBlockAsync(error.FailedStep, ct);

            if (!executor.HasFailed)
            {
                executor.OpenGate();
            }
        }
        catch (OperationCanceledException)
        {
            if (!executor.HasFailed)
            {
                executor.OpenGate();
            }
        }
        catch (Exception ex)
        {
            await HandleRetryFailureWithHardResetAsync(error, ex);
            executor.OpenGate();
        }
        finally
        {
            _retryState.MarkCompleted();
            TryPublishEvent(new ExecutionEvent(
                ExecutionEventKind.RetryCompleted,
                StepError: error,
                ColumnExecutor: executor));
        }
    }

    private async Task HandleRetryFailureWithHardResetAsync(StepError error, Exception ex)
    {
        _logger.LogError(
            ex,
            "Ошибка Retry в фоне для колонки {Column}. Запрошен HardReset",
            error.ColumnIndex);

        var hardResetAccepted = TryRequestHardResetFromRetryFailure();
        if (hardResetAccepted && (_flowState.IsStopRequested || IsCancellationRequested))
        {
            return;
        }

        RequestStopAsFailure(ExecutionStopReason.PlcHardReset);
        var cts = _cts;
        if (cts != null)
        {
            await cts.CancelAsync();
        }
    }

    private bool TryRequestHardResetFromRetryFailure()
    {
        try
        {
            _errorCoordinator.Reset();
            return true;
        }
        catch (Exception resetEx)
        {
            _logger.LogError(resetEx, "Не удалось выполнить HardReset после ошибки Retry");
            return false;
        }
    }

    /// <summary>
    /// Обрабатывает пропуск шага.
    /// </summary>
    private async Task ProcessSkipAsync(StepError error, ColumnExecutor executor, CancellationToken ct)
    {
        await _pauseToken.WaitWhilePausedAsync(ct);

        await ResetBlockStartAsync(error.FailedStep, ct);
        await ResetFaultIfNoBlockAsync(error.FailedStep, ct);

        _logger.LogDebug("Ожидание сброса сигналов Skip...");
        try
        {
            await WaitForSkipSignalsResetAsync(error.FailedStep, ct);
        }
        catch (TimeoutException)
        {
            await HandleTagTimeoutAsync("сброс сигналов Skip", ct);
            return;
        }
        _logger.LogDebug("Сброс сигналов Skip завершён");

        _statusReporter.ReportSkipped(error.UiStepId);
        StateManager.MarkErrorSkipped();
        StateManager.DequeueError();
        executor.ClearFailedState();
    }
}
