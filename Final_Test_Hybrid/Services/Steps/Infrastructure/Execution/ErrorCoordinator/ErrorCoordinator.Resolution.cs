using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;

public sealed partial class ErrorCoordinator
{
    #region Error Resolution

    public Task<ErrorResolution> WaitForResolutionAsync(
        WaitForResolutionOptions? options = null,
        CancellationToken ct = default)
    {
        var opts = options ?? new WaitForResolutionOptions();
        return WaitForResolutionCoreAsync(opts, ct);
    }

    private async Task<ErrorResolution> WaitForResolutionCoreAsync(WaitForResolutionOptions options, CancellationToken ct)
    {
        var timeoutMsg = options.Timeout.HasValue
            ? $"таймаут {options.Timeout.Value.TotalSeconds} сек"
            : "без таймаута";
        _logger.LogInformation("Ожидание решения оператора ({Timeout})...", timeoutMsg);

        try
        {
            return await WaitForOperatorSignalAsync(options, ct);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Таймаут ожидания ответа оператора");
            return ErrorResolution.Timeout;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation("Ожидание решения отменено");
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            return default;
        }
    }

    private async Task<ErrorResolution> WaitForOperatorSignalAsync(WaitForResolutionOptions options, CancellationToken ct)
    {
        var builder = _resolution.TagWaiter.CreateWaitGroup<ErrorResolution>()
            .WaitForTrue(BaseTags.ErrorRetry, () => ErrorResolution.Retry, "Retry");

        if (options.EnableSkip)
        {
            if (options.BlockEndTag != null && options.BlockErrorTag != null)
            {
                builder.WaitForAllTrue(
                    [options.BlockEndTag, options.BlockErrorTag],
                    () => ErrorResolution.Skip,
                    "Skip");
            }
            else
            {
                builder.WaitForTrue(BaseTags.TestEndStep, () => ErrorResolution.Skip, "Skip");
            }
        }

        if (options.Timeout.HasValue)
        {
            builder.WithTimeout(options.Timeout.Value);
        }

        var waitResult = await _resolution.TagWaiter.WaitAnyAsync(builder, ct);
        var resolution = waitResult.Result;
        _logger.LogInformation("Получен сигнал: {Resolution}", resolution);
        return resolution;
    }

    public Task SendAskRepeatAsync(CancellationToken ct) => SendAskRepeatAsync(null, ct);

    /// <summary>
    /// Отправляет сигнал AskRepeat в PLC и ожидает сброса Block.Error.
    /// </summary>
    /// <param name="blockErrorTag">Тег Block.Error для ожидания сброса (null для шагов без блока).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <exception cref="TimeoutException">Block.Error не сброшен за 60 секунд.</exception>
    public async Task SendAskRepeatAsync(string? blockErrorTag, CancellationToken ct)
    {
        _logger.LogInformation("Отправка AskRepeat в PLC");
        var result = await _resolution.PlcService.WriteAsync(BaseTags.AskRepeat, true, ct);

        if (result.Error != null)
        {
            _logger.LogError("Ошибка записи AskRepeat: {Error}", result.Error);
            return;
        }

        if (blockErrorTag != null)
        {
            _logger.LogDebug("Ожидание сброса Error блока: {Tag}", blockErrorTag);
            await _resolution.TagWaiter.WaitForFalseAsync(blockErrorTag, timeout: TimeSpan.FromSeconds(60), ct);
            _logger.LogDebug("Error блока сброшен");
        }
    }

    /// <summary>
    /// Ожидает сброса сигнала Req_Repeat.
    /// </summary>
    /// <param name="ct">Токен отмены.</param>
    /// <exception cref="TimeoutException">Req_Repeat не сброшен за 60 секунд.</exception>
    public async Task WaitForRetrySignalResetAsync(CancellationToken ct)
    {
        _logger.LogDebug("Ожидание сброса Req_Repeat...");
        await _resolution.TagWaiter.WaitForFalseAsync(BaseTags.ErrorRetry, timeout: TimeSpan.FromSeconds(60), ct);
        _logger.LogDebug("Req_Repeat сброшен");
    }

    #endregion

    #region Reset and Recovery

    public void Reset()
    {
        _logger.LogInformation("=== ПОЛНЫЙ СБРОС ===");
        _pauseToken.Resume();
        ClearCurrentInterrupt();
        InvokeEventSafe(OnReset, "OnReset");
    }

    public void ForceStop()
    {
        _logger.LogInformation("=== МЯГКИЙ СБРОС (снятие прерывания) ===");
        _pauseToken.Resume();
        ClearCurrentInterrupt();
    }

    private async Task TryResumeFromPauseAsync(CancellationToken ct)
    {
        if (_disposed) { return; }
        if (!await TryAcquireLockAsync(ct)) { return; }

        try
        {
            if (_pauseToken.IsPaused)
            {
                _pauseToken.Resume();
                ClearConnectionErrors();
                InvokeEventSafe(OnRecovered, "OnRecovered");
            }
        }
        finally
        {
            ReleaseLockSafe();
        }
    }

    private void ClearConnectionErrors()
    {
        _resolution.ErrorService.Clear(ErrorDefinitions.OpcConnectionLost.Code);
        _resolution.ErrorService.Clear(ErrorDefinitions.TagReadTimeout.Code);
        ClearCurrentInterrupt();
    }

    private void ClearCurrentInterrupt()
    {
        CurrentInterrupt = null;
        InvokeEventSafe(OnInterruptChanged, "OnInterruptChanged");
    }

    #endregion
}
