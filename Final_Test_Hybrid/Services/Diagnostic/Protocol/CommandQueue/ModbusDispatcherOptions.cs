namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Настройки диспетчера команд Modbus.
/// </summary>
public class ModbusDispatcherOptions
{
    /// <summary>
    /// Максимальный размер очереди высокого приоритета.
    /// </summary>
    public int HighPriorityQueueCapacity { get; set; } = 100;

    /// <summary>
    /// Максимальный размер очереди низкого приоритета.
    /// </summary>
    public int LowPriorityQueueCapacity { get; set; } = 10;

    /// <summary>
    /// Начальный интервал переподключения (мс).
    /// </summary>
    public int InitialReconnectDelayMs { get; set; } = 1000;

    /// <summary>
    /// Максимальный интервал переподключения (мс).
    /// </summary>
    public int MaxReconnectDelayMs { get; set; } = 30000;

    /// <summary>
    /// Множитель для exponential backoff при переподключении.
    /// </summary>
    public double ReconnectBackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Таймаут ожидания команды в очереди (мс).
    /// </summary>
    public int CommandWaitTimeoutMs { get; set; } = 100;
}
