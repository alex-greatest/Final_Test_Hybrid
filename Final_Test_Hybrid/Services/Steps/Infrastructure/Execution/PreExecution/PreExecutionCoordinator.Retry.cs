using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
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
        plcResetCoordinator.OnForceStop += HandleSoftStop;
        errorCoordinator.OnReset += HandleHardReset;
    }

    private void HandleSoftStop() => SignalResolution(PreExecutionResolution.SoftStop);
    private void HandleHardReset() => SignalResolution(PreExecutionResolution.HardReset);

    private void SignalResolution(PreExecutionResolution resolution)
    {
        errorService.ClearActiveApplicationErrors();
        _externalSignal?.TrySetResult(resolution);
    }

    #endregion

    #region Retry Loop

    private async Task<PreExecutionResult> ExecuteRetryLoopAsync(
        IPreExecutionStep step,
        PreExecutionResult initialResult,
        PreExecutionContext context,
        Guid stepId,
        CancellationToken ct)
    {
        var errorScope = new ErrorScope(errorService);
        var currentResult = initialResult;
        try
        {
            while (currentResult.IsRetryable)
            {
                await SetSelectedAsync(step);
                errorScope.Raise(currentResult.Errors, step.Id, step.Name);
                statusReporter.ReportError(stepId, currentResult.ErrorMessage!);

                var resolution = await WaitForResolutionAsync(ct);
                // PLC сам сбросит Selected — нам не нужно
                if (resolution == PreExecutionResolution.Retry)
                {
                    errorScope.Clear();
                    currentResult = await RetryStepAsync(step, context, stepId, ct);
                }
                else
                {
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
        IPreExecutionStep step,
        PreExecutionContext context,
        Guid stepId,
        CancellationToken ct)
    {
        statusReporter.ReportRetry(stepId);
        return await ExecuteAndRecordAsync(step, context, ct);
    }

    #endregion

    #region Wait For Resolution

    private async Task<PreExecutionResolution> WaitForResolutionAsync(CancellationToken ct)
    {
        var signal = _externalSignal = new TaskCompletionSource<PreExecutionResolution>();
        try
        {
            var completedTask = await WaitForFirstSignalAsync(signal, ct);
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
        var resolutionTask = errorCoordinator.WaitForResolutionAsync(ct);
        var externalTask = signal.Task;

        return Task.WhenAny(resolutionTask, externalTask);
    }

    private static async Task<PreExecutionResolution> ExtractResolutionAsync(
        Task completedTask,
        TaskCompletionSource<PreExecutionResolution> signal)
    {
        if (completedTask == signal.Task)
        {
            return await signal.Task;
        }

        var errorResolutionTask = (Task<ErrorResolution>)completedTask;
        var errorResolution = await errorResolutionTask;

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

    private async Task SetSelectedAsync(IPreExecutionStep step)
    {
        if (step is not IHasPlcBlockPath plcStep)
        {
            return;
        }
        var selectedTag = $"ns=3;s=\"{plcStep.PlcBlockPath}\".\"Selected\"";
        logger.LogDebug("Взведение Selected для {BlockPath}", plcStep.PlcBlockPath);
        var result = await plcService.WriteAsync(selectedTag, true);
        if (result.Error != null)
        {
            logger.LogWarning("Ошибка записи Selected: {Error}", result.Error);
        }
    }
    #endregion
}
