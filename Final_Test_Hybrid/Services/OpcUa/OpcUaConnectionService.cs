using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public sealed partial class OpcUaConnectionService : IOpcUaConnectionService
{
    public bool IsConnected => _session?.Connected == true;
    public event Action<bool>? ConnectionChanged;
    public event Action? SessionRecreated;
    private readonly OpcUaSettings _settings;
    private readonly ILogger<OpcUaConnectionService> _logger;
    private readonly IOpcUaSessionFactory _sessionFactory;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private ISession? _session;
    private SessionReconnectHandler? _reconnectHandler;
    private int _isReconnecting;
    private PeriodicTimer? _connectTimer;
    private Task? _connectLoopTask;
    private int _disposeState;
    private bool IsDisposed => Volatile.Read(ref _disposeState) != 0;

    public OpcUaConnectionService(
        IOptions<OpcUaSettings> settings,
        ILogger<OpcUaConnectionService> logger,
        IOpcUaSessionFactory sessionFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _sessionFactory = sessionFactory;
        OpcUaSettingsValidator.Validate(_settings, _logger);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (_connectLoopTask is not null)
            {
                throw new InvalidOperationException("Service is already started");
            }
            _connectTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.ReconnectIntervalMs));
            try
            {
                _connectLoopTask = ConnectLoopAsync(_disposeCts.Token);
            }
            catch
            {
                _connectTimer.Dispose();
                _connectTimer = null;
                throw;
            }
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public Task StopAsync()
    {
        return DisposeAsync().AsTask();
    }

    public async Task ExecuteWithSessionAsync(Func<ISession, Task> action, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            EnsureConnected();
            await action(_session!).ConfigureAwait(false);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async Task<T> ExecuteWithSessionAsync<T>(Func<ISession, Task<T>> action, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            EnsureConnected();
            return await action(_session!).ConfigureAwait(false);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }
        await DisposeAsyncCore().ConfigureAwait(false);
    }

    private async Task DisposeAsyncCore()
    {
        await _disposeCts.CancelAsync().ConfigureAwait(false);
        _connectTimer?.Dispose();
        await WaitForConnectLoopAsync().ConfigureAwait(false);
        await DisposeSessionWithLockAsync().ConfigureAwait(false);
    }

    private async Task DisposeSessionWithLockAsync()
    {
        await _sessionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            DisposeReconnectHandler();
            await CloseSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            _sessionLock.Release();
            _sessionLock.Dispose();
            _disposeCts.Dispose();
        }
    }

    private void EnsureConnected()
    {
        if (_session is null || !_session.Connected)
        {
            throw new InvalidOperationException("OPC UA session is not connected");
        }
    }

    private async Task ConnectLoopAsync(CancellationToken cancellationToken)
    {
        await TryConnectAsync().ConfigureAwait(false);
        await RunPeriodicReconnectAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RunPeriodicReconnectAsync(CancellationToken cancellationToken)
    {
        if (_connectTimer is null)
        {
            return;
        }
        await ExecutePeriodicReconnectLoopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecutePeriodicReconnectLoopAsync(CancellationToken cancellationToken)
    {
        while (await WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await TryConnectIfNotConnectedAsync().ConfigureAwait(false);
        }
    }

    private async Task<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _connectTimer!.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task TryConnectIfNotConnectedAsync()
    {
        if (Volatile.Read(ref _isReconnecting) != 0)
        {
            return;
        }
        await TryConnectAsync().ConfigureAwait(false);
    }

    private async Task TryConnectAsync()
    {
        await _sessionLock.WaitAsync(_disposeCts.Token).ConfigureAwait(false);
        try
        {
            await TryConnectCoreAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to OPC UA server at {Endpoint}", _settings.EndpointUrl);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task TryConnectCoreAsync()
    {
        if (IsConnected)
        {
            return;
        }
        _session = await _sessionFactory.CreateAsync(_settings, _disposeCts.Token).ConfigureAwait(false);
        _session.KeepAlive += OnKeepAlive;
        _logger.LogInformation("Connected to OPC UA server at {Endpoint}", _settings.EndpointUrl);
        RaiseConnectionChangedAsync(true);
    }

    private async Task WaitForConnectLoopAsync()
    {
        if (_connectLoopTask is null)
        {
            return;
        }
        await WaitForTaskSafeAsync(_connectLoopTask).ConfigureAwait(false);
    }

    private static async Task WaitForTaskSafeAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CloseSessionAsync()
    {
        if (_session is null)
        {
            return;
        }
        await CloseSessionCoreAsync().ConfigureAwait(false);
    }

    private async Task CloseSessionCoreAsync()
    {
        _session!.KeepAlive -= OnKeepAlive;
        await TryCloseSessionAsync().ConfigureAwait(false);
        _session.Dispose();
        _session = null;
    }

    private async Task TryCloseSessionAsync()
    {
        try
        {
            await _session!.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing OPC UA session");
        }
    }
}
