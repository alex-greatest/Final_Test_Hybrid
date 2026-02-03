using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Plc;
using Final_Test_Hybrid.Services.Steps.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator
{
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

        async Task<(PreExecutionResult Result, bool ShouldExit)> ProcessIterationAsync(PreExecutionResult result)
        {
            infra.Logger.LogInformation("Retry loop: IsRetryable=true, показываем диалог");
            await SetSelectedAsync(step);
            errorScope.Raise(result.Errors, step.Id, step.Name);
            infra.StatusReporter.ReportError(stepId, result.ErrorMessage!);

            await coordinators.DialogCoordinator.ShowBlockErrorDialogAsync(
                step.Name,
                result.UserMessage ?? result.ErrorMessage!,
                step.ErrorSourceTitle);

            infra.Logger.LogInformation("Диалог показан, ожидаем WaitForResolutionAsync...");
            var resolution = await WaitForResolutionAsync(step, ct);
            infra.Logger.LogInformation("WaitForResolutionAsync вернул: {Resolution}", resolution);

            if (resolution == PreExecutionResolution.Retry)
            {
                var retryResult = await ExecuteRetryAsync();
                return (retryResult, !retryResult.IsRetryable);
            }

            coordinators.DialogCoordinator.CloseBlockErrorDialog();
            infra.Logger.LogInformation("Не Retry, выходим из цикла с {Resolution}", resolution);
            return (CreateExitResult(resolution, result), true);
        }

        async Task<PreExecutionResult> ExecuteRetryAsync()
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
            return await RetryStepAsync(step, context, stepId, ct);
        }

        try
        {
            while (currentResult.IsRetryable)
            {
                var iteration = await ProcessIterationAsync(currentResult);
                if (iteration.ShouldExit)
                {
                    return iteration.Result;
                }
                currentResult = iteration.Result;
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
