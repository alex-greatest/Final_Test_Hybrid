using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Steps;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator
{
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

        state.PhaseState.Clear();
        StartTestExecution(context);
        return PreExecutionResult.TestStarted();
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

    private static PreExecutionResult HandleNonContinueResult(PreExecutionResult result)
    {
        // UserMessage теперь обрабатывается через ErrorService/NotificationService
        return result;
    }

    private void ReportBlockStepResult(Guid stepId, PreExecutionResult result)
    {
        switch (result.Status)
        {
            case PreExecutionStatus.Continue:
                infra.StatusReporter.ReportSuccess(stepId, result.SuccessMessage ?? "", result.Limits);
                break;
            case PreExecutionStatus.Cancelled:
                // Не меняем статус - шаг остаётся с тем статусом, который был
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
        infra.Logger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        infra.TestStepLogger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        return PreExecutionResult.Fail(ex.Message);
    }

    private PreExecutionResult HandleStepException(BlockBoilerAdapterStep step, Guid stepId, Exception ex)
    {
        infra.Logger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        infra.TestStepLogger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        infra.StatusReporter.ReportError(stepId, ex.Message);
        return PreExecutionResult.Fail(ex.Message);
    }

    private void StartTestExecution(PreExecutionContext context)
    {
        LogTestExecutionStart(context);
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
            infra.Logger.LogError(ex, "Ошибка запуска теста");
            infra.TestStepLogger.LogError(ex, "Ошибка запуска теста");
        }
    }

    private void LogTestExecutionStart(PreExecutionContext context)
    {
        var mapCount = context.Maps?.Count ?? 0;
        infra.Logger.LogInformation("Запуск TestExecutionCoordinator с {Count} maps", mapCount);
        infra.TestStepLogger.LogInformation("Запуск TestExecutionCoordinator с {Count} maps", mapCount);
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
