using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Plc;
using Final_Test_Hybrid.Services.Steps.Steps;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator
{
    private TaskCompletionSource<PreExecutionResolution>? _externalSignal;
    private bool _subscribed;

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
        if (TryCancelActiveOperation())
        {
            // Очистка произойдёт позже: в catch блоке или HandlePostTestCompletion
        }
        else
        {
            // Нет активной операции — очищаем сразу
            ClearStateOnReset();
        }
        SignalResolution(resolution);
    }

    private bool TryCancelActiveOperation()
    {
        if (coordinators.TestCoordinator.IsRunning)
        {
            _resetRequested = true;
            return true;
        }
        if (state.ActivityTracker.IsPreExecutionActive)
        {
            _resetRequested = true;
            _currentCts?.Cancel();
            return true;
        }
        return false;
    }

    private void HandleGridClear()
    {
        infra.StatusReporter.ClearAllExceptScan();
    }

    private void HandleSoftStop() => HandleStopSignal(PreExecutionResolution.SoftStop);

    private void HandleHardReset()
    {
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
                    currentResult.UserMessage ?? currentResult.ErrorMessage!);

                infra.Logger.LogInformation("Диалог показан, ожидаем WaitForResolutionAsync...");
                var resolution = await WaitForResolutionAsync(ct);
                infra.Logger.LogInformation("WaitForResolutionAsync вернул: {Resolution}", resolution);

                if (resolution == PreExecutionResolution.Retry)
                {
                    infra.Logger.LogInformation("Отправляем SendAskRepeatAsync...");
                    var errorTag = GetBlockErrorTag(step);
                    await coordinators.ErrorCoordinator.SendAskRepeatAsync(errorTag, ct);
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

    private async Task<PreExecutionResolution> WaitForResolutionAsync(CancellationToken ct)
    {
        infra.Logger.LogDebug("WaitForResolutionAsync: создаём _externalSignal");
        var signal = _externalSignal = new TaskCompletionSource<PreExecutionResolution>();
        try
        {
            var completedTask = await WaitForFirstSignalAsync(signal, ct);
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
        CancellationToken ct)
    {
        infra.Logger.LogDebug("WaitForFirstSignalAsync: вызываем errorCoordinator.WaitForResolutionAsync");
        var resolutionTask = coordinators.ErrorCoordinator.WaitForResolutionAsync(ct);
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
        var selectedTag = PlcBlockTagHelper.GetSelectedTag(step as IHasPlcBlockPath);
        if (selectedTag == null) return;

        infra.Logger.LogDebug("Взведение Selected для {BlockPath}: {Tag}",
            (step as IHasPlcBlockPath)?.PlcBlockPath, selectedTag);
        var result = await infra.PlcService.WriteAsync(selectedTag, true);
        if (result.Error != null)
        {
            infra.Logger.LogWarning("Ошибка записи Selected: {Error}", result.Error);
        }
    }

    #endregion

    #region Block Error Tag

    private static string? GetBlockErrorTag(BlockBoilerAdapterStep step)
    {
        return PlcBlockTagHelper.GetErrorTag(step as IHasPlcBlockPath);
    }

    #endregion
}
