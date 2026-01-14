using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.SpringBoot.Operation;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Services.Steps.Steps;
using Final_Test_Hybrid.Services.Steps.Validation;
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
    ScanDialogCoordinator dialogCoordinator,
    ILogger<PreExecutionCoordinator> logger)
{
    // === Состояние ввода ===
    private TaskCompletionSource<string>? _barcodeSource;
    private CancellationTokenSource? _currentCts;
    private volatile bool _resetRequested;

    public bool IsAcceptingInput { get; private set; }
    public bool IsProcessing => !IsAcceptingInput && activityTracker.IsPreExecutionActive;
    public string? CurrentBarcode { get; private set; }
    public event Action? OnStateChanged;

    // === Проксирование событий диалогов ===
    public event Func<IReadOnlyList<string>, Task>? OnMissingPlcTagsDialogRequested
    {
        add => dialogCoordinator.OnMissingPlcTagsDialogRequested += value;
        remove => dialogCoordinator.OnMissingPlcTagsDialogRequested -= value;
    }

    public event Func<IReadOnlyList<string>, Task>? OnMissingRequiredTagsDialogRequested
    {
        add => dialogCoordinator.OnMissingRequiredTagsDialogRequested += value;
        remove => dialogCoordinator.OnMissingRequiredTagsDialogRequested -= value;
    }

    public event Func<IReadOnlyList<UnknownStepInfo>, Task>? OnUnknownStepsDialogRequested
    {
        add => dialogCoordinator.OnUnknownStepsDialogRequested += value;
        remove => dialogCoordinator.OnUnknownStepsDialogRequested -= value;
    }

    public event Func<IReadOnlyList<MissingRecipeInfo>, Task>? OnMissingRecipesDialogRequested
    {
        add => dialogCoordinator.OnMissingRecipesDialogRequested += value;
        remove => dialogCoordinator.OnMissingRecipesDialogRequested -= value;
    }

    public event Func<IReadOnlyList<RecipeWriteErrorInfo>, Task>? OnRecipeWriteErrorDialogRequested
    {
        add => dialogCoordinator.OnRecipeWriteErrorDialogRequested += value;
        remove => dialogCoordinator.OnRecipeWriteErrorDialogRequested -= value;
    }

    public event Func<string, Func<string, string, Task<ReworkSubmitResult>>, Task<ReworkFlowResult>>? OnReworkDialogRequested
    {
        add => dialogCoordinator.OnReworkDialogRequested += value;
        remove => dialogCoordinator.OnReworkDialogRequested -= value;
    }

    public void ClearBarcode()
    {
        CurrentBarcode = null;
        OnStateChanged?.Invoke();
    }

    public void SubmitBarcode(string barcode)
    {
        _barcodeSource?.TrySetResult(barcode);
    }

    private void SetAcceptingInput(bool value)
    {
        IsAcceptingInput = value;
        OnStateChanged?.Invoke();
    }

    public async Task StartMainLoopAsync(CancellationToken ct)
    {
        EnsureSubscribed();
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

    private async Task RunSingleCycleAsync(CancellationToken ct)
    {
        statusReporter.UpdateScanStepStatus(TestStepStatus.Running, "Ожидание сканирования");
        SetAcceptingInput(true);

        var barcode = await WaitForBarcodeAsync(ct);
        SetAcceptingInput(false);

        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            activityTracker.SetPreExecutionActive(true);
            var result = await ExecutePreExecutionPipelineAsync(barcode, _currentCts.Token);

            if (result.Status == PreExecutionStatus.TestStarted)
            {
                await WaitForTestCompletionAsync(ct);
                HandlePostTestCompletion();
            }
        }
        catch (OperationCanceledException) when (_resetRequested)
        {
            _resetRequested = false;
        }
        finally
        {
            activityTracker.SetPreExecutionActive(false);
            _currentCts?.Dispose();
            _currentCts = null;
        }
    }

    private async Task<string> WaitForBarcodeAsync(CancellationToken ct)
    {
        _barcodeSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var reg = ct.Register(() => _barcodeSource.TrySetCanceled());
        var barcode = await _barcodeSource.Task;
        CurrentBarcode = barcode;
        OnStateChanged?.Invoke();
        return barcode;
    }

    private async Task WaitForTestCompletionAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler() => tcs.TrySetResult();
        testCoordinator.OnSequenceCompleted += Handler;

        try
        {
            await using var reg = ct.Register(() => tcs.TrySetCanceled());
            await tcs.Task;
        }
        finally
        {
            testCoordinator.OnSequenceCompleted -= Handler;
        }
    }

    private void HandlePostTestCompletion()
    {
        dialogCoordinator.ShowCompletionNotification(testCoordinator.HasErrors);
        if (_resetRequested)
        {
            statusReporter.ClearAllExceptScan();
            _resetRequested = false;
        }
    }

    private async Task<PreExecutionResult> ExecutePreExecutionPipelineAsync(
        string barcode,
        CancellationToken ct)
    {
        var context = CreateContext(barcode);

        var scanResult = await ExecuteScanStepAsync(context, ct);

        if (scanResult.Status == PreExecutionStatus.Failed)
        {
            statusReporter.UpdateScanStepStatus(
                TestStepStatus.Error,
                scanResult.ErrorMessage ?? "Ошибка",
                scanResult.Limits);
            await dialogCoordinator.HandlePreExecutionErrorAsync(scanResult);
            return scanResult;
        }

        if (scanResult.Status == PreExecutionStatus.Cancelled)
        {
            return scanResult;
        }

        statusReporter.UpdateScanStepStatus(
            TestStepStatus.Success,
            scanResult.SuccessMessage ?? "",
            scanResult.Limits);

        var blockResult = await ExecuteBlockBoilerAdapterAsync(context, ct);
        if (blockResult.Status != PreExecutionStatus.Continue)
        {
            return HandleNonContinueResult(blockResult);
        }

        messageState.Clear();
        StartTestExecution(context);
        return PreExecutionResult.TestStarted();
    }

    private async Task<PreExecutionResult> ExecuteScanStepAsync(PreExecutionContext context, CancellationToken ct)
    {
        var scanStep = GetScanStep();
        try
        {
            await pauseToken.WaitWhilePausedAsync(ct);
            var startTime = DateTime.Now;
            var result = await scanStep.ExecuteAsync(context, ct);
            stepTimingService.Record(scanStep.Name, scanStep.Description, DateTime.Now - startTime);
            return result;
        }
        catch (Exception ex)
        {
            return HandleStepException(scanStep, ex);
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

            ReportBlockStepResult(stepId, result);
            return result;
        }
        catch (Exception ex)
        {
            return HandleStepException(blockBoilerAdapterStep, stepId, ex);
        }
    }

    public ScanStepBase GetScanStep()
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

    private void ReportBlockStepResult(Guid stepId, PreExecutionResult result)
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

    private PreExecutionResult HandleStepException(ScanStepBase step, Exception ex)
    {
        logger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        testStepLogger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
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

    private PreExecutionContext CreateContext(string barcode)
    {
        return new PreExecutionContext
        {
            Barcode = barcode,
            BoilerState = boilerState,
            OpcUa = opcUa,
            TestStepLogger = testStepLogger
        };
    }
}
