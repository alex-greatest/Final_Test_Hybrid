using System.Threading.Channels;
using Final_Test_Hybrid.Services.Common.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Диспетчер команд Modbus с приоритетной очередью.
/// Единственный владелец физического соединения.
/// </summary>
public class ModbusDispatcher : IModbusDispatcher
{
    private readonly ModbusConnectionManager _connectionManager;
    private readonly ModbusDispatcherOptions _options;
    private readonly DualLogger<ModbusDispatcher> _logger;
    private readonly object _stateLock = new();

    private readonly Channel<IModbusCommand> _highQueue;
    private readonly Channel<IModbusCommand> _lowQueue;

    private CancellationTokenSource? _cts;
    private Task? _workerTask;
    private bool _isStopping;
    private bool _wasStopped;
    private volatile bool _isConnected;
    private volatile bool _isReconnecting;
    private volatile bool _disposed;

    /// <summary>
    /// Создаёт диспетчер команд.
    /// </summary>
    public ModbusDispatcher(
        ModbusConnectionManager connectionManager,
        IOptions<ModbusDispatcherOptions> options,
        ILogger<ModbusDispatcher> logger,
        ITestStepLogger testStepLogger)
    {
        _connectionManager = connectionManager;
        _options = options.Value;
        _logger = new DualLogger<ModbusDispatcher>(logger, testStepLogger);

        _highQueue = Channel.CreateBounded<IModbusCommand>(
            new BoundedChannelOptions(_options.HighPriorityQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        _lowQueue = Channel.CreateBounded<IModbusCommand>(
            new BoundedChannelOptions(_options.LowPriorityQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
    }

    /// <inheritdoc />
    public event Func<Task>? Disconnecting;

    /// <inheritdoc />
    public event Action? Connected;

    /// <inheritdoc />
    public bool IsConnected => _isConnected;

    /// <inheritdoc />
    public bool IsReconnecting => _isReconnecting;

    /// <inheritdoc />
    public bool IsStarted
    {
        get
        {
            lock (_stateLock)
            {
                return _workerTask != null;
            }
        }
    }

    #region Public API

    /// <inheritdoc />
    public async ValueTask EnqueueAsync(IModbusCommand command, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var channel = command.Priority == CommandPriority.High ? _highQueue : _lowQueue;

        try
        {
            await channel.Writer.WriteAsync(command, ct).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            throw new InvalidOperationException("Диспетчер остановлен, новые команды не принимаются");
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        lock (_stateLock)
        {
            // Рестарт после остановки не поддерживается (каналы завершены)
            if (_wasStopped)
            {
                throw new InvalidOperationException("Рестарт диспетчера после остановки не поддерживается");
            }

            // Не запускаем если уже работает или останавливается
            if (_workerTask != null || _isStopping)
            {
                return Task.CompletedTask;
            }

            _cts = new CancellationTokenSource();
            _workerTask = RunWorkerLoopAsync(_cts.Token);
        }

        _logger.LogInformation("ModbusDispatcher запущен");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        CancellationTokenSource? ctsToCancel;
        Task? taskToWait;

        lock (_stateLock)
        {
            // Уже останавливаемся или не запущен
            if (_isStopping || _workerTask == null || _cts == null)
            {
                return;
            }

            _isStopping = true;
            ctsToCancel = _cts;
            taskToWait = _workerTask;
        }

        _logger.LogInformation("Остановка ModbusDispatcher...");

        // Завершаем каналы чтобы новые EnqueueAsync не зависли
        _highQueue.Writer.TryComplete();
        _lowQueue.Writer.TryComplete();

        await ctsToCancel.CancelAsync().ConfigureAwait(false);

        try
        {
            await taskToWait.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker завершился с ошибкой: {Error}", ex.Message);
        }

        // Cleanup всегда выполняется
        CancelPendingCommands();
        _connectionManager.Close();
        _isConnected = false;
        ctsToCancel.Dispose();

        lock (_stateLock)
        {
            _cts = null;
            _workerTask = null;
            _isStopping = false;
            _wasStopped = true;
        }

        _logger.LogInformation("ModbusDispatcher остановлен");
    }

    #endregion

    #region Worker Loop

    private async Task RunWorkerLoopAsync(CancellationToken ct)
    {
        // Запоминаем текущую задачу для корректной очистки
        var thisTask = _workerTask;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await EnsureConnectedAsync(ct).ConfigureAwait(false);
                    await ProcessCommandsLoopAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в воркере диспетчера: {Error}", ex.Message);
                    await HandleConnectionErrorAsync(ct).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            // Если воркер завершился не через StopAsync, очищаем состояние сами
            CleanupWorkerStateIfNeeded(thisTask);
        }
    }

    /// <summary>
    /// Очищает состояние воркера если он завершился не через StopAsync.
    /// </summary>
    private void CleanupWorkerStateIfNeeded(Task? thisTask)
    {
        CancellationTokenSource? ctsToDispose = null;
        var shouldCleanup = false;

        lock (_stateLock)
        {
            // Если StopAsync не вызывался (не isStopping), а воркер завершился сам
            if (!_isStopping && _workerTask == thisTask)
            {
                _workerTask = null;
                ctsToDispose = _cts;
                _cts = null;
                _wasStopped = true;
                shouldCleanup = true;

                _logger.LogWarning("Воркер диспетчера завершился неожиданно");
            }
        }

        if (shouldCleanup)
        {
            // Полная очистка как в StopAsync
            _highQueue.Writer.TryComplete();
            _lowQueue.Writer.TryComplete();
            CancelPendingCommands();
            _connectionManager.Close();
            _isConnected = false;
            ctsToDispose?.Dispose();
        }
    }

    private async Task ProcessCommandsLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _isConnected)
        {
            var command = await WaitForCommandAsync(ct).ConfigureAwait(false);

            if (command == null)
            {
                continue;
            }

            try
            {
                await ExecuteCommandAsync(command, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsCommunicationError(ex))
            {
                command.SetException(ex);
                throw;
            }
            catch (Exception ex)
            {
                command.SetException(ex);
                _logger.LogError(ex, "Ошибка выполнения команды: {Error}", ex.Message);
            }
        }
    }

    private async Task<IModbusCommand?> WaitForCommandAsync(CancellationToken ct)
    {
        // Сначала обрабатываем все высокоприоритетные команды
        while (_highQueue.Reader.TryRead(out var highCmd))
        {
            if (!highCmd.CancellationToken.IsCancellationRequested)
            {
                return highCmd;
            }
            highCmd.SetCanceled();
        }

        // Затем одну низкоприоритетную
        if (_lowQueue.Reader.TryRead(out var lowCmd))
        {
            if (!lowCmd.CancellationToken.IsCancellationRequested)
            {
                return lowCmd;
            }
            lowCmd.SetCanceled();
        }

        // Ждём новую команду с таймаутом
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.CommandWaitTimeoutMs);

        try
        {
            // Ждём любую команду из обоих каналов
            var highTask = _highQueue.Reader.WaitToReadAsync(timeoutCts.Token).AsTask();
            var lowTask = _lowQueue.Reader.WaitToReadAsync(timeoutCts.Token).AsTask();

            await Task.WhenAny(highTask, lowTask).ConfigureAwait(false);
            return null; // Вернёмся в цикл и прочитаем команду
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null; // Таймаут - проверим снова
        }
    }

    private async Task ExecuteCommandAsync(IModbusCommand command, CancellationToken ct)
    {
        var master = _connectionManager.ModbusMaster
            ?? throw new InvalidOperationException("Нет подключения к устройству");

        var slaveId = _connectionManager.SlaveId;

        await Task.Run(() => command.ExecuteAsync(master, slaveId, ct), ct).ConfigureAwait(false);
    }

    #endregion

    #region Connection Management

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_isConnected && _connectionManager.IsConnected)
        {
            return;
        }

        _isReconnecting = true;
        var delay = _options.InitialReconnectDelayMs;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _connectionManager.Connect();
                _isConnected = true;
                _isReconnecting = false;
                Connected?.Invoke();
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Не удалось подключиться: {Error}. Повтор через {Delay} мс",
                    ex.Message, delay);

                await Task.Delay(delay, ct).ConfigureAwait(false);
                delay = Math.Min(delay * (int)_options.ReconnectBackoffMultiplier, _options.MaxReconnectDelayMs);
            }
        }
    }

    private async Task HandleConnectionErrorAsync(CancellationToken ct)
    {
        _isConnected = false;

        await NotifyDisconnectingAsync().ConfigureAwait(false);
        _connectionManager.Close();

        await Task.Delay(_options.InitialReconnectDelayMs, ct).ConfigureAwait(false);
    }

    private async Task NotifyDisconnectingAsync()
    {
        var handler = Disconnecting;
        if (handler == null)
        {
            return;
        }

        try
        {
            await handler.Invoke().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике Disconnecting: {Error}", ex.Message);
        }
    }

    #endregion

    #region Error Handling

    private static bool IsCommunicationError(Exception ex)
    {
        return ex is TimeoutException
            || ex is System.IO.IOException
            || ex.Message.Contains("port", StringComparison.OrdinalIgnoreCase);
    }

    private void CancelPendingCommands()
    {
        while (_highQueue.Reader.TryRead(out var cmd))
        {
            cmd.SetCanceled();
        }

        while (_lowQueue.Reader.TryRead(out var cmd))
        {
            cmd.SetCanceled();
        }
    }

    #endregion

    #region Dispose

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        await StopAsync().ConfigureAwait(false);
        _connectionManager.Dispose();
    }

    #endregion
}
