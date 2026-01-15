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
        SetAcceptingInput(true);
        var barcode = await WaitForBarcodeAsync(ct);
        SetAcceptingInput(false);

        infra.StatusReporter.UpdateScanStepStatus(Models.Steps.TestStepStatus.Running, "Обработка штрихкода");
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
        var result = await ExecutePreExecutionPipelineAsync(barcode, _currentCts!.Token);

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

        return CycleExitReason.TestCompleted;
    }

    private void HandleCycleExit(CycleExitReason reason)
    {
        switch (reason)
        {
            case CycleExitReason.TestCompleted:
                HandleTestCompleted();
                break;

            case CycleExitReason.SoftReset:
                // Ничего - очистка произойдёт по AskEnd в HandleGridClear
                break;

            case CycleExitReason.HardReset:
                ClearStateOnReset();
                infra.StatusReporter.ClearAllExceptScan();
                break;

            case CycleExitReason.PipelineFailed:
            case CycleExitReason.PipelineCancelled:
                // Ничего — состояние сохраняется для retry или следующей попытки
                break;
        }
    }

    private void HandleTestCompleted()
    {
        var hasErrors = coordinators.TestCoordinator.HasErrors || coordinators.TestCoordinator.HadSkippedError;
        var testResult = hasErrors ? 2 : 1;
        state.BoilerState.SetTestResult(testResult);
        infra.Logger.LogInformation("Тест завершён. Результат: {Result} ({Status})",
            testResult, testResult == 1 ? "OK" : "NOK");
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
