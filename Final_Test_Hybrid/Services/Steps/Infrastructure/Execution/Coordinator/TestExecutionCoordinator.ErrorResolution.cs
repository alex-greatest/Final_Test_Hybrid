using Final_Test_Hybrid.Models.Steps;
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

        while (StateManager.HasPendingErrors && !cts.IsCancellationRequested)
        {
            var error = StateManager.CurrentError;
            if (error == null)
            {
                break;
            }
            StateManager.TransitionTo(ExecutionState.PausedOnError);
            await SetSelectedAsync(error, true);
            await SetFaultIfNoBlockAsync(error.FailedStep, cts.Token);
            OnErrorOccurred?.Invoke(error);
            ErrorResolution resolution;
            try
            {
                var options = new WaitForResolutionOptions(
                    BlockEndTag: GetBlockEndTag(error.FailedStep),
                    BlockErrorTag: GetBlockErrorTag(error.FailedStep),
                    EnableSkip: error.CanSkip);
                resolution = await _errorCoordinator.WaitForResolutionAsync(options, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // await SetSelectedAsync(error, false);  // PLC сам сбросит
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
            // await SetSelectedAsync(error, false);  // PLC сам сбросит
        }
        if (!cts.IsCancellationRequested && !_flowState.IsStopRequested)
        {
            StateManager.TransitionTo(ExecutionState.Running);
        }
    }

    /// <summary>
    /// Устанавливает тег Selected для PLC-блока.
    /// </summary>
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

        _logger.LogWarning("TagTimeout во время {Context} — жёсткий стоп теста", context);
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
    /// Обрабатывает повтор шага.
    /// Fire-and-forget для retry, чтобы диалог следующей ошибки появился сразу.
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
            _logger.LogError("Block.Error не сброшен за 60 сек — жёсткий стоп");
            await HandleTagTimeoutAsync("Block.Error не сброшен", ct);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка SendAskRepeatAsync для колонки {Column}", error.ColumnIndex);
            return;
        }

        InvokeRetryStartedSafely();

        try
        {
            await _errorCoordinator.WaitForRetrySignalResetAsync(ct);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Req_Repeat не сброшен за 60 сек — жёсткий стоп");
            await HandleTagTimeoutAsync("Req_Repeat не сброшен", ct);
            return;
        }

        StateManager.DequeueError();

        _ = ExecuteRetryInBackgroundAsync(error, executor, ct);
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
            // Гарантируем что колонка не зависнет при Cancel
            if (!executor.HasFailed)
            {
                executor.OpenGate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка Retry в фоне для колонки {Column}", error.ColumnIndex);
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

        _logger.LogWarning(">>> ProcessSkipAsync: НАЧАЛО ожидания сброса сигналов");
        try
        {
            await WaitForSkipSignalsResetAsync(error.FailedStep, ct);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(">>> ProcessSkipAsync: TIMEOUT РѕР¶РёРґР°РЅРёСЏ СЃР±СЂРѕСЃР° СЃРёРіРЅР°Р»РѕРІ");
            await HandleTagTimeoutAsync("сброс сигналов Skip", ct);
            return;
        }
        _logger.LogWarning(">>> ProcessSkipAsync: КОНЕЦ ожидания сброса сигналов");

        _statusReporter.ReportSkipped(error.UiStepId);
        StateManager.MarkErrorSkipped();
        StateManager.DequeueError();     // СНАЧАЛА удаляем из очереди (защита от race condition)
        executor.ClearFailedState();     // ПОТОМ открываем gate
    }
}

