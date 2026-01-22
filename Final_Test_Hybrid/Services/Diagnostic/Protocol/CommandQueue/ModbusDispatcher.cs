using System.Threading.Channels;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue.Internal;
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
    // === DI зависимости ===
    private readonly ModbusConnectionManager _connectionManager;
    private readonly ModbusDispatcherOptions _options;
    private readonly DualLogger<ModbusDispatcher> _logger;

    // === Internal компоненты ===
    private readonly ModbusCommandQueue _commandQueue;
    private readonly ModbusWorkerLoop _workerLoop;
    private readonly ModbusPingLoop _pingLoop;

    // === Состояние (ЕДИНЫЙ LOCK) ===
    private readonly Lock _stateLock = new();
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _pingCts;
    private Task? _workerTask;
    private Task? _pingTask;
    private volatile bool _isStopping;
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
    /// <param name="connectionManager">Менеджер соединений Modbus.</param>
    /// <param name="options">Настройки диспетчера.</param>
    /// <param name="diagnosticSettings">Настройки диагностики (включая BaseAddressOffset).</param>
    /// <param name="logger">Логгер.</param>
    /// <param name="testStepLogger">Логгер тестовых шагов.</param>
    public ModbusDispatcher(
        ModbusConnectionManager connectionManager,
        IOptions<ModbusDispatcherOptions> options,
        IOptions<DiagnosticSettings> diagnosticSettings,
        ILogger<ModbusDispatcher> logger,
        ITestStepLogger testStepLogger)
    {
        _connectionManager = connectionManager;
        _options = options.Value;
        _logger = new DualLogger<ModbusDispatcher>(logger, testStepLogger);

        var baseAddressOffset = diagnosticSettings.Value.BaseAddressOffset;

        // Создаём internal компоненты
        _commandQueue = new ModbusCommandQueue();
        _commandQueue.RecreateChannels(_options);

        _workerLoop = new ModbusWorkerLoop(
            _connectionManager,
            _commandQueue,
            _options,
            _logger);

        // Настраиваем колбэки воркера — фасад владеет connect/close
        _workerLoop.OnFirstCommandSucceeded = NotifyConnectedSafely;
        _workerLoop.OnConnectionLost = HandleConnectionLostAsync;
        _workerLoop.DoConnect = () => _connectionManager.Connect();
        _workerLoop.DoClose = () => _connectionManager.Close();

        _pingLoop = new ModbusPingLoop(
            EnqueueAsync,
            _options,
            baseAddressOffset,
            _logger);

        // Настраиваем колбэк ping
        _pingLoop.OnPingDataReceived = HandlePingDataReceived;
    }

    /// <inheritdoc />
    public event Func<Task>? Disconnecting;

    /// <inheritdoc />
    public event Action? Connected;

    /// <inheritdoc />
    public event Action? Stopped;

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

            channel = _commandQueue.GetChannel(command.Priority);
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
                _commandQueue.RecreateChannels(_options);
                _isStopped = false;
            }

            // Не запускаем если уже работает или останавливается
            if (_workerTask != null || _isStopping)
            {
                return Task.CompletedTask;
            }

            _cts = new CancellationTokenSource();
            _workerTask = RunWorkerLoopAsync(_cts.Token);

            // Запускаем ping task с проверкой isStopping для защиты от race
            _pingCts = new CancellationTokenSource();
            _pingTask = _pingLoop.RunAsync(() => _isPortOpen, () => _isStopping, _pingCts.Token);
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

        // 1. Завершаем каналы чтобы новые EnqueueAsync не зависли
        _commandQueue.CompleteChannels();

        // 2. Отменяем tasks
        if (pingCtsToCancel != null)
        {
            await pingCtsToCancel.CancelAsync().ConfigureAwait(false);
        }
        await ctsToCancel.CancelAsync().ConfigureAwait(false);

        // 3. Закрываем порт СРАЗУ — прервёт in-flight NModbus команду (IOException)
        _connectionManager.Close();
        _isPortOpen = false;

        // 4. Отменяем команды в очереди СРАЗУ — разблокирует ping loop и другие ожидающие
        _commandQueue.CancelAllPendingCommands();

        // 5. Уведомляем подписчиков с таймаутом
        try
        {
            await NotifyDisconnectingAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Таймаут ожидания Disconnecting handlers (2 сек)");
        }

        // 6. Ждём завершения ping и worker ПАРАЛЛЕЛЬНО с общим таймаутом 5 сек
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

        // 7. Cleanup
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
        NotifyStoppedSafely();
    }

    #endregion

    #region Worker Loop

    /// <summary>
    /// Основной цикл воркера. Делегирует логику в ModbusWorkerLoop.
    /// </summary>
    private async Task RunWorkerLoopAsync(CancellationToken ct)
    {
        // Запоминаем текущую задачу для корректной очистки
        var thisTask = _workerTask;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var shouldContinue = await _workerLoop.RunIterationAsync(
                    isPortOpen: () => _isPortOpen,
                    setPortOpen: value => _isPortOpen = value,
                    isConnected: () => _isConnected,
                    setConnected: value => _isConnected = value,
                    setReconnecting: value => _isReconnecting = value,
                    isStopping: () => _isStopping,
                    ct).ConfigureAwait(false);

                if (!shouldContinue)
                {
                    break;
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
    /// КРИТИЧНО: этот метод остаётся в фасаде для сохранения единого lock.
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
            _commandQueue.CompleteChannels();
            _commandQueue.CancelAllPendingCommands();
            _connectionManager.Close();
            _isConnected = false;
            _isPortOpen = false;
            _isReconnecting = false;
            _lastPingData = null;
            ctsToDispose?.Dispose();
            pingCtsToDispose?.Dispose();

            NotifyStoppedSafely();
        }
    }

    #endregion

    #region Event Notifications

    /// <summary>
    /// Обрабатывает потерю соединения: очищает ping данные и уведомляет подписчиков.
    /// </summary>
    private async Task HandleConnectionLostAsync(CancellationToken ct)
    {
        // Очищаем старые ping данные при разрыве соединения
        _lastPingData = null;

        // Уведомляем подписчиков с таймаутом
        await NotifyDisconnectingAsync().WaitAsync(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Обрабатывает полученные данные ping.
    /// </summary>
    private void HandlePingDataReceived(DiagnosticPingData data)
    {
        _lastPingData = data;
        NotifyPingDataUpdatedSafely(data);
    }

    /// <summary>
    /// Уведомляет о разрыве соединения с защитой от параллельных вызовов.
    /// </summary>
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
    /// Безопасно уведомляет об остановке диспетчера.
    /// </summary>
    private void NotifyStoppedSafely()
    {
        try
        {
            Stopped?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике Stopped: {Error}", ex.Message);
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

    #region Dispose

    /// <summary>
    /// Проверяет, что объект не освобождён.
    /// </summary>
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
