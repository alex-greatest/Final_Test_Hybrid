using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator
{
    public async Task StartMainLoopAsync(CancellationToken ct)
    {
        EnsureSubscribed();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await RunSingleCycleAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            SetAcceptingInput(false);
        }
    }

    private async Task RunSingleCycleAsync(CancellationToken ct)
    {
        state.FlowState.ClearStop();
        // Snapshot reset sequence before await; guards reuse of CurrentBarcode on repeat.
        var resetSequence = Volatile.Read(ref _resetSequence);
        await WaitForAskEndIfNeededAsync(ct);

        // Проверка пропуска сканирования для повтора
        string barcode;
        if (_skipNextScan)
        {
            // If a reset happened during the await above, do not reuse the previous barcode.
            if (DidResetOccur(resetSequence)) return;
            barcode = CurrentBarcode!;
            infra.StatusReporter.UpdateScanStepStatus(Models.Steps.TestStepStatus.Success, "Повтор теста");
        }
        else
        {
            SetAcceptingInput(true);
            try
            {
                barcode = await WaitForBarcodeAsync(ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && DidResetOccur(resetSequence))
            {
                SetAcceptingInput(false);
                return;
            }
            SetAcceptingInput(false);
            infra.StatusReporter.UpdateScanStepStatus(Models.Steps.TestStepStatus.Running, "Обработка штрихкода");
        }

        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var testCompletionTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action onTestCompleted = () => testCompletionTcs.TrySetResult();
        coordinators.TestCoordinator.OnSequenceCompleted += onTestCompleted;

        CycleExitReason exitReason;
        try
        {
            state.ActivityTracker.SetPreExecutionActive(true);
            exitReason = await ExecuteCycleAsync(barcode, testCompletionTcs, ct);
        }
        catch (OperationCanceledException)
        {
            exitReason = _pendingExitReason ?? CycleExitReason.PipelineCancelled;
        }
        finally
        {
            _pendingExitReason = null;
            _resetSignal = null;
            coordinators.TestCoordinator.OnSequenceCompleted -= onTestCompleted;
            state.ActivityTracker.SetPreExecutionActive(false);
            _currentCts?.Dispose();
            _currentCts = null;
        }

        HandleCycleExit(exitReason);
    }

    private async Task<CycleExitReason> ExecuteCycleAsync(
        string barcode,
        TaskCompletionSource testCompletionTcs,
        CancellationToken ct)
    {
        var isRepeat = _skipNextScan;
        var isNokRepeat = _skipNextScan && _executeFullPreparation;
        _skipNextScan = false;
        _executeFullPreparation = false;

        // Создаём сигнал сброса для защиты от race condition
        var resetSignal = new TaskCompletionSource<CycleExitReason>(TaskCreationOptions.RunContinuationsAsynchronously);
        _resetSignal = resetSignal;

        var result = isNokRepeat
            ? await ExecuteNokRepeatPipelineAsync(_currentCts!.Token)
            : isRepeat
                ? await ExecuteRepeatPipelineAsync(_currentCts!.Token)
                : await ExecutePreExecutionPipelineAsync(barcode, _currentCts!.Token);

        // Проверяем pending exit reason (сброс мог прийти во время pipeline)
        if (TryGetStopExitReason(out var stopExitReason))
        {
            return stopExitReason;
        }

        if (result.Status != Interfaces.PreExecution.PreExecutionStatus.TestStarted)
        {
            return result.Status == Interfaces.PreExecution.PreExecutionStatus.Cancelled
                ? CycleExitReason.PipelineCancelled
                : CycleExitReason.PipelineFailed;
        }

        // Ждём завершения теста ИЛИ сигнала сброса
        await using var reg = ct.Register(() => testCompletionTcs.TrySetCanceled());
        var completedTask = await Task.WhenAny(testCompletionTcs.Task, resetSignal.Task);

        // Если сработал сброс - возвращаем причину сброса
        if (completedTask == resetSignal.Task)
        {
            return await resetSignal.Task;
        }

        await testCompletionTcs.Task;

        // Проверяем pending exit reason после теста
        if (TryGetStopExitReason(out stopExitReason))
        {
            return stopExitReason;
        }

        // Обрабатываем завершение теста через координатор
        return await HandleTestCompletionAsync(ct);
    }

    private void HandleCycleExit(CycleExitReason reason)
    {
        switch (reason)
        {
            case CycleExitReason.TestCompleted:
                state.BoilerState.SetTestRunning(false);
                ClearForTestCompletion();
                state.BoilerState.ResetAndStartChangeoverTimer();
                break;

            case CycleExitReason.SoftReset:
                // Ничего - очистка произойдёт по AskEnd в HandleGridClear
                state.BoilerState.ResetAndStartChangeoverTimer();
                break;

            case CycleExitReason.HardReset:
                ClearStateOnReset();
                infra.StatusReporter.ClearAllExceptScan();
                state.BoilerState.ResetAndStartChangeoverTimer();
                break;

            case CycleExitReason.RepeatRequested:
                ClearForRepeat();
                _skipNextScan = true;
                break;

            case CycleExitReason.NokRepeatRequested:
                ClearForNokRepeat();
                _skipNextScan = true;
                _executeFullPreparation = true;
                break;

            case CycleExitReason.PipelineFailed:
            case CycleExitReason.PipelineCancelled:
                // Ничего — состояние сохраняется для retry или следующей попытки
                break;
        }
    }

    private async Task<CycleExitReason> HandleTestCompletionAsync(CancellationToken ct)
    {
        // Защита от race condition — если сброс уже произошёл, не показываем результат
        if (TryGetStopExitReason(out var stopExitReason))
        {
            return stopExitReason;
        }

        infra.ErrorService.IsHistoryEnabled = false;
        state.BoilerState.StopTestTimer();
        var hasErrors = coordinators.TestCoordinator.HasErrors
                        || coordinators.TestCoordinator.HadSkippedError;
        var testResult = hasErrors ? 2 : 1;
        state.BoilerState.SetTestResult(testResult);

        infra.Logger.LogInformation("Тест завершён. Результат: {Result} ({Status})",
            testResult, testResult == 1 ? "OK" : "NOK");
        // Показать картинку результата
        coordinators.CompletionUiState.ShowImage(testResult);

        // Связываем с _resetCts чтобы Reset прерывал ожидание End
        var resetCts = _resetCts;
        CancellationTokenSource linked;
        try { linked = CancellationTokenSource.CreateLinkedTokenSource(ct, resetCts.Token); }
        catch (ObjectDisposedException)
        {
            // _resetCts disposed → reset уже завершён
            // Примечание: в узком окне между dispose и SignalReset может вернуться SoftReset
            // вместо HardReset — это допустимо, т.к. состояние очистится по AskEnd
            coordinators.CompletionUiState.HideImage();
            return TryGetStopExitReason(out var exitReason) ? exitReason : CycleExitReason.SoftReset;
        }

        try
        {
            // Вызвать координатор завершения
            var result = await coordinators.CompletionCoordinator
                .HandleTestCompletedAsync(testResult, linked.Token);

            return result switch
            {
                CompletionResult.Finished => CycleExitReason.TestCompleted,
                CompletionResult.RepeatRequested => CycleExitReason.RepeatRequested,
                CompletionResult.NokRepeatRequested => CycleExitReason.NokRepeatRequested,
                _ => TryGetStopExitReason(out var exitReason) ? exitReason : CycleExitReason.SoftReset,
            };
        }
        catch (OperationCanceledException)
        {
            // Reset или внешняя отмена прервали ожидание End
            // При shutdown тоже вернётся SoftReset — это допустимо, цикл завершится на следующей итерации
            return TryGetStopExitReason(out var exitReason) ? exitReason : CycleExitReason.SoftReset;
        }
        finally
        {
            linked.Dispose();
            coordinators.CompletionUiState.HideImage();
        }
    }

    private async Task<string> WaitForBarcodeAsync(CancellationToken ct)
    {
        var newSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _barcodeSource, newSource);

        var resetCts = _resetCts;
        CancellationTokenSource linkedCts;
        try { linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, resetCts.Token); }
        catch (ObjectDisposedException) { throw new OperationCanceledException(ct); }

        using (linkedCts)
        {
            await using var reg = linkedCts.Token.Register(() => newSource.TrySetCanceled());
            var barcode = await newSource.Task;
            CurrentBarcode = barcode;
            OnStateChanged?.Invoke();
            return barcode;
        }
    }
}
