using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator(
    IPreExecutionStepRegistry stepRegistry,
    TestExecutionCoordinator testCoordinator,
    StepStatusReporter statusReporter,
    BoilerState boilerState,
    PausableOpcUaTagService opcUa,
    OpcUaTagService plcService,
    ITestStepLogger testStepLogger,
    ExecutionActivityTracker activityTracker,
    ExecutionMessageState messageState,
    PauseTokenSource pauseToken,
    ErrorCoordinator errorCoordinator,
    PlcResetCoordinator plcResetCoordinator,
    IErrorService errorService,
    ILogger<PreExecutionCoordinator> logger)
{
    public async Task<PreExecutionResult> ExecuteAsync(string barcode, Guid? scanStepId, CancellationToken ct)
    {
        EnsureSubscribed();
        activityTracker.SetPreExecutionActive(true);
        try
        {
            return await ExecutePreExecutionPipelineAsync(barcode, scanStepId, ct);
        }
        finally
        {
            activityTracker.SetPreExecutionActive(false);
        }
    }

    private async Task<PreExecutionResult> ExecutePreExecutionPipelineAsync(
        string barcode,
        Guid? scanStepId,
        CancellationToken ct)
    {
        var context = CreateContext(barcode, scanStepId);
        var stepsResult = await ExecuteAllStepsAsync(context, ct);
        if (stepsResult.Status != PreExecutionStatus.Continue)
        {
            return HandleNonContinueResult(stepsResult);
        }
        messageState.Clear();
        StartTestExecution(context);
        return PreExecutionResult.TestStarted();
    }

    private PreExecutionResult HandleNonContinueResult(PreExecutionResult result)
    {
        if (result.Status == PreExecutionStatus.Failed && result.UserMessage != null)
        {
            messageState.SetMessage(result.UserMessage);
        }
        return result;
    }

    private async Task<PreExecutionResult> ExecuteAllStepsAsync(PreExecutionContext context, CancellationToken ct)
    {
        foreach (var step in stepRegistry.GetOrderedSteps())
        {
            await pauseToken.WaitWhilePausedAsync(ct);
            var result = await ExecuteStepAsync(step, context, ct);
            if (result.Status != PreExecutionStatus.Continue)
            {
                return result;
            }
        }
        return PreExecutionResult.Continue();
    }

    private async Task<PreExecutionResult> ExecuteStepAsync(
        IPreExecutionStep step,
        PreExecutionContext context,
        CancellationToken ct)
    {
        var stepId = GetOrCreateStepId(step, context);
        try
        {
            return await ExecuteStepCoreAsync(step, stepId, context, ct);
        }
        catch (Exception ex)
        {
            return HandleStepException(step, stepId, ex);
        }
    }

    private Guid GetOrCreateStepId(IPreExecutionStep step, PreExecutionContext context)
    {
        return context.ScanStepId.HasValue ? ReuseExistingScanStepId(context) : CreateNewStepId(step);
    }

    private Guid CreateNewStepId(IPreExecutionStep step)
    {
        return statusReporter.ReportStepStarted(step);
    }

    private Guid ReuseExistingScanStepId(PreExecutionContext context)
    {
        var stepId = context.ScanStepId!.Value;
        statusReporter.ReportRetry(stepId);
        context.ScanStepId = null;
        return stepId;
    }

    private async Task<PreExecutionResult> ExecuteStepCoreAsync(
        IPreExecutionStep step,
        Guid stepId,
        PreExecutionContext context,
        CancellationToken ct)
    {
        var result = await step.ExecuteAsync(context, ct);
        if (result.IsRetryable)
        {
            return await ExecuteRetryLoopAsync(step, result, context, stepId, ct);
        }
        ReportStepResult(stepId, result);
        return result;
    }

    private void ReportStepResult(Guid stepId, PreExecutionResult result)
    {
        switch (result.Status)
        {
            case PreExecutionStatus.Continue:
                statusReporter.ReportSuccess(stepId, result.SuccessMessage ?? "");
                break;

            case PreExecutionStatus.Cancelled:
                statusReporter.ReportError(stepId, result.ErrorMessage ?? "Операция отменена");
                break;

            case PreExecutionStatus.TestStarted:
                break;

            case PreExecutionStatus.Failed:
                statusReporter.ReportError(stepId, result.ErrorMessage!);
                break;
            default:
                throw new InvalidOperationException($"Неизвестный статус PreExecution: {result.Status}");
        }
    }

    private PreExecutionResult HandleStepException(IPreExecutionStep step, Guid stepId, Exception ex)
    {
        logger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        testStepLogger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        statusReporter.ReportError(stepId, ex.Message);
        return PreExecutionResult.Fail(ex.Message);
    }

    private void StartTestExecution(PreExecutionContext context)
    {
        LogTestExecutionStart(context);
        testCoordinator.SetMaps(context.Maps!);
        _ = StartTestWithErrorHandlingAsync();
    }

    private async Task StartTestWithErrorHandlingAsync()
    {
        try
        {
            await testCoordinator.StartAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка запуска теста");
            testStepLogger.LogError(ex, "Ошибка запуска теста");
        }
    }

    private void LogTestExecutionStart(PreExecutionContext context)
    {
        var mapCount = context.Maps?.Count ?? 0;
        logger.LogInformation("Запуск TestExecutionCoordinator с {Count} maps", mapCount);
        testStepLogger.LogInformation("Запуск TestExecutionCoordinator с {Count} maps", mapCount);
    }

    private PreExecutionContext CreateContext(string barcode, Guid? scanStepId)
    {
        return new PreExecutionContext
        {
            Barcode = barcode,
            ScanStepId = scanStepId,
            BoilerState = boilerState,
            OpcUa = opcUa,
            TestStepLogger = testStepLogger
        };
    }
}
