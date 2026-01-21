namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Интерфейс диспетчера команд Modbus.
/// </summary>
public interface IModbusDispatcher : IAsyncDisposable
{
    /// <summary>
    /// Вызывается перед отключением.
    /// </summary>
    event Func<Task>? Disconnecting;

    /// <summary>
    /// Вызывается после успешного подключения.
    /// </summary>
    event Action? Connected;

    /// <summary>
    /// True если подключено к устройству.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// True если идёт процесс переподключения.
    /// </summary>
    bool IsReconnecting { get; }

    /// <summary>
    /// True если диспетчер запущен.
    /// </summary>
    bool IsStarted { get; }

    /// <summary>
    /// Добавляет команду в очередь на выполнение.
    /// </summary>
    /// <param name="command">Команда для выполнения.</param>
    /// <param name="ct">Токен отмены.</param>
    ValueTask EnqueueAsync(IModbusCommand command, CancellationToken ct = default);

    /// <summary>
    /// Запускает диспетчер и подключается к устройству.
    /// </summary>
    /// <param name="ct">Токен отмены.</param>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Останавливает диспетчер и отключается от устройства.
    /// </summary>
    Task StopAsync();
}
