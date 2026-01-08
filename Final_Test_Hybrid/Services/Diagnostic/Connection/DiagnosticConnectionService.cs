using System.IO.Ports;
using AsyncAwaitBestPractices;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Diagnostic.Polling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NModbus;
using NModbus.Serial;

namespace Final_Test_Hybrid.Services.Diagnostic.Connection;

/// <summary>
/// Сервис подключения к ЭБУ котла через COM-порт (ModBus RTU).
/// </summary>
public class DiagnosticConnectionService(
    IOptions<DiagnosticSettings> settingsOptions,
    DiagnosticConnectionState connectionState,
    PollingPauseCoordinator pauseCoordinator,
    ILogger<DiagnosticConnectionService> logger,
    IModbusMaster? modbusMaster)
    : IAsyncDisposable
{
    private readonly DiagnosticSettings _settings = settingsOptions.Value;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Lock _reconnectLock = new();
    private SerialPort? _serialPort;
    private SerialPortAdapter? _serialPortAdapter;
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;

    /// <summary>
    /// Вызывается перед отключением.
    /// </summary>
    public event Func<Task>? Disconnecting;

    /// <summary>
    /// Флаг подключения к устройству.
    /// </summary>
    public bool IsConnected => _serialPort?.IsOpen == true && ModbusMaster != null;

    /// <summary>
    /// Флаг процесса переподключения.
    /// </summary>
    private volatile bool _isReconnecting;
    public bool IsReconnecting => _isReconnecting;

    /// <summary>
    /// ModBus master для выполнения операций.
    /// </summary>
    internal IModbusMaster? ModbusMaster { get; private set; } = modbusMaster;

    /// <summary>
    /// Адрес ведомого устройства (Slave ID).
    /// </summary>
    public byte SlaveId => _settings.SlaveId;

    #region Public Methods

    /// <summary>
    /// Подключается к COM-порту и создаёт ModBus master.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsConnected)
            {
                return;
            }

            await ConnectWithRetryAsync(ct).ConfigureAwait(false);
            connectionState.SetConnected(true);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Запускает процесс переподключения при потере связи.
    /// </summary>
    public void StartReconnect()
    {
        ExecuteReconnectAsync().SafeFireAndForget(ex =>
            logger.LogError(ex, "Ошибка при переподключении к {Port}", _settings.PortName));
    }

    /// <summary>
    /// Отключается от COM-порта.
    /// </summary>
    public async Task DisconnectAsync()
    {
        // Сначала отменяем reconnect БЕЗ семафора чтобы избежать deadlock
        await CancelReconnectAsync().ConfigureAwait(false);

        // Теперь безопасно захватываем семафор
        await using var _ = await AsyncLock.AcquireAsync(_semaphore).ConfigureAwait(false);

        await NotifyDisconnectingAsync().ConfigureAwait(false);

        CloseConnection();
        connectionState.SetConnected(false);

        logger.LogInformation("Отключено от {Port}", _settings.PortName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
        _semaphore.Dispose();
    }

    #endregion

    #region Connection Lifecycle

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (TryOpenConnection())
            {
                return;
            }

            await WaitBeforeRetryAsync(ct).ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();
    }

    private bool TryOpenConnection()
    {
        try
        {
            OpenConnection();
            logger.LogInformation("Подключено к ЭБУ котла через {Port}", _settings.PortName);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось подключиться к {Port}. Повтор через {Interval} мс",
                _settings.PortName, _settings.ReconnectIntervalMs);
            return false;
        }
    }

    private async Task WaitBeforeRetryAsync(CancellationToken ct)
    {
        await Task.Delay(_settings.ReconnectIntervalMs, ct).ConfigureAwait(false);
    }

    private void OpenConnection()
    {
        CloseConnection();

        var port = CreateSerialPort();
        try
        {
            port.Open();
            _serialPort = port;
            _serialPortAdapter = new SerialPortAdapter(port);
            var factory = new ModbusFactory();
            ModbusMaster = factory.CreateRtuMaster(_serialPortAdapter);
        }
        catch
        {
            port.Dispose();
            _serialPort = null;
            throw;
        }
    }

    private SerialPort CreateSerialPort()
    {
        return new SerialPort(_settings.PortName)
        {
            BaudRate = _settings.BaudRate,
            DataBits = _settings.DataBits,
            Parity = _settings.Parity,
            StopBits = _settings.StopBits,
            ReadTimeout = _settings.ReadTimeoutMs,
            WriteTimeout = _settings.WriteTimeoutMs
        };
    }

    private void CloseConnection()
    {
        DisposeModbusMaster();
        DisposeSerialResources();
    }

    private void DisposeModbusMaster()
    {
        ModbusMaster?.Dispose();
        ModbusMaster = null;
    }

    private void DisposeSerialResources()
    {
        if (_serialPort?.IsOpen == true)
        {
            _serialPort.Close();
        }

        _serialPortAdapter?.Dispose();
        _serialPortAdapter = null;

        _serialPort?.Dispose();
        _serialPort = null;
    }

    #endregion

    #region Reconnection

    private async Task ExecuteReconnectAsync()
    {
        await using var _ = await AsyncLock.AcquireAsync(_semaphore).ConfigureAwait(false);

        if (IsReconnecting)
        {
            return;
        }

        await PerformReconnectAsync().ConfigureAwait(false);
    }

    private async Task PerformReconnectAsync()
    {
        _isReconnecting = true;
        connectionState.SetConnected(false);

        await pauseCoordinator.PauseAsync().ConfigureAwait(false);

        lock (_reconnectLock)
        {
            _reconnectCts = new CancellationTokenSource();
        }

        try
        {
            await ReconnectWithLoggingAsync().ConfigureAwait(false);
        }
        finally
        {
            FinalizeReconnect();
        }
    }

    private async Task ReconnectWithLoggingAsync()
    {
        logger.LogWarning("Соединение с {Port} потеряно. Запуск переподключения...", _settings.PortName);

        await ConnectWithRetryAsync(_reconnectCts!.Token).ConfigureAwait(false);
        connectionState.SetConnected(true);

        logger.LogInformation("Переподключение к {Port} выполнено успешно", _settings.PortName);
    }

    private void FinalizeReconnect()
    {
        _isReconnecting = false;
        pauseCoordinator.Resume();

        lock (_reconnectLock)
        {
            _reconnectCts?.Dispose();
            _reconnectCts = null;
        }
    }

    #endregion

    #region Disconnect Helpers

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
            logger.LogError(ex, "Ошибка в обработчике события Disconnecting");
        }
    }

    private async Task CancelReconnectAsync()
    {
        CancellationTokenSource? cts;
        lock (_reconnectLock)
        {
            cts = _reconnectCts;
        }

        if (cts != null)
        {
            await cts.CancelAsync();
            // НЕ dispose здесь - это сделает FinalizeReconnect после завершения reconnect task
        }
    }

    #endregion
}