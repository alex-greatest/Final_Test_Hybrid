using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Services.Steps.Steps;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

/// <summary>
/// Упрощённый координатор PreExecution.
/// Выполняет только два шага: ScanStep (вся подготовка) и BlockBoilerAdapterStep.
/// </summary>
public partial class PreExecutionCoordinator(
    AppSettingsService appSettings,
    ScanBarcodeStep scanBarcodeStep,
    ScanBarcodeMesStep scanBarcodeMesStep,
    BlockBoilerAdapterStep blockBoilerAdapterStep,
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
    IStepTimingService stepTimingService,
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

        // 1. Выполнить шаг сканирования (вся подготовка внутри)
        var scanResult = await ExecuteScanStepAsync(context, ct);
        if (scanResult.Status != PreExecutionStatus.Continue)
        {
            return HandleNonContinueResult(scanResult);
        }

        // 2. Выполнить BlockBoilerAdapter (с retry логикой)
        var blockResult = await ExecuteBlockBoilerAdapterAsync(context, ct);
        if (blockResult.Status != PreExecutionStatus.Continue)
        {
            return HandleNonContinueResult(blockResult);
        }

        // 3. Запустить тест
        messageState.Clear();
        StartTestExecution(context);
        return PreExecutionResult.TestStarted();
    }

    private async Task<PreExecutionResult> ExecuteScanStepAsync(PreExecutionContext context, CancellationToken ct)
    {
        var scanStep = GetScanStep();
        var stepId = GetOrCreateStepId(scanStep, context);
        try
        {
            await pauseToken.WaitWhilePausedAsync(ct);
            var startTime = DateTime.Now;
            var result = await scanStep.ExecuteAsync(context, ct);
            stepTimingService.Record(scanStep.Name, scanStep.Description, DateTime.Now - startTime);
            ReportStepResult(stepId, result);
            return result;
        }
        catch (Exception ex)
        {
            return HandleStepException(scanStep, stepId, ex);
        }
    }

    private async Task<PreExecutionResult> ExecuteBlockBoilerAdapterAsync(PreExecutionContext context, CancellationToken ct)
    {
        var stepId = statusReporter.ReportStepStarted(blockBoilerAdapterStep);
        try
        {
            await pauseToken.WaitWhilePausedAsync(ct);
            var startTime = DateTime.Now;
            var result = await blockBoilerAdapterStep.ExecuteAsync(context, ct);
            stepTimingService.Record(blockBoilerAdapterStep.Name, blockBoilerAdapterStep.Description, DateTime.Now - startTime);

            if (result.IsRetryable)
            {
                return await ExecuteRetryLoopAsync(blockBoilerAdapterStep, result, context, stepId, ct);
            }

            ReportStepResult(stepId, result);
            return result;
        }
        catch (Exception ex)
        {
            return HandleStepException(blockBoilerAdapterStep, stepId, ex);
        }
    }

    private ScanStepBase GetScanStep()
    {
        return appSettings.UseMes ? scanBarcodeMesStep : scanBarcodeStep;
    }

    private PreExecutionResult HandleNonContinueResult(PreExecutionResult result)
    {
        if (result is { Status: PreExecutionStatus.Failed, UserMessage: not null })
        {
            messageState.SetMessage(result.UserMessage);
        }
        return result;
    }

    private Guid GetOrCreateStepId(ScanStepBase step, PreExecutionContext context)
    {
        return context.ScanStepId.HasValue ? ReuseExistingScanStepId(context) : CreateNewStepId(step);
    }

    private Guid CreateNewStepId(ScanStepBase step)
    {
        return statusReporter.ReportStepStarted(step.Id, step.Name);
    }

    private Guid ReuseExistingScanStepId(PreExecutionContext context)
    {
        var stepId = context.ScanStepId!.Value;
        statusReporter.ReportRetry(stepId);
        context.ScanStepId = null;
        return stepId;
    }

    private void ReportStepResult(Guid stepId, PreExecutionResult result)
    {
        switch (result.Status)
        {
            case PreExecutionStatus.Continue:
                statusReporter.ReportSuccess(stepId, result.SuccessMessage ?? "", result.Limits);
                break;
            case PreExecutionStatus.Cancelled:
                statusReporter.ReportError(stepId, result.ErrorMessage ?? "Операция отменена", result.Limits);
                break;
            case PreExecutionStatus.TestStarted:
                break;
            case PreExecutionStatus.Failed:
                statusReporter.ReportError(stepId, result.ErrorMessage!, result.Limits);
                break;
            default:
                throw new InvalidOperationException($"Неизвестный статус PreExecution: {result.Status}");
        }
    }

    private PreExecutionResult HandleStepException(ScanStepBase step, Guid stepId, Exception ex)
    {
        logger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        testStepLogger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        statusReporter.ReportError(stepId, ex.Message);
        return PreExecutionResult.Fail(ex.Message);
    }

    private PreExecutionResult HandleStepException(BlockBoilerAdapterStep step, Guid stepId, Exception ex)
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
