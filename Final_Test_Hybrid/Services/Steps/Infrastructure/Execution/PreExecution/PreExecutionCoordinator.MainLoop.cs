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

        // Проверка пропуска сканирования для повтора
        string barcode;
        if (_skipNextScan)
        {
            barcode = CurrentBarcode!;
            infra.StatusReporter.UpdateScanStepStatus(Models.Steps.TestStepStatus.Success, "Повтор теста");
        }
        else
        {
            SetAcceptingInput(true);
            barcode = await WaitForBarcodeAsync(ct);
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

        var result = isNokRepeat
            ? await ExecuteNokRepeatPipelineAsync(_currentCts!.Token)
            : isRepeat
                ? await ExecuteRepeatPipelineAsync(_currentCts!.Token)
                : await ExecutePreExecutionPipelineAsync(barcode, _currentCts!.Token);

        // Проверяем pending exit reason (сброс мог прийти во время pipeline)
        if (_pendingExitReason.HasValue)
        {
            return _pendingExitReason.Value;
        }

        if (result.Status != Interfaces.PreExecution.PreExecutionStatus.TestStarted)
        {
            return result.Status == Interfaces.PreExecution.PreExecutionStatus.Cancelled
                ? CycleExitReason.PipelineCancelled
                : CycleExitReason.PipelineFailed;
        }

        // Ждём завершения теста
        await using var reg = ct.Register(() => testCompletionTcs.TrySetCanceled());
        await testCompletionTcs.Task;

        // Проверяем pending exit reason после теста
        if (_pendingExitReason.HasValue)
        {
            return _pendingExitReason.Value;
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
                break;

            case CycleExitReason.SoftReset:
                // Ничего - очистка произойдёт по AskEnd в HandleGridClear
                break;

            case CycleExitReason.HardReset:
                ClearStateOnReset();
                infra.StatusReporter.ClearAllExceptScan();
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
        state.BoilerState.StopTestTimer();
        var hasErrors = coordinators.TestCoordinator.HasErrors
                        || coordinators.TestCoordinator.HadSkippedError;
        var testResult = hasErrors ? 2 : 1;
        state.BoilerState.SetTestResult(testResult);

        infra.Logger.LogInformation("Тест завершён. Результат: {Result} ({Status})",
            testResult, testResult == 1 ? "OK" : "NOK");
        // Показать картинку результата
        coordinators.CompletionUiState.ShowImage(testResult);

        try
        {
            // Вызвать координатор завершения
            var result = await coordinators.CompletionCoordinator
                .HandleTestCompletedAsync(testResult, ct);

            return result switch
            {
                CompletionResult.Finished => CycleExitReason.TestCompleted,
                CompletionResult.RepeatRequested => CycleExitReason.RepeatRequested,
                CompletionResult.NokRepeatRequested => CycleExitReason.NokRepeatRequested,
                _ => _pendingExitReason ?? CycleExitReason.SoftReset,
            };
        }
        finally
        {
            coordinators.CompletionUiState.HideImage();
        }
    }

    private async Task<string> WaitForBarcodeAsync(CancellationToken ct)
    {
        var newSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _barcodeSource, newSource);
        await using var reg = ct.Register(() => newSource.TrySetCanceled());
        var barcode = await newSource.Task;
        CurrentBarcode = barcode;
        OnStateChanged?.Invoke();
        return barcode;
    }
}
