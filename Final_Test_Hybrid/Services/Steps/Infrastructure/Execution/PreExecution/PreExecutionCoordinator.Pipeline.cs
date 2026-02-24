using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator
{
    private async Task<PreExecutionResult> ExecutePreExecutionPipelineAsync(
        string barcode,
        CancellationToken ct)
    {
        var context = CreateContext(barcode);
        try
        {
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

            // Сохраняем context после успешного ScanStep (для повтора)
            _lastSuccessfulContext = context;

            // Общий старт теста (очистка, история, флаги, changeover)
            var initializeError = await InitializeTestRunningAsync(context, ct);
            if (initializeError != null)
            {
                infra.StatusReporter.UpdateScanStepStatus(
                    TestStepStatus.Error,
                    initializeError.ErrorMessage ?? "Ошибка",
                    initializeError.Limits);
                return initializeError;
            }

            infra.StatusReporter.UpdateScanStepStatus(
                TestStepStatus.Success,
                scanResult.SuccessMessage ?? "",
                scanResult.Limits);

            ct.ThrowIfCancellationRequested();

            var timerResult = await ExecuteStartTimer1Async(context, ct);
            if (timerResult.Status != PreExecutionStatus.Continue)
            {
                return timerResult;
            }

            var blockResult = await ExecuteBlockBoilerAdapterAsync(context, ct);
            if (blockResult.Status != PreExecutionStatus.Continue)
            {
                return HandleNonContinueResult(blockResult);
            }

            return !StartTestExecution(context) ? PreExecutionResult.Fail("Test execution did not start") : PreExecutionResult.TestStarted();
        }
        finally
        {
            state.PhaseState.Clear();
        }
    }

    private async Task<PreExecutionResult> ExecuteRepeatPipelineAsync(CancellationToken ct)
    {
        var context = _lastSuccessfulContext;
        try
        {
            if (context?.Maps == null)
            {
                infra.Logger.LogError("Нет сохранённого контекста для повтора");
                return PreExecutionResult.Fail("Нет данных для повтора");
            }

            // Пропускаем ScanStep - данные уже загружены
            // Общий старт теста (очистка, история, флаги, changeover)
            var initializeError = await InitializeTestRunningAsync(context, ct);
            if (initializeError != null)
            {
                infra.StatusReporter.UpdateScanStepStatus(
                    TestStepStatus.Error,
                    initializeError.ErrorMessage ?? "Ошибка",
                    initializeError.Limits);
                return initializeError;
            }

            ct.ThrowIfCancellationRequested();

            var timerResult = await ExecuteStartTimer1Async(context, ct);
            if (timerResult.Status != PreExecutionStatus.Continue)
            {
                return timerResult;
            }

            var blockResult = await ExecuteBlockBoilerAdapterAsync(context, ct);
            if (blockResult.Status != PreExecutionStatus.Continue)
            {
                return HandleNonContinueResult(blockResult);
            }

            return !StartTestExecution(context) ? PreExecutionResult.Fail("Test execution did not start") : PreExecutionResult.TestStarted();
        }
        finally
        {
            state.PhaseState.Clear();
        }
    }

    private async Task<PreExecutionResult> ExecuteNokRepeatPipelineAsync(CancellationToken ct)
    {
        try
        {
            if (CurrentBarcode == null)
            {
                infra.Logger.LogError("NOK повтор: CurrentBarcode is null");
                return PreExecutionResult.Fail("Отсутствует штрихкод для повтора");
            }

            infra.Logger.LogInformation("NOK повтор: запуск полной подготовки с barcode={Barcode}", CurrentBarcode);

            while (!ct.IsCancellationRequested)
            {
                // Выполнить полный pipeline с сохранённым штрихкодом
                var result = await ExecutePreExecutionPipelineAsync(CurrentBarcode, ct);

                if (result.Status is PreExecutionStatus.TestStarted or PreExecutionStatus.Cancelled)
                {
                    return result;
                }

                // Показать диалог ошибки подготовки
                var shouldRetry = await ShowPrepareErrorDialogAsync(result.ErrorMessage);
                if (!shouldRetry)
                {
                    return PreExecutionResult.Cancelled();
                }

                infra.Logger.LogInformation("NOK повтор: повторная попытка подготовки");
            }

            return PreExecutionResult.Cancelled();
        }
        finally
        {
            state.PhaseState.Clear();
        }
    }

    private Task<bool> ShowPrepareErrorDialogAsync(string? errorMessage)
    {
        return coordinators.CompletionCoordinator.ShowPrepareErrorDialogAsync(errorMessage);
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
            infra.Logger.LogError(ex, "Ошибка в шаге StartTimer1");
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
                var retryResult = await ExecuteRetryLoopAsync(steps.BlockBoilerAdapter, result, context, stepId, ct);
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
}
