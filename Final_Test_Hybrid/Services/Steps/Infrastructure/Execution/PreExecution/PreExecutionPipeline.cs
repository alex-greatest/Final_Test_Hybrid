using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Lifecycle;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

/// <summary>
/// Выполняет pipeline подготовки к тесту: ScanStep, BlockBoilerAdapterStep.
/// Делегирует retry-логику в RetryCoordinator.
/// </summary>
public class PreExecutionPipeline(
    PreExecutionSteps steps,
    PreExecutionInfrastructure infra,
    PreExecutionCoordinators coordinators,
    PreExecutionState state,
    RetryCoordinator retryCoordinator,
    SystemLifecycleManager lifecycle,
    DualLogger<PreExecutionPipeline> logger)
{
    private PreExecutionContext? _lastSuccessfulContext;

    /// <summary>
    /// Возвращает текущий ScanStep (зависит от MES-режима).
    /// </summary>
    public ScanStepBase GetScanStep() => steps.GetScanStep();

    /// <summary>
    /// Выполняет pipeline подготовки.
    /// </summary>
    public Task<PreExecutionResult> ExecutePipelineAsync(
        string barcode,
        bool isRepeat,
        bool isNokRepeat,
        CancellationToken ct)
    {
        return isNokRepeat
            ? ExecuteNokRepeatPipelineAsync(barcode, ct)
            : isRepeat
                ? ExecuteRepeatPipelineAsync(ct)
                : ExecutePreExecutionPipelineAsync(barcode, ct);
    }

    /// <summary>
    /// Очистка при завершении теста (OK/NOK).
    /// Результаты и история ошибок НЕ чистятся — оператор должен их видеть.
    /// </summary>
    public void ClearForTestCompletion()
    {
        infra.StatusReporter.ClearAllExceptScan();
        infra.StepTimingService.Clear();
        infra.RecipeProvider.Clear();
        state.BoilerState.Clear();
        infra.ErrorService.IsHistoryEnabled = false;

        logger.LogInformation("Состояние очищено после завершения теста");
    }

    /// <summary>
    /// Очистка при сбросе PLC.
    /// </summary>
    public void ClearStateOnReset()
    {
        state.BoilerState.Clear();
        state.PhaseState.Clear();
        infra.ErrorService.IsHistoryEnabled = false;
        infra.StepTimingService.Clear();
        infra.RecipeProvider.Clear();
        _lastSuccessfulContext = null;

        logger.LogInformation("Состояние очищено при сбросе");
    }

    /// <summary>
    /// Очистка для OK-повтора.
    /// </summary>
    public void ClearForRepeat()
    {
        infra.ErrorService.IsHistoryEnabled = false;
        infra.StatusReporter.ClearAllExceptScan();
        infra.StepTimingService.Clear();
        coordinators.TestCoordinator.ResetForRepeat();

        logger.LogInformation("Состояние очищено для повтора");
    }

    /// <summary>
    /// Очистка для NOK-повтора с полной подготовкой.
    /// </summary>
    public void ClearForNokRepeat()
    {
        state.BoilerState.Clear();
        state.PhaseState.Clear();
        infra.ErrorService.IsHistoryEnabled = false;
        _lastSuccessfulContext = null;

        infra.StatusReporter.ClearAllExceptScan();
        infra.StepTimingService.Clear();
        infra.RecipeProvider.Clear();
        coordinators.TestCoordinator.ResetForRepeat();

        logger.LogInformation("Состояние очищено для NOK повтора с подготовкой");
    }

    private async Task<PreExecutionResult> ExecutePreExecutionPipelineAsync(
        string barcode,
        CancellationToken ct)
    {
        var context = CreateContext(barcode);

        var scanResult = await ExecuteScanStepAsync(context, ct);

        switch (scanResult.Status)
        {
            case PreExecutionStatus.Failed:
                infra.StatusReporter.UpdateScanStepStatus(
                    TestStepStatus.Error,
                    scanResult.ErrorMessage ?? "Ошибка",
                    scanResult.Limits);
                return scanResult;
            case PreExecutionStatus.Cancelled:
                return scanResult;
        }

        infra.StatusReporter.UpdateScanStepStatus(
            TestStepStatus.Success,
            scanResult.SuccessMessage ?? "",
            scanResult.Limits);

        _lastSuccessfulContext = context;
        ClearForNewTestStart();

        infra.ErrorService.IsHistoryEnabled = true;
        state.BoilerState.SetTestRunning(true);
        state.BoilerState.StartTestTimer();

        ct.ThrowIfCancellationRequested();

        var timerResult = await ExecuteStartTimer1Async(context, ct);
        if (timerResult.Status != PreExecutionStatus.Continue)
        {
            return timerResult;
        }

        var blockResult = await ExecuteBlockBoilerAdapterAsync(context, ct);
        if (blockResult.Status != PreExecutionStatus.Continue)
        {
            return blockResult;
        }

        state.PhaseState.Clear();
        lifecycle.Transition(SystemTrigger.PreparationCompleted);
        StartTestExecution(context);
        return PreExecutionResult.TestStarted();
    }

    private async Task<PreExecutionResult> ExecuteRepeatPipelineAsync(CancellationToken ct)
    {
        var context = _lastSuccessfulContext;
        if (context?.Maps == null)
        {
            logger.LogError("Нет сохранённого контекста для повтора");
            return PreExecutionResult.Fail("Нет данных для повтора");
        }

        ClearForNewTestStart();

        infra.ErrorService.IsHistoryEnabled = true;
        state.BoilerState.SetTestRunning(true);
        state.BoilerState.StartTestTimer();

        ct.ThrowIfCancellationRequested();

        var timerResult = await ExecuteStartTimer1Async(context, ct);
        if (timerResult.Status != PreExecutionStatus.Continue)
        {
            return timerResult;
        }

        var blockResult = await ExecuteBlockBoilerAdapterAsync(context, ct);
        if (blockResult.Status != PreExecutionStatus.Continue)
        {
            return blockResult;
        }

        state.PhaseState.Clear();
        lifecycle.Transition(SystemTrigger.PreparationCompleted);
        StartTestExecution(context);
        return PreExecutionResult.TestStarted();
    }

    private async Task<PreExecutionResult> ExecuteNokRepeatPipelineAsync(string barcode, CancellationToken ct)
    {
        logger.LogInformation("NOK повтор: запуск полной подготовки с barcode={Barcode}", barcode);

        while (!ct.IsCancellationRequested)
        {
            var result = await ExecutePreExecutionPipelineAsync(barcode, ct);

            if (result.Status == PreExecutionStatus.TestStarted || result.Status == PreExecutionStatus.Cancelled)
            {
                return result;
            }

            var shouldRetry = await coordinators.CompletionCoordinator.ShowPrepareErrorDialogAsync(result.ErrorMessage);
            if (!shouldRetry)
            {
                return PreExecutionResult.Cancelled();
            }

            logger.LogInformation("NOK повтор: повторная попытка подготовки");
        }

        return PreExecutionResult.Cancelled();
    }

    private async Task<PreExecutionResult> ExecuteScanStepAsync(PreExecutionContext context, CancellationToken ct)
    {
        var scanStep = steps.GetScanStep();
        try
        {
            await infra.PauseToken.WaitWhilePausedAsync(ct);
            var result = await scanStep.ExecuteAsync(context, ct);
            if (result.Status != PreExecutionStatus.Failed)
            {
                infra.StepTimingService.StopScanTiming();
            }
            return result;
        }
        catch (Exception ex)
        {
            return HandleStepException(scanStep, ex);
        }
    }

    private async Task<PreExecutionResult> ExecuteStartTimer1Async(PreExecutionContext context, CancellationToken ct)
    {
        var stepId = infra.StatusReporter.ReportStepStarted(steps.StartTimer1);
        try
        {
            await infra.PauseToken.WaitWhilePausedAsync(ct);
            var result = await steps.StartTimer1.ExecuteAsync(context, ct);
            infra.StatusReporter.ReportSuccess(stepId, result.SuccessMessage ?? "");
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка в шаге StartTimer1");
            infra.StatusReporter.ReportError(stepId, ex.Message);
            return PreExecutionResult.Fail(ex.Message);
        }
    }

    private async Task<PreExecutionResult> ExecuteBlockBoilerAdapterAsync(PreExecutionContext context, CancellationToken ct)
    {
        var stepId = infra.StatusReporter.ReportStepStarted(steps.BlockBoilerAdapter);
        try
        {
            await infra.PauseToken.WaitWhilePausedAsync(ct);
            infra.StepTimingService.StartCurrentStepTiming(steps.BlockBoilerAdapter.Name, steps.BlockBoilerAdapter.Description);
            var result = await steps.BlockBoilerAdapter.ExecuteAsync(context, ct);
            infra.StepTimingService.StopCurrentStepTiming();

            if (result.IsRetryable)
            {
                var retryResult = await retryCoordinator.ExecuteRetryLoopAsync(
                    steps.BlockBoilerAdapter, result, context, stepId, ct);
                ReportBlockStepResult(stepId, retryResult);
                return retryResult;
            }

            ReportBlockStepResult(stepId, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            infra.StepTimingService.StopCurrentStepTiming();
            throw;
        }
        catch (Exception ex)
        {
            infra.StepTimingService.StopCurrentStepTiming();
            return HandleStepException(steps.BlockBoilerAdapter, stepId, ex);
        }
    }

    private void ClearForNewTestStart()
    {
        infra.ErrorService.ClearHistory();
        infra.TestResultsService.Clear();
        logger.LogInformation("История и результаты очищены для нового теста");
    }

    private void ReportBlockStepResult(Guid stepId, PreExecutionResult result)
    {
        switch (result.Status)
        {
            case PreExecutionStatus.Continue:
                infra.StatusReporter.ReportSuccess(stepId, result.SuccessMessage ?? "", result.Limits);
                break;
            case PreExecutionStatus.Cancelled:
                break;
            case PreExecutionStatus.TestStarted:
                break;
            case PreExecutionStatus.Failed:
                infra.StatusReporter.ReportError(stepId, result.ErrorMessage!, result.Limits);
                break;
            default:
                throw new InvalidOperationException($"Неизвестный статус PreExecution: {result.Status}");
        }
    }

    private PreExecutionResult HandleStepException(ScanStepBase step, Exception ex)
    {
        logger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        infra.TestStepLogger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        return PreExecutionResult.Fail(ex.Message);
    }

    private PreExecutionResult HandleStepException(BlockBoilerAdapterStep step, Guid stepId, Exception ex)
    {
        logger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        infra.TestStepLogger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        infra.StatusReporter.ReportError(stepId, ex.Message);
        return PreExecutionResult.Fail(ex.Message);
    }

    private void StartTestExecution(PreExecutionContext context)
    {
        var mapCount = context.Maps?.Count ?? 0;
        logger.LogInformation("Запуск TestExecutionCoordinator с {Count} maps", mapCount);
        infra.TestStepLogger.LogInformation("Запуск TestExecutionCoordinator с {Count} maps", mapCount);

        coordinators.TestCoordinator.SetMaps(context.Maps!);
        _ = StartTestWithErrorHandlingAsync();
    }

    private async Task StartTestWithErrorHandlingAsync()
    {
        try
        {
            await coordinators.TestCoordinator.StartAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка запуска теста");
            infra.TestStepLogger.LogError(ex, "Ошибка запуска теста");
        }
    }

    private PreExecutionContext CreateContext(string barcode)
    {
        return new PreExecutionContext
        {
            Barcode = barcode,
            BoilerState = state.BoilerState,
            OpcUa = infra.OpcUa,
            TestStepLogger = infra.TestStepLogger
        };
    }
}
