using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Models.Steps;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;

/// <summary>
/// ErrorCoordinator partial: Interrupt handling logic.
/// </summary>
public partial class ErrorCoordinator
{
    #region Synchronization Primitives

    private bool TryAcquireInterruptFlag(InterruptReason reason)
    {
        var acquired = Interlocked.CompareExchange(ref _isHandlingInterrupt, 1, 0) == 0;

        if (!acquired)
        {
            _logger.LogWarning("Прерывание {Reason} проигнорировано — уже обрабатывается", reason);
        }

        return acquired;
    }

    private void ReleaseInterruptFlag()
    {
        Interlocked.Exchange(ref _isHandlingInterrupt, 0);
    }

    private void IncrementActiveOperations() => Interlocked.Increment(ref _activeOperations);
    private void DecrementActiveOperations() => Interlocked.Decrement(ref _activeOperations);

    private async Task<bool> TryAcquireLockAsync(CancellationToken ct)
    {
        if (_disposed) { return false; }
        return await TryAcquireLockCoreAsync(ct);
    }

    private async Task<bool> TryAcquireLockCoreAsync(CancellationToken ct)
    {
        try { return await AcquireAndValidateAsync(ct); }
        catch (Exception) when (ct.IsCancellationRequested || _disposed) { return false; }
    }

    private async Task<bool> AcquireAndValidateAsync(CancellationToken ct)
    {
        await _operationLock.WaitAsync(ct);
        return ValidateOrRelease();
    }

    private bool ValidateOrRelease()
    {
        if (!_disposed)
        {
            return true;
        }
        _operationLock.Release(); 
        return false;
    }

    private void ReleaseLockSafe()
    {
        try
        {
            if (!_disposed)
            {
                _operationLock.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            // Expected during shutdown
        }
    }

    #endregion

    #region Interrupt Handling

    public async Task HandleInterruptAsync(InterruptReason reason, CancellationToken ct = default)
    {
        if (!await TryAcquireResourcesAsync(reason, ct))
        {
            return; 
        }
        try
        {
            await ProcessInterruptAsync(reason, ct);
        }
        finally
        {
            ReleaseResources();
        }
    }

    private async Task<bool> TryAcquireResourcesAsync(InterruptReason reason, CancellationToken ct)
    {
        if (_disposed) { return false; }
        if (!TryAcquireInterruptFlag(reason)) { return false; }

        IncrementActiveOperations();

        if (await TryAcquireLockAsync(ct)) { return true; }

        DecrementActiveOperations();
        ReleaseInterruptFlag();
        return false;
    }

    private void ReleaseResources()
    {
        ReleaseLockSafe();
        DecrementActiveOperations();
        ReleaseInterruptFlag();
    }

    private async Task ProcessInterruptAsync(InterruptReason reason, CancellationToken ct)
    {
        var behavior = _behaviorRegistry.Get(reason);
        if (behavior == null)
        {
            _logger.LogError("Неизвестная причина прерывания: {Reason}", reason);
            return;
        }

        // Ранняя проверка: AutoModeDisabled при уже восстановленном автомате — пропуск
        var isAutoReady = _subscriptions.AutoReady.IsReady;
        if (reason == InterruptReason.AutoModeDisabled && isAutoReady)
        {
            _logger.LogInformation("AutoReady восстановлен до обработки прерывания — пропуск");
            return;
        }

        _logger.LogWarning("Прерывание: {Reason} — {Message}", reason, behavior.Message);

        if (behavior.AssociatedError != null)
        {
            _resolution.ErrorService.Raise(behavior.AssociatedError);
        }

        SetCurrentInterrupt(reason);
        await behavior.ExecuteAsync(this, ct);
    }

    private void SetCurrentInterrupt(InterruptReason reason)
    {
        CurrentInterrupt = reason;
        InvokeEventSafe(OnInterruptChanged, "OnInterruptChanged");
    }

    #endregion
    #region Error Resolution

    public Task<ErrorResolution> WaitForResolutionAsync(CancellationToken ct)
        => WaitForResolutionAsync(null, ct);

    public async Task<ErrorResolution> WaitForResolutionAsync(string? blockErrorTag, CancellationToken ct, TimeSpan? timeout = null)
    {
        var timeoutMsg = timeout.HasValue ? $"таймаут {timeout.Value.TotalSeconds} сек" : "без таймаута";
        _logger.LogInformation("Ожидание решения оператора ({Timeout})...", timeoutMsg);
        try
        {
            return await WaitForOperatorSignalAsync(blockErrorTag, timeout, ct);
        }
        catch (Exception ex)
        {
            return HandleResolutionException(ex);
        }
    }

    private ErrorResolution HandleResolutionException(Exception ex)
    {
        return ex switch
        {
            TimeoutException => HandleResolutionTimeout(),
            OperationCanceledException => LogAndRethrow(ex),
            _ => Rethrow(ex)
        };
    }

    private ErrorResolution LogAndRethrow(Exception ex)
    {
        _logger.LogInformation("Ожидание решения отменено");
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
        return default;
    }

    private static ErrorResolution Rethrow(Exception ex)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
        return default;
    }

    private async Task<ErrorResolution> WaitForOperatorSignalAsync(string? blockErrorTag, TimeSpan? timeout, CancellationToken ct)
    {
        var builder = _resolution.TagWaiter.CreateWaitGroup<ErrorResolution>()
            .WaitForTrue(BaseTags.ErrorRetry, () => ErrorResolution.Retry, "Retry");

        if (blockErrorTag != null)
        {
            builder.WaitForAllTrue(
                [BaseTags.ErrorSkip, blockErrorTag],
                () => ErrorResolution.Skip,
                "Skip");
        }
        else
        {
            builder.WaitForTrue(BaseTags.ErrorSkip, () => ErrorResolution.Skip, "Skip");
        }

        if (timeout.HasValue)
        {
            builder.WithTimeout(timeout.Value);
        }

        var waitResult = await _resolution.TagWaiter.WaitAnyAsync(builder, ct);

        var resolution = waitResult.Result;
        _logger.LogInformation("Получен сигнал: {Resolution}", resolution);
        return resolution;
    }

    private ErrorResolution HandleResolutionTimeout()
    {
        _logger.LogWarning("Таймаут ожидания ответа оператора ({Timeout} сек)",
            ResolutionTimeout.TotalSeconds);
        return ErrorResolution.Timeout;
    }

    public Task SendAskRepeatAsync(CancellationToken ct) => SendAskRepeatAsync(null, ct);

    public async Task SendAskRepeatAsync(string? blockErrorTag, CancellationToken ct)
    {
        _logger.LogInformation("Отправка AskRepeat в PLC");
        var result = await _resolution.PlcService.WriteAsync(BaseTags.AskRepeat, true, ct);

        if (result.Error != null)
        {
            _logger.LogError("Ошибка записи AskRepeat: {Error}", result.Error);
            return;
        }
        await WaitForPlcAcknowledgeAsync(blockErrorTag, ct);
    }

    private async Task WaitForPlcAcknowledgeAsync(string? blockErrorTag, CancellationToken ct)
    {
        if (blockErrorTag == null)
        {
            return;
        }
        _logger.LogDebug("Ожидание сброса Error блока: {Tag}", blockErrorTag);
        await _resolution.TagWaiter.WaitForFalseAsync(blockErrorTag, timeout: null, ct);
        _logger.LogDebug("Error блока сброшен");
    }

    #endregion
}
