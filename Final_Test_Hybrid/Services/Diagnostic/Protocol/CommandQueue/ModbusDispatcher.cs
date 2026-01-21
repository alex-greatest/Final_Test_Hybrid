using System.Threading.Channels;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Диспетчер команд Modbus с приоритетной очередью.
/// Единственный владелец физического соединения.
/// Поддерживает рестарт после StopAsync().
/// </summary>
public class ModbusDispatcher : IModbusDispatcher
{
    private readonly ModbusConnectionManager _connectionManager;
    private readonly ModbusDispatcherOptions _options;
    private readonly DualLogger<ModbusDispatcher> _logger;
    private readonly object _stateLock = new();

    private Channel<IModbusCommand>? _highQueue;
    private Channel<IModbusCommand>? _lowQueue;

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _pingCts;
    private Task? _workerTask;
    private Task? _pingTask;
    private bool _isStopping;
    private bool _isStopped;
    private volatile bool _isConnected;
    private volatile bool _isReconnecting;
    private volatile bool _isPortOpen;
    private volatile bool _disposed;
    private int _isNotifyingDisconnect; // 0 = не выполняется, 1 = выполняется (для Interlocked)
    private volatile DiagnosticPingData? _lastPingData;

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

        RecreateChannels();
    }

    /// <inheritdoc />
    public event Func<Task>? Disconnecting;

    /// <inheritdoc />
    public event Action? Connected;

    /// <inheritdoc />
    public event Action<DiagnosticPingData>? PingDataUpdated;

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
                return _workerTask != null && !_isStopped;
            }
        }
    }

    /// <inheritdoc />
    public DiagnosticPingData? LastPingData => _lastPingData;

    #region Public API

    /// <inheritdoc />
    public async ValueTask EnqueueAsync(IModbusCommand command, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        Channel<IModbusCommand>? channel;

        lock (_stateLock)
        {
            if (_isStopped || _isStopping)
            {
                throw new InvalidOperationException("Диспетчер остановлен, новые команды не принимаются");
            }

            channel = command.Priority == CommandPriority.High ? _highQueue : _lowQueue;
        }

        if (channel == null)
        {
            throw new InvalidOperationException("Диспетчер не инициализирован");
        }

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
            // Если останавливались — пересоздаём каналы
            if (_isStopped)
            {
                RecreateChannels();
                _isStopped = false;
            }

            // Не запускаем если уже работает или останавливается
            if (_workerTask != null || _isStopping)
            {
                return Task.CompletedTask;
            }

            _cts = new CancellationTokenSource();
            _workerTask = RunWorkerLoopAsync(_cts.Token);

            // Запускаем ping task
            _pingCts = new CancellationTokenSource();
            _pingTask = RunPingLoopAsync(_pingCts.Token);
        }

        _logger.LogInformation("ModbusDispatcher запущен");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        CancellationTokenSource? ctsToCancel;
        CancellationTokenSource? pingCtsToCancel;
        Task? taskToWait;
        Task? pingTaskToWait;

        lock (_stateLock)
        {
            // Уже останавливаемся, уже остановлен, или не запущен
            if (_isStopping || _isStopped || _workerTask == null || _cts == null)
            {
                return;
            }

            _isStopping = true;
            ctsToCancel = _cts;
            pingCtsToCancel = _pingCts;
            taskToWait = _workerTask;
            pingTaskToWait = _pingTask;
        }

        _logger.LogInformation("Остановка ModbusDispatcher...");

        // Завершаем каналы чтобы новые EnqueueAsync не зависли
        _highQueue?.Writer.TryComplete();
        _lowQueue?.Writer.TryComplete();

        // Отменяем tasks
        if (pingCtsToCancel != null)
        {
            await pingCtsToCancel.CancelAsync().ConfigureAwait(false);
        }
        await ctsToCancel.CancelAsync().ConfigureAwait(false);

        // Закрываем порт СРАЗУ — прервёт in-flight NModbus команду (IOException)
        _connectionManager.Close();
        _isPortOpen = false;

        // Уведомляем подписчиков с таймаутом — polling уже получил IOException, быстро завершится
        try
        {
            await NotifyDisconnectingAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Таймаут ожидания Disconnecting handlers (2 сек)");
        }

        // Ждём завершения ping и worker ПАРАЛЛЕЛЬНО с общим таймаутом 5 сек
        var tasksToWait = new List<Task>();
        if (pingTaskToWait != null) tasksToWait.Add(pingTaskToWait);
        tasksToWait.Add(taskToWait);

        try
        {
            var allTasks = Task.WhenAll(tasksToWait);
            await allTasks.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Tasks не завершились после Close() — это критический баг
            const string message = "CRITICAL: Worker/Ping tasks не завершились за 5 сек после Close(). " +
                                   "Это баг в NModbus или коде диспетчера.";
            _logger.LogError(message);
            Environment.FailFast(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task завершился с ошибкой: {Error}", ex.Message);
        }

        // Cleanup
        CancelPendingCommands();
        _connectionManager.Close();
        _isConnected = false;
        _isPortOpen = false;
        _isReconnecting = false;
        _lastPingData = null;
        ctsToCancel.Dispose();
        pingCtsToCancel?.Dispose();

        lock (_stateLock)
        {
            _cts = null;
            _pingCts = null;
            _workerTask = null;
            _pingTask = null;
            _isStopped = true;
            _isStopping = false;
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
        CancellationTokenSource? pingCtsToDispose = null;
        var shouldCleanup = false;

        lock (_stateLock)
        {
            // Если StopAsync не вызывался (не isStopping), а воркер завершился сам
            if (!_isStopping && _workerTask == thisTask)
            {
                _workerTask = null;
                _pingTask = null;
                ctsToDispose = _cts;
                pingCtsToDispose = _pingCts;
                _cts = null;
                _pingCts = null;
                _isStopped = true;
                shouldCleanup = true;

                _logger.LogWarning("Воркер диспетчера завершился неожиданно");
            }
        }

        if (shouldCleanup)
        {
            // Отменяем ping
            try
            {
                pingCtsToDispose?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ignore
            }

            // Полная очистка как в StopAsync
            _highQueue?.Writer.TryComplete();
            _lowQueue?.Writer.TryComplete();
            CancelPendingCommands();
            _connectionManager.Close();
            _isConnected = false;
            _isPortOpen = false;
            _isReconnecting = false;
            _lastPingData = null;
            ctsToDispose?.Dispose();
            pingCtsToDispose?.Dispose();
        }
    }

    private async Task ProcessCommandsLoopAsync(CancellationToken ct)
    {
        // Обрабатываем команды пока порт открыт (не пока IsConnected)
        while (!ct.IsCancellationRequested && _isPortOpen)
        {
            var command = await WaitForCommandAsync(ct).ConfigureAwait(false);

            if (command == null)
            {
                continue;
            }

            try
            {
                await ExecuteCommandAsync(command, ct).ConfigureAwait(false);

                // Первая успешная команда устанавливает IsConnected
                if (!_isConnected)
                {
                    _isConnected = true;
                    _logger.LogInformation("Соединение с устройством подтверждено");
                    NotifyConnectedSafely();
                }
            }
            catch (Exception ex) when (IsCommunicationError(ex))
            {
                command.SetException(ex);
                _isConnected = false;
                _logger.LogWarning("Ошибка связи: {Error}. Переподключение...", ex.Message);
                throw; // Выходим из ProcessCommandsLoopAsync для reconnect
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
        var highQueue = _highQueue;
        var lowQueue = _lowQueue;

        if (highQueue == null || lowQueue == null)
        {
            return null;
        }

        // Сначала обрабатываем все высокоприоритетные команды
        while (highQueue.Reader.TryRead(out var highCmd))
        {
            if (!highCmd.CancellationToken.IsCancellationRequested)
            {
                return highCmd;
            }
            highCmd.SetCanceled();
        }

        // Затем одну низкоприоритетную
        if (lowQueue.Reader.TryRead(out var lowCmd))
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
            var highTask = highQueue.Reader.WaitToReadAsync(timeoutCts.Token).AsTask();
            var lowTask = lowQueue.Reader.WaitToReadAsync(timeoutCts.Token).AsTask();

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

    #region Ping Keep-Alive

    private async Task RunPingLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.PingIntervalMs));

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                // Только отправляем ping если порт открыт
                if (!_isPortOpen)
                {
                    continue;
                }

                try
                {
                    var command = new PingCommand(CommandPriority.Low, ct);
                    await EnqueueAsync(command, ct).ConfigureAwait(false);

                    var pingData = await command.Task.ConfigureAwait(false);
                    _lastPingData = pingData;
                    NotifyPingDataUpdatedSafely(pingData);

                    _logger.LogDebug("Ping OK: ModeKey={ModeKey:X8}, BoilerStatus={BoilerStatus}",
                        pingData.ModeKey, pingData.BoilerStatus);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Ping failure будет обработан dispatcher'ом при выполнении команды
                    _logger.LogDebug("Ping failed: {Error}", ex.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected при StopAsync
        }
    }

    #endregion

    #region Connection Management

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_isPortOpen && _connectionManager.IsConnected)
        {
            return;
        }

        _isReconnecting = true;
        var delay = _options.ReconnectDelayMs;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _connectionManager.Connect();
                _isPortOpen = true;
                _isReconnecting = false;
                // IsConnected остаётся false пока не выполнится первая команда
                _logger.LogInformation("COM-порт открыт. Ожидание первой успешной команды...");
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
                // Фиксированный интервал, без exponential backoff
            }
        }
    }

    private async Task HandleConnectionErrorAsync(CancellationToken ct)
    {
        _isConnected = false;
        _isPortOpen = false;

        _connectionManager.Close();

        // Таймаут на уведомление — защита от зависшего подписчика
        try
        {
            await NotifyDisconnectingAsync().WaitAsync(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Таймаут ожидания Disconnecting handlers при reconnect (2 сек)");
        }

        await Task.Delay(_options.ReconnectDelayMs, ct).ConfigureAwait(false);
    }

    private async Task NotifyDisconnectingAsync()
    {
        // Атомарная проверка и установка: если 0 → устанавливаем 1 и продолжаем
        if (Interlocked.CompareExchange(ref _isNotifyingDisconnect, 1, 0) != 0)
        {
            return; // Уже выполняется
        }

        try
        {
            var handler = Disconnecting;
            if (handler != null)
            {
                await handler.Invoke().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике Disconnecting: {Error}", ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _isNotifyingDisconnect, 0);
        }
    }

    /// <summary>
    /// Безопасно уведомляет о подключении.
    /// </summary>
    private void NotifyConnectedSafely()
    {
        try
        {
            Connected?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике Connected: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Безопасно уведомляет об обновлении данных ping.
    /// </summary>
    private void NotifyPingDataUpdatedSafely(DiagnosticPingData data)
    {
        try
        {
            PingDataUpdated?.Invoke(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике PingDataUpdated: {Error}", ex.Message);
        }
    }

    #endregion

    #region Channel Management

    /// <summary>
    /// Пересоздаёт каналы команд.
    /// </summary>
    private void RecreateChannels()
    {
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

    #endregion

    #region Error Handling

    private static bool IsCommunicationError(Exception ex)
    {
        return ex is TimeoutException
            || ex is IOException
            || ex.Message.Contains("port", StringComparison.OrdinalIgnoreCase);
    }

    private void CancelPendingCommands()
    {
        if (_highQueue != null)
        {
            while (_highQueue.Reader.TryRead(out var cmd))
            {
                cmd.SetCanceled();
            }
        }

        if (_lowQueue != null)
        {
            while (_lowQueue.Reader.TryRead(out var cmd))
            {
                cmd.SetCanceled();
            }
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
