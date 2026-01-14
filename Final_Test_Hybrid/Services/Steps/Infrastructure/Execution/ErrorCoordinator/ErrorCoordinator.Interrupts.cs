using Final_Test_Hybrid.Models.Errors;
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
        if (!InterruptBehaviors.TryGetValue(reason, out var behavior))
        {
            _logger.LogError("Неизвестная причина прерывания: {Reason}", reason);
            return;
        }
        // Ранняя проверка: AutoModeDisabled при уже восстановленном автомате — пропуск
        if (reason == InterruptReason.AutoModeDisabled && _autoReady.IsReady)
        {
            _logger.LogInformation("AutoReady восстановлен до обработки прерывания — пропуск");
            return;
        }
        LogInterrupt(reason, behavior);
        RaiseErrorForInterrupt(reason);
        SetCurrentInterrupt(reason);
        NotifyInterrupt(behavior);
        await ExecuteInterruptActionAsync(behavior, ct);
    }

    private void SetCurrentInterrupt(InterruptReason reason)
    {
        CurrentInterrupt = reason;
        InvokeEventSafe(OnInterruptChanged, "OnInterruptChanged");
    }

    private void RaiseErrorForInterrupt(InterruptReason reason)
    {
        var error = reason switch
        {
            InterruptReason.PlcConnectionLost => ErrorDefinitions.OpcConnectionLost,
            InterruptReason.TagTimeout => ErrorDefinitions.TagReadTimeout,
            _ => null
        };
        if (error != null)
        {
            _errorService.Raise(error);
        }
    }

    private void LogInterrupt(InterruptReason reason, InterruptBehavior behavior)
    {
        _logger.LogWarning("Прерывание: {Reason} — {Message}", reason, behavior.Message);
    }

    private void NotifyInterrupt(InterruptBehavior behavior)
    {
        _notifications.ShowWarning(behavior.Message, GetInterruptDetails(behavior));
    }

    private static string GetInterruptDetails(InterruptBehavior behavior)
    {
        return behavior.Action switch
        {
            InterruptAction.PauseAndWait => "Ожидание восстановления...",
            InterruptAction.ResetAfterDelay when behavior.Delay.HasValue =>
                $"Сброс через {behavior.Delay.Value.TotalSeconds:0} сек",
            InterruptAction.ResetAfterDelay => "Сброс теста",
            _ => string.Empty
        };
    }

    private async Task ExecuteInterruptActionAsync(InterruptBehavior behavior, CancellationToken ct)
    {
        switch (behavior.Action)
        {
            case InterruptAction.PauseAndWait:
                _pauseToken.Pause();
                break;

            case InterruptAction.ResetAfterDelay:
                await DelayThenResetAsync(behavior.Delay, ct);
                break;

            case InterruptAction.ResetImmediately:
                Reset();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(behavior), behavior.Action, "Неизвестное действие");
        }
    }

    private async Task DelayThenResetAsync(TimeSpan? delay, CancellationToken ct)
    {
        if (delay.HasValue)
        {
            await Task.Delay(delay.Value, ct);
        }
        Reset();
    }

    #endregion
    #region Error Resolution

    public async Task<ErrorResolution> WaitForResolutionAsync(CancellationToken ct)
    {
        _logger.LogInformation("Ожидание решения оператора (таймаут {Timeout} сек)...",
            ResolutionTimeout.TotalSeconds);
        try
        {
            return await WaitForOperatorSignalAsync(ct);
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

    private async Task<ErrorResolution> WaitForOperatorSignalAsync(CancellationToken ct)
    {
        var waitResult = await _tagWaiter.WaitAnyAsync(
            _tagWaiter.CreateWaitGroup<ErrorResolution>()
                .WaitForTrue(BaseTags.ErrorRetry, () => ErrorResolution.Retry, "Retry")
                .WaitForTrue(BaseTags.ErrorSkip, () => ErrorResolution.Skip, "Skip")
                .WithTimeout(ResolutionTimeout),
            ct);

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

    public async Task SendAskRepeatAsync(CancellationToken ct)
    {
        _logger.LogInformation("Отправка AskRepeat в PLC");
        var result = await _plcService.WriteAsync(BaseTags.AskRepeat, true, ct);

        if (result.Error != null)
        {
            _logger.LogError("Ошибка записи AskRepeat: {Error}", result.Error);
        }
    }

    #endregion
}
