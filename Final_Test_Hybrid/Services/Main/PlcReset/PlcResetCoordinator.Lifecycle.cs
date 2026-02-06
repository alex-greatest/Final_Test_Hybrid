using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Main.PlcReset;

public sealed partial class PlcResetCoordinator
{
    #region Synchronization

    private bool TryAcquireResetFlag() => Interlocked.CompareExchange(ref _isHandlingReset, 1, 0) == 0;

    private void ReleaseResetFlag() => Interlocked.Exchange(ref _isHandlingReset, 0);

    private void Cleanup()
    {
        try
        {
            IsActive = false;
            NotifyActiveChangedSafely();
        }
        finally
        {
            ReleaseResetFlag();
            DisposeCurrentResetCts();
        }
    }

    private void DisposeCurrentResetCts()
    {
        var cts = Interlocked.Exchange(ref _currentResetCts, null);
        try
        {
            cts?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка освобождения _currentResetCts");
        }
    }

    private void NotifyActiveChangedSafely()
    {
        try
        {
            OnActiveChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике OnActiveChanged");
        }
    }

    #endregion

    #region Public API

    public void CancelCurrentReset()
    {
        var cts = _currentResetCts;
        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    #endregion

    #region Disposal

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        CancelDisposeCts();
        UnsubscribeEvents();
        await WaitForCurrentOperationAsync();
        DisposeResources();
    }

    private void CancelDisposeCts()
    {
        try
        {
            _disposeCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void UnsubscribeEvents()
    {
        _resetSubscription.OnStateChanged -= HandleResetSignal;
        OnForceStop = null;
        OnResetStarting = null;
        OnAskEndReceived = null;
        OnResetCompleted = null;
    }

    private async Task WaitForCurrentOperationAsync()
    {
        var spinWait = new SpinWait();
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (_isHandlingReset == 1 && DateTime.UtcNow < timeout)
        {
            spinWait.SpinOnce();
            await Task.Yield();
        }
    }

    private void DisposeResources()
    {
        _currentResetCts?.Dispose();
        _disposeCts.Dispose();
    }

    #endregion
}
