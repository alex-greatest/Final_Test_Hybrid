using Final_Test_Hybrid.Services.Common.Logging;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue.Internal;

/// <summary>
/// Логика основного цикла обработки команд Modbus.
/// Не владеет состоянием - получает и устанавливает его через делегаты.
/// Не владеет CTS/Task и соединением - это ответственность фасада.
/// </summary>
internal sealed class ModbusWorkerLoop
{
    private readonly ModbusConnectionManager _connectionManager;
    private readonly ModbusCommandQueue _commandQueue;
    private readonly ModbusDispatcherOptions _options;
    private readonly IDualLogger _logger;

    /// <summary>
    /// Вызывается при первой успешной команде.
    /// </summary>
    public Action? OnFirstCommandSucceeded;

    /// <summary>
    /// Вызывается при потере соединения для уведомления фасада.
    /// </summary>
    public Func<CancellationToken, Task>? OnConnectionLost;

    /// <summary>
    /// Вызывается для установки соединения. Фасад владеет connect.
    /// </summary>
    public Action? DoConnect;

    /// <summary>
    /// Вызывается для закрытия соединения. Фасад владеет close.
    /// </summary>
    public Action? DoClose;

    /// <summary>
    /// Создаёт экземпляр воркера.
    /// </summary>
    /// <param name="connectionManager">Менеджер подключения (только для чтения состояния и выполнения команд).</param>
    /// <param name="commandQueue">Очередь команд.</param>
    /// <param name="options">Настройки диспетчера.</param>
    /// <param name="logger">Логгер.</param>
    public ModbusWorkerLoop(
        ModbusConnectionManager connectionManager,
        ModbusCommandQueue commandQueue,
        ModbusDispatcherOptions options,
        IDualLogger logger)
    {
        _connectionManager = connectionManager;
        _commandQueue = commandQueue;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Выполняет один цикл: connect, process commands, handle error.
    /// </summary>
    /// <param name="isPortOpen">Функция проверки состояния порта.</param>
    /// <param name="setPortOpen">Функция установки состояния порта.</param>
    /// <param name="isConnected">Функция проверки состояния подключения.</param>
    /// <param name="setConnected">Функция установки состояния подключения.</param>
    /// <param name="setReconnecting">Функция установки флага переподключения.</param>
    /// <param name="isStopping">Функция проверки остановки.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>True если нужно продолжать цикл, false если cancelled.</returns>
    public async Task<bool> RunIterationAsync(
        Func<bool> isPortOpen,
        Action<bool> setPortOpen,
        Func<bool> isConnected,
        Action<bool> setConnected,
        Action<bool> setReconnecting,
        Func<bool> isStopping,
        CancellationToken ct)
    {
        try
        {
            await EnsureConnectedAsync(isPortOpen, setPortOpen, setReconnecting, ct).ConfigureAwait(false);
            await ProcessCommandsLoopAsync(isPortOpen, isConnected, setConnected, isStopping, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в воркере диспетчера: {Error}", ex.Message);
            await HandleConnectionErrorAsync(setConnected, setPortOpen, ct).ConfigureAwait(false);
            return true;
        }
    }

    /// <summary>
    /// Ожидает команду с приоритетом high > low.
    /// </summary>
    /// <param name="timeoutMs">Таймаут ожидания в мс.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Команда или null если таймаут.</returns>
    public async Task<IModbusCommand?> WaitForCommandAsync(int timeoutMs, CancellationToken ct)
    {
        var highQueue = _commandQueue.HighQueue;
        var lowQueue = _commandQueue.LowQueue;

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
        timeoutCts.CancelAfter(timeoutMs);

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

    /// <summary>
    /// Обеспечивает установку соединения через колбэк фасада.
    /// </summary>
    private async Task EnsureConnectedAsync(
        Func<bool> isPortOpen,
        Action<bool> setPortOpen,
        Action<bool> setReconnecting,
        CancellationToken ct)
    {
        if (isPortOpen() && _connectionManager.IsConnected)
        {
            return;
        }

        setReconnecting(true);
        var delay = _options.ReconnectDelayMs;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Фасад владеет connect - вызываем через колбэк
                DoConnect?.Invoke();

                // Settling delay ДО setPortOpen — иначе ping-loop будет ставить команды в очередь
                // и блокировать отправителей (bounded channel + FullMode=Wait)
                if (_options.PortOpenSettlingDelayMs > 0)
                {
                    _logger.LogDebug("Settling delay {Delay}ms перед активацией порта", _options.PortOpenSettlingDelayMs);
                    await Task.Delay(_options.PortOpenSettlingDelayMs, ct).ConfigureAwait(false);
                }

                setPortOpen(true);
                setReconnecting(false);
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
            }
        }
    }

    /// <summary>
    /// Цикл обработки команд.
    /// </summary>
    private async Task ProcessCommandsLoopAsync(
        Func<bool> isPortOpen,
        Func<bool> isConnected,
        Action<bool> setConnected,
        Func<bool> isStopping,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && isPortOpen())
        {
            var command = await WaitForCommandAsync(_options.CommandWaitTimeoutMs, ct).ConfigureAwait(false);

            if (command == null)
            {
                continue;
            }

            try
            {
                await ExecuteCommandAsync(command, ct).ConfigureAwait(false);

                // Первая успешная команда устанавливает IsConnected
                if (!isConnected())
                {
                    setConnected(true);
                    _logger.LogInformation("Соединение с устройством подтверждено");
                    OnFirstCommandSucceeded?.Invoke();
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested || command.CancellationToken.IsCancellationRequested)
            {
                // Штатная отмена по нашим токенам - без логирования ошибки
                command.SetCanceled();
            }
            catch (OperationCanceledException ex) when (isStopping())
            {
                // Отмена при остановке (не по токену) - Debug для наблюдаемости
                command.SetCanceled();
                _logger.LogDebug("Команда отменена при остановке: {Error}", ex.Message);
            }
            catch (Exception ex) when (!isStopping() && CommunicationErrorHelper.IsCommunicationError(ex))
            {
                command.SetException(ex);
                setConnected(false);
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

    /// <summary>
    /// Выполняет команду.
    /// </summary>
    private async Task ExecuteCommandAsync(IModbusCommand command, CancellationToken ct)
    {
        var master = _connectionManager.ModbusMaster
            ?? throw new InvalidOperationException("Нет подключения к устройству");

        var slaveId = _connectionManager.SlaveId;

        await Task.Run(() => command.ExecuteAsync(master, slaveId, ct), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Обрабатывает ошибку соединения.
    /// </summary>
    private async Task HandleConnectionErrorAsync(
        Action<bool> setConnected,
        Action<bool> setPortOpen,
        CancellationToken ct)
    {
        setConnected(false);
        setPortOpen(false);

        // Фасад владеет close - вызываем через колбэк
        DoClose?.Invoke();

        // Уведомляем фасад о потере соединения (он очистит _lastPingData)
        if (OnConnectionLost != null)
        {
            try
            {
                await OnConnectionLost(ct).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Таймаут ожидания Disconnecting handlers при reconnect (2 сек)");
            }
        }

        await Task.Delay(_options.ReconnectDelayMs, ct).ConfigureAwait(false);
    }
}
