namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;

public sealed partial class ErrorCoordinator
{
    #region Interrupt Handling

    public async Task HandleInterruptAsync(InterruptReason reason, CancellationToken ct = default)
    {
        if (_disposed) { return; }
        if (!await TryAcquireLockAsync(ct)) { return; }
        try
        {
            await ProcessInterruptAsync(reason, ct);
        }
        finally
        {
            ReleaseLockSafe();
        }
    }

    private async Task<bool> TryAcquireLockAsync(CancellationToken ct)
    {
        try
        {
            await _interruptLock.WaitAsync(ct);
            if (_disposed)
            {
                _interruptLock.Release();
                return false;
            }
            return true;
        }
        catch (Exception) when (ct.IsCancellationRequested || _disposed)
        {
            return false;
        }
    }

    private void ReleaseLockSafe()
    {
        try
        {
            if (!_disposed)
            {
                _interruptLock.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            // Expected during shutdown
        }
    }

    private async Task ProcessInterruptAsync(InterruptReason reason, CancellationToken ct)
    {
        var behavior = _behaviorRegistry.Get(reason);
        if (behavior == null)
        {
            _logger.LogError("Неизвестная причина прерывания: {Reason}", reason);
            return;
        }
        if (reason == InterruptReason.AutoModeDisabled && _subscriptions.AutoReady.IsReady)
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
}
