using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Plc;
using Final_Test_Hybrid.Services.Steps.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

/// <summary>
/// Управляет циклом повтора шагов при ошибке.
/// Показывает диалог, ожидает сигналы PLC или пользователя, выполняет повтор.
/// </summary>
public class RetryCoordinator(
    PreExecutionInfrastructure infra,
    IErrorCoordinator errorCoordinator,
    ScanDialogCoordinator dialogCoordinator,
    DualLogger<RetryCoordinator> logger)
{
    private TaskCompletionSource<PreExecutionResolution>? _externalSignal;

    /// <summary>
    /// Сигнализирует внешнее разрешение (от PlcReset или ErrorCoordinator).
    /// </summary>
    public void SignalExternalResolution(PreExecutionResolution resolution)
    {
        infra.ErrorService.ClearActiveApplicationErrors();
        _externalSignal?.TrySetResult(resolution);
    }

    /// <summary>
    /// Выполняет цикл повтора шага до успеха, skip или отмены.
    /// </summary>
    public async Task<PreExecutionResult> ExecuteRetryLoopAsync(
        BlockBoilerAdapterStep step,
        PreExecutionResult initialResult,
        PreExecutionContext context,
        Guid stepId,
        CancellationToken ct)
    {
        logger.LogInformation("Вход в ExecuteRetryLoopAsync для {StepName}", step.Name);
        var errorScope = new ErrorScope(infra.ErrorService);
        var currentResult = initialResult;
        try
        {
            while (currentResult.IsRetryable)
            {
                logger.LogInformation("Retry loop: IsRetryable=true, показываем диалог");
                await SetSelectedAsync(step);
                errorScope.Raise(currentResult.Errors, step.Id, step.Name);
                infra.StatusReporter.ReportError(stepId, currentResult.ErrorMessage!);

                await dialogCoordinator.ShowBlockErrorDialogAsync(
                    step.Name,
                    currentResult.UserMessage ?? currentResult.ErrorMessage!);

                logger.LogInformation("Диалог показан, ожидаем WaitForResolutionAsync...");
                var resolution = await WaitForResolutionAsync(step, ct);
                logger.LogInformation("WaitForResolutionAsync вернул: {Resolution}", resolution);

                if (resolution == PreExecutionResolution.Retry)
                {
                    logger.LogInformation("Отправляем SendAskRepeatAsync...");
                    var errorTag = GetBlockErrorTag(step);
                    await errorCoordinator.SendAskRepeatAsync(errorTag, ct);
                    dialogCoordinator.CloseBlockErrorDialog();
                    logger.LogInformation("SendAskRepeatAsync отправлен, повторяем шаг");
                    errorScope.Clear();
                    currentResult = await RetryStepAsync(step, context, stepId, ct);
                }
                else
                {
                    dialogCoordinator.CloseBlockErrorDialog();
                    logger.LogInformation("Не Retry, выходим из цикла с {Resolution}", resolution);
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

    private async Task<PreExecutionResolution> WaitForResolutionAsync(BlockBoilerAdapterStep step, CancellationToken ct)
    {
        logger.LogDebug("WaitForResolutionAsync: создаём _externalSignal");
        var signal = _externalSignal = new TaskCompletionSource<PreExecutionResolution>();

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
            logger.LogDebug("WaitForResolutionAsync: получили completedTask");
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
        logger.LogDebug("WaitForFirstSignalAsync: вызываем errorCoordinator.WaitForResolutionAsync (enableSkip={EnableSkip})", enableSkip);
        var options = new ErrorCoordinator.WaitForResolutionOptions(
            BlockEndTag: blockEndTag,
            BlockErrorTag: blockErrorTag,
            EnableSkip: enableSkip);
        var resolutionTask = errorCoordinator.WaitForResolutionAsync(options, ct);
        var externalTask = signal.Task;

        return Task.WhenAny(resolutionTask, externalTask);
    }

    private async Task<PreExecutionResolution> ExtractResolutionAsync(
        Task completedTask,
        TaskCompletionSource<PreExecutionResolution> signal)
    {
        if (completedTask == signal.Task)
        {
            logger.LogDebug("ExtractResolutionAsync: сработал externalSignal");
            return await signal.Task;
        }
        var errorResolutionTask = (Task<ErrorResolution>)completedTask;
        var errorResolution = await errorResolutionTask;
        logger.LogDebug("ExtractResolutionAsync: errorCoordinator вернул {Resolution}", errorResolution);
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

    private async Task SetSelectedAsync(BlockBoilerAdapterStep step)
    {
        var selectedTag = PlcBlockTagHelper.GetSelectedTag(step);
        if (selectedTag == null) return;
        logger.LogDebug("Взведение Selected для {BlockPath}: {Tag}",
            (step as IHasPlcBlockPath).PlcBlockPath, selectedTag);
        var result = await infra.PlcService.WriteAsync(selectedTag, true);
        if (result.Error != null)
        {
            logger.LogWarning("Ошибка записи Selected: {Error}", result.Error);
        }
    }

    private static string? GetBlockEndTag(BlockBoilerAdapterStep step)
    {
        return PlcBlockTagHelper.GetEndTag(step);
    }

    private static string? GetBlockErrorTag(BlockBoilerAdapterStep step)
    {
        return PlcBlockTagHelper.GetErrorTag(step);
    }
}
