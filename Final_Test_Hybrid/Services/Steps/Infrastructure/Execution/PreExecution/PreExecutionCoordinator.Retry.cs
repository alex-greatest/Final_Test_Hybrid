using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Plc;
using Final_Test_Hybrid.Services.Steps.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator
{
    private TaskCompletionSource<PreExecutionResolution>? _externalSignal;
    private bool _subscribed;
    private int _interruptDialogActive;
    private CancellationTokenSource? _interruptDialogCts;

    #region Subscriptions

    private void EnsureSubscribed()
    {
        if (_subscribed)
        {
            return;
        }
        _subscribed = true;
        SubscribeToStopSignals();
    }

    private void SubscribeToStopSignals()
    {
        coordinators.PlcResetCoordinator.OnForceStop += HandleSoftStop;
        coordinators.PlcResetCoordinator.OnAskEndReceived += HandleGridClear;
        coordinators.ErrorCoordinator.OnReset += HandleHardReset;
    }

    private void HandleStopSignal(PreExecutionResolution resolution)
    {
        infra.StepTimingService.PauseAllColumnsTiming();
        var exitReason = resolution == PreExecutionResolution.SoftStop
            ? CycleExitReason.SoftReset
            : CycleExitReason.HardReset;
        var stopReason = resolution == PreExecutionResolution.SoftStop
            ? ExecutionStopReason.PlcSoftReset
            : ExecutionStopReason.PlcHardReset;
        state.FlowState.RequestStop(stopReason, stopAsFailure: true);
        SignalReset(exitReason);

        if (TryCancelActiveOperation(exitReason))
        {
            // Очистка произойдёт в HandleCycleExit
        }
        else
        {
            // Нет активной операции — очищаем сразу
            HandleCycleExit(exitReason);
        }
        SignalResolution(resolution);
    }

    private bool TryCancelActiveOperation(CycleExitReason exitReason)
    {
        if (coordinators.TestCoordinator.IsRunning || state.ActivityTracker.IsPreExecutionActive)
        {
            _pendingExitReason = exitReason;
            _currentCts?.Cancel();
            return true;
        }
        return false;
    }

    private async void HandleGridClear()
    {
        try
        {
            await ExecuteGridClearAsync();
        }
        catch (Exception ex)
        {
            infra.Logger.LogError(ex, "Ошибка в HandleGridClear");
            ResetInterruptState();
            CompletePlcReset();
        }
    }

    private async Task ExecuteGridClearAsync()
    {
        var context = CaptureAndClearState();

        if (!ShouldShowInterruptDialog(context))
        {
            CompletePlcReset();
            return;
        }
        await TryShowInterruptDialogAsync(context.SerialNumber!);
        CompletePlcReset();
    }

    private (bool WasTestRunning, string? SerialNumber) CaptureAndClearState()
    {
        var wasTestRunning = state.BoilerState.IsTestRunning;
        var serialNumber = state.BoilerState.SerialNumber;

        ClearStateOnReset();
        infra.StatusReporter.ClearAllExceptScan();

        return (wasTestRunning, serialNumber);
    }

    private bool ShouldShowInterruptDialog((bool WasTestRunning, string? SerialNumber) context)
    {
        return context.WasTestRunning
            && context.SerialNumber != null
            && infra.AppSettings.UseInterruptReason;
    }

    private async Task TryShowInterruptDialogAsync(string serialNumber)
    {
        if (!TryAcquireDialogLock())
        {
            CancelActiveDialog();
            return;
        }
        var cts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _interruptDialogCts, cts);
        DisposeOldCts(oldCts);

        try
        {
            await ShowInterruptReasonDialogAsync(serialNumber, cts.Token);
        }
        finally
        {
            ReleaseDialogLock(cts);
        }
    }

    private bool TryAcquireDialogLock()
    {
        return Interlocked.CompareExchange(ref _interruptDialogActive, 1, 0) == 0;
    }

    private static void DisposeOldCts(CancellationTokenSource? oldCts)
    {
        oldCts?.Dispose();
    }

    private void CancelActiveDialog()
    {
        var currentCts = Volatile.Read(ref _interruptDialogCts);
        SafeCancel(currentCts);
    }

    private void ReleaseDialogLock(CancellationTokenSource cts)
    {
        Interlocked.Exchange(ref _interruptDialogActive, 0);
        Interlocked.CompareExchange(ref _interruptDialogCts, null, cts);
        cts.Dispose();
    }

    private void ResetInterruptState()
    {
        Interlocked.Exchange(ref _interruptDialogActive, 0);
    }

    private static void SafeCancel(CancellationTokenSource? cts)
    {
        if (cts == null)
        {
            return;
        }
        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task ShowInterruptReasonDialogAsync(string serialNumber, CancellationToken ct)
    {
        void HandleCancel() => CancelActiveDialog();
        coordinators.PlcResetCoordinator.OnForceStop += HandleCancel;
        coordinators.ErrorCoordinator.OnReset += HandleCancel;

        try
        {
            var useMes = infra.AppSettings.UseMes;
            var operatorUsername = state.OperatorState.Username ?? "Unknown";

            var result = await coordinators.DialogCoordinator.ShowInterruptReasonDialogAsync(
                serialNumber,
                (admin, reason, token) => infra.InterruptReasonRouter.SaveAsync(
                    serialNumber, admin, reason, useMes, token),
                useMes,
                operatorUsername,
                ct);

            LogInterruptResult(result);
        }
        finally
        {
            coordinators.PlcResetCoordinator.OnForceStop -= HandleCancel;
            coordinators.ErrorCoordinator.OnReset -= HandleCancel;
        }
    }

    private void LogInterruptResult(InterruptFlowResult result)
    {
        if (result.IsSuccess)
        {
            infra.Logger.LogInformation("Причина прерывания сохранена: {Admin}", result.AdminUsername);
        }
    }

    private void HandleSoftStop()
    {
        BeginPlcReset();
        HandleStopSignal(PreExecutionResolution.SoftStop);
    }

    private void HandleHardReset()
    {
        TryCompletePlcReset();
        HandleStopSignal(PreExecutionResolution.HardReset);
        infra.StatusReporter.ClearAllExceptScan();
    }

    private void SignalResolution(PreExecutionResolution resolution)
    {
        infra.ErrorService.ClearActiveApplicationErrors();
        _externalSignal?.TrySetResult(resolution);
    }

    #endregion

    #region Retry Loop

    private async Task<PreExecutionResult> ExecuteRetryLoopAsync(
        BlockBoilerAdapterStep step,
        PreExecutionResult initialResult,
        PreExecutionContext context,
        Guid stepId,
        CancellationToken ct)
    {
        infra.Logger.LogInformation("Вход в ExecuteRetryLoopAsync для {StepName}", step.Name);
        var errorScope = new ErrorScope(infra.ErrorService);
        var currentResult = initialResult;
        try
        {
            while (currentResult.IsRetryable)
            {
                infra.Logger.LogInformation("Retry loop: IsRetryable=true, показываем диалог");
                await SetSelectedAsync(step);
                errorScope.Raise(currentResult.Errors, step.Id, step.Name);
                infra.StatusReporter.ReportError(stepId, currentResult.ErrorMessage!);

                await coordinators.DialogCoordinator.ShowBlockErrorDialogAsync(
                    step.Name,
                    currentResult.UserMessage ?? currentResult.ErrorMessage!,
                    step.ErrorSourceTitle);

                infra.Logger.LogInformation("Диалог показан, ожидаем WaitForResolutionAsync...");
                var resolution = await WaitForResolutionAsync(step, ct);
                infra.Logger.LogInformation("WaitForResolutionAsync вернул: {Resolution}", resolution);

                if (resolution == PreExecutionResolution.Retry)
                {
                    infra.Logger.LogInformation("Отправляем SendAskRepeatAsync...");
                    var errorTag = GetBlockErrorTag(step);
                    try
                    {
                        await coordinators.ErrorCoordinator.SendAskRepeatAsync(errorTag, ct);
                    }
                    catch (TimeoutException)
                    {
                        infra.Logger.LogError("Block.Error не сброшен за 5 сек — жёсткий стоп pre-execution");
                        coordinators.DialogCoordinator.CloseBlockErrorDialog();
                        await coordinators.ErrorCoordinator.HandleInterruptAsync(
                            ErrorCoordinator.InterruptReason.TagTimeout, ct);
                        return PreExecutionResult.Fail("Таймаут ожидания Block.Error");
                    }
                    coordinators.DialogCoordinator.CloseBlockErrorDialog();
                    infra.Logger.LogInformation("SendAskRepeatAsync отправлен, повторяем шаг");
                    errorScope.Clear();
                    currentResult = await RetryStepAsync(step, context, stepId, ct);
                }
                else
                {
                    coordinators.DialogCoordinator.CloseBlockErrorDialog();
                    infra.Logger.LogInformation("Не Retry, выходим из цикла с {Resolution}", resolution);
                    return CreateExitResult(resolution, currentResult);
                }
            }
            return currentResult;
        }
        finally
        {
            errorScope.Clear();
        }
    }

    private static PreExecutionResult CreateExitResult(
        PreExecutionResolution resolution,
        PreExecutionResult failedResult)
    {
        return resolution switch
        {
            PreExecutionResolution.Skip when failedResult.CanSkip => PreExecutionResult.Continue(),
            PreExecutionResolution.SoftStop => PreExecutionResult.Cancelled("Остановлено оператором"),
            PreExecutionResolution.HardReset => PreExecutionResult.Fail("Сброс теста"),
            _ => failedResult with { IsRetryable = false }
        };
    }

    private async Task<PreExecutionResult> RetryStepAsync(
        BlockBoilerAdapterStep step,
        PreExecutionContext context,
        Guid stepId,
        CancellationToken ct)
    {
        infra.StatusReporter.ReportRetry(stepId);
        infra.StepTimingService.StartCurrentStepTiming(step.Name, step.Description);
        var result = await step.ExecuteAsync(context, ct);
        infra.StepTimingService.StopCurrentStepTiming();
        return result;
    }

    #endregion

    #region Wait For Resolution

    private async Task<PreExecutionResolution> WaitForResolutionAsync(BlockBoilerAdapterStep step, CancellationToken ct)
    {
        infra.Logger.LogDebug("WaitForResolutionAsync: создаём _externalSignal");
        var signal = _externalSignal = new TaskCompletionSource<PreExecutionResolution>();

        // Передаём block-теги только для пропускаемых шагов
        string? blockEndTag = null;
        string? blockErrorTag = null;
        if (step.IsSkippable)
        {
            blockEndTag = GetBlockEndTag(step);
            blockErrorTag = GetBlockErrorTag(step);
        }

        try
        {
            var completedTask = await WaitForFirstSignalAsync(signal, blockEndTag, blockErrorTag, step.IsSkippable, ct);
            infra.Logger.LogDebug("WaitForResolutionAsync: получили completedTask");
            return await ExtractResolutionAsync(completedTask, signal);
        }
        finally
        {
            _externalSignal = null;
        }
    }

    private Task<Task> WaitForFirstSignalAsync(
        TaskCompletionSource<PreExecutionResolution> signal,
        string? blockEndTag,
        string? blockErrorTag,
        bool enableSkip,
        CancellationToken ct)
    {
        infra.Logger.LogDebug("WaitForFirstSignalAsync: вызываем errorCoordinator.WaitForResolutionAsync (enableSkip={EnableSkip})", enableSkip);
        var options = new ErrorCoordinator.WaitForResolutionOptions(
            BlockEndTag: blockEndTag,
            BlockErrorTag: blockErrorTag,
            EnableSkip: enableSkip);
        var resolutionTask = coordinators.ErrorCoordinator.WaitForResolutionAsync(options, ct);
        var externalTask = signal.Task;

        return Task.WhenAny(resolutionTask, externalTask);
    }

    private async Task<PreExecutionResolution> ExtractResolutionAsync(
        Task completedTask,
        TaskCompletionSource<PreExecutionResolution> signal)
    {
        if (completedTask == signal.Task)
        {
            infra.Logger.LogDebug("ExtractResolutionAsync: сработал externalSignal");
            return await signal.Task;
        }
        var errorResolutionTask = (Task<ErrorResolution>)completedTask;
        var errorResolution = await errorResolutionTask;
        infra.Logger.LogDebug("ExtractResolutionAsync: errorCoordinator вернул {Resolution}", errorResolution);
        return MapToPreExecutionResolution(errorResolution);
    }

    private static PreExecutionResolution MapToPreExecutionResolution(ErrorResolution resolution)
    {
        return resolution switch
        {
            ErrorResolution.Retry => PreExecutionResolution.Retry,
            ErrorResolution.Skip => PreExecutionResolution.Skip,
            _ => PreExecutionResolution.Timeout
        };
    }

    #endregion

    #region Selected Management

    private async Task SetSelectedAsync(BlockBoilerAdapterStep step)
    {
        var selectedTag = PlcBlockTagHelper.GetSelectedTag(step);
        if (selectedTag == null) return;

        infra.Logger.LogDebug("Взведение Selected для {BlockPath}: {Tag}",
            (step as IHasPlcBlockPath).PlcBlockPath, selectedTag);
        var result = await infra.PlcService.WriteAsync(selectedTag, true);
        if (result.Error != null)
        {
            infra.Logger.LogWarning("Ошибка записи Selected: {Error}", result.Error);
        }
    }

    #endregion

    #region Block Tags

    private static string? GetBlockEndTag(BlockBoilerAdapterStep step)
    {
        return PlcBlockTagHelper.GetEndTag(step);
    }

    private static string? GetBlockErrorTag(BlockBoilerAdapterStep step)
    {
        return PlcBlockTagHelper.GetErrorTag(step);
    }

    #endregion
}
