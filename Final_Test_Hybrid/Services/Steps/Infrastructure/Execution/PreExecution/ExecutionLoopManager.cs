using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Lifecycle;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

/// <summary>
/// Управляет основным циклом PreExecution: ожидание barcode, запуск pipeline, ожидание теста.
/// Интегрирован с SystemLifecycleManager для управления фазами системы.
/// </summary>
public class ExecutionLoopManager(
    PreExecutionPipeline pipeline,
    PreExecutionInfrastructure infra,
    PreExecutionCoordinators coordinators,
    PreExecutionState state,
    SystemLifecycleManager lifecycle,
    DualLogger<ExecutionLoopManager> logger)
{
    private TaskCompletionSource<string>? _barcodeSource;
    private CancellationTokenSource? _currentCts;
    private CycleExitReason? _pendingExitReason;
    private bool _skipNextScan;
    private bool _executeFullPreparation;

    /// <summary>
    /// Принимает ли система ввод штрихкода.
    /// </summary>
    public bool IsAcceptingInput { get; private set; }

    /// <summary>
    /// Выполняется ли обработка (не принимает ввод, но PreExecution активен).
    /// </summary>
    public bool IsProcessing => !IsAcceptingInput && state.ActivityTracker.IsPreExecutionActive;

    /// <summary>
    /// Текущий штрихкод.
    /// </summary>
    public string? CurrentBarcode => lifecycle.CurrentBarcode;

    /// <summary>
    /// Вызывается при изменении состояния ввода.
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// Запускает основной цикл PreExecution.
    /// </summary>
    public async Task StartMainLoopAsync(CancellationToken ct)
    {
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

    /// <summary>
    /// Отправляет штрихкод в систему.
    /// </summary>
    public void SubmitBarcode(string barcode)
    {
        _barcodeSource?.TrySetResult(barcode);
    }

    /// <summary>
    /// Уведомляет подписчиков об изменении состояния.
    /// </summary>
    public void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Устанавливает причину выхода из цикла (для обработки сброса).
    /// </summary>
    public void SetPendingExitReason(CycleExitReason reason)
    {
        _pendingExitReason = reason;
        _currentCts?.Cancel();
    }

    /// <summary>
    /// Проверяет, есть ли активная операция.
    /// </summary>
    public bool HasActiveOperation()
    {
        return coordinators.TestCoordinator.IsRunning || state.ActivityTracker.IsPreExecutionActive;
    }

    private async Task RunSingleCycleAsync(CancellationToken ct)
    {
        string barcode;
        if (_skipNextScan)
        {
            barcode = CurrentBarcode!;
            infra.StatusReporter.UpdateScanStepStatus(TestStepStatus.Success, "Повтор теста");
        }
        else
        {
            SetAcceptingInput(true);
            barcode = await WaitForBarcodeAsync(ct);
            SetAcceptingInput(false);
            infra.StatusReporter.UpdateScanStepStatus(TestStepStatus.Running, "Обработка штрихкода");
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

        var result = await pipeline.ExecutePipelineAsync(barcode, isRepeat, isNokRepeat, _currentCts!.Token);

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

        await using var reg = ct.Register(() => testCompletionTcs.TrySetCanceled());
        await testCompletionTcs.Task;

        if (_pendingExitReason.HasValue)
        {
            return _pendingExitReason.Value;
        }

        return await HandleTestCompletionAsync(ct);
    }

    private void HandleCycleExit(CycleExitReason reason)
    {
        switch (reason)
        {
            case CycleExitReason.TestCompleted:
                state.BoilerState.SetTestRunning(false);
                pipeline.ClearForTestCompletion();
                lifecycle.Transition(SystemTrigger.TestAcknowledged);
                break;

            case CycleExitReason.SoftReset:
                // Очистка произойдёт по AskEnd в HandleGridClear
                break;

            case CycleExitReason.HardReset:
                pipeline.ClearStateOnReset();
                infra.StatusReporter.ClearAllExceptScan();
                break;

            case CycleExitReason.RepeatRequested:
                pipeline.ClearForRepeat();
                _skipNextScan = true;
                lifecycle.Transition(SystemTrigger.RepeatRequested);
                break;

            case CycleExitReason.NokRepeatRequested:
                pipeline.ClearForNokRepeat();
                _skipNextScan = true;
                _executeFullPreparation = true;
                lifecycle.Transition(SystemTrigger.RepeatRequested);
                break;

            case CycleExitReason.PipelineFailed:
            case CycleExitReason.PipelineCancelled:
                lifecycle.Transition(SystemTrigger.PreparationFailed);
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

        logger.LogInformation("Тест завершён. Результат: {Result} ({Status})",
            testResult, testResult == 1 ? "OK" : "NOK");

        coordinators.CompletionUiState.ShowImage(testResult);

        try
        {
            lifecycle.Transition(SystemTrigger.TestFinished);

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

        lifecycle.Transition(SystemTrigger.BarcodeReceived, barcode);
        OnStateChanged?.Invoke();
        return barcode;
    }

    private void SetAcceptingInput(bool value)
    {
        IsAcceptingInput = value;
        if (value)
        {
            infra.StepTimingService.ResetScanTiming();
        }
        OnStateChanged?.Invoke();
    }
}
