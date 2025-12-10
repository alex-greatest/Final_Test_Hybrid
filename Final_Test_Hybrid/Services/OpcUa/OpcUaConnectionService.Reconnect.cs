using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public sealed partial class OpcUaConnectionService
{
    private void OnKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (ServiceResult.IsGood(e.Status))
        {
            return;
        }
        HandleKeepAliveError(session);
    }

    private void HandleKeepAliveError(ISession session)
    {
        _logger.LogWarning("Connection lost to OPC UA server");
        RaiseConnectionChangedAsync(false);
        _ = StartReconnectHandlerAsync(session).ContinueWith(
            t => _logger.LogError(t.Exception, "Error starting reconnect handler"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private async Task StartReconnectHandlerAsync(ISession session)
    {
        if (!await TryAcquireSessionLockAsync().ConfigureAwait(false))
        {
            return;
        }
        ExecuteReconnectWithRelease(session);
    }

    private async Task<bool> TryAcquireSessionLockAsync()
    {
        try
        {
            await _sessionLock.WaitAsync(_disposeCts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void ExecuteReconnectWithRelease(ISession session)
    {
        try
        {
            StartReconnectHandlerCore(session);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private void StartReconnectHandlerCore(ISession session)
    {
        if (Interlocked.CompareExchange(ref _isReconnecting, 1, 0) != 0)
        {
            return;
        }
        if (IsDisposed)
        {
            Interlocked.Exchange(ref _isReconnecting, 0);
            return;
        }
        _reconnectHandler = new SessionReconnectHandler();
        _reconnectHandler.BeginReconnect(session, _settings.ReconnectIntervalMs, OnReconnectComplete);
    }

    private void OnReconnectComplete(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }
        _ = HandleReconnectCompleteAsync().ContinueWith(
            t => _logger.LogError(t.Exception, "Unhandled exception in reconnect handler"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private async Task HandleReconnectCompleteAsync()
    {
        await _sessionLock.WaitAsync(_disposeCts.Token).ConfigureAwait(false);
        try
        {
            UpdateSessionIfReconnected();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling reconnect completion");
        }
        finally
        {
            Interlocked.Exchange(ref _isReconnecting, 0);
            _sessionLock.Release();
        }
    }

    private void UpdateSessionIfReconnected()
    {
        var reconnectedSession = _reconnectHandler?.Session;
        if (reconnectedSession is null)
        {
            return;
        }
        ProcessReconnectedSession(reconnectedSession);
    }

    private void ProcessReconnectedSession(ISession reconnectedSession)
    {
        HandleSessionRecreateIfNeeded(reconnectedSession);
        DisposeReconnectHandler();
        _logger.LogInformation("Reconnected to OPC UA server");
        RaiseConnectionChangedAsync(true);
    }

    private void HandleSessionRecreateIfNeeded(ISession reconnectedSession)
    {
        if (!IsSessionRecreated(reconnectedSession))
        {
            return;
        }
        HandleSessionRecreate(reconnectedSession);
        RaiseSessionRecreatedAsync();
    }

    private bool IsSessionRecreated(ISession newSession)
    {
        return !ReferenceEquals(newSession, _session);
    }

    private void HandleSessionRecreate(ISession newSession)
    {
        _logger.LogWarning("Session recreated, old subscriptions lost");
        DisposeOldSession();
        _session = newSession;
        _session.KeepAlive += OnKeepAlive;
    }

    private void DisposeOldSession()
    {
        var oldSession = _session;
        if (oldSession is null)
        {
            return;
        }
        oldSession.KeepAlive -= OnKeepAlive;
        oldSession.Dispose();
    }

    private void DisposeReconnectHandler()
    {
        _reconnectHandler?.Dispose();
        _reconnectHandler = null;
    }

    private void RaiseConnectionChangedAsync(bool isConnected)
    {
        var handler = ConnectionChanged;
        if (handler is null)
        {
            return;
        }
        Task.Run(() => InvokeConnectionChangedSafe(handler, isConnected));
    }

    private void InvokeConnectionChangedSafe(Action<bool> handler, bool isConnected)
    {
        try
        {
            handler.Invoke(isConnected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ConnectionChanged handler");
        }
    }

    private void RaiseSessionRecreatedAsync()
    {
        var handler = SessionRecreated;
        if (handler is null)
        {
            return;
        }
        Task.Run(() => InvokeSessionRecreatedSafe(handler));
    }

    private void InvokeSessionRecreatedSafe(Action handler)
    {
        try
        {
            handler.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SessionRecreated handler");
        }
    }
}
