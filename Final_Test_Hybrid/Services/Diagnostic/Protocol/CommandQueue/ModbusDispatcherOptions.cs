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
    /// Интервал переподключения (мс).
    /// Фиксированный интервал между попытками.
    /// </summary>
    public int ReconnectDelayMs { get; set; } = 5000;

    /// <summary>
    /// Таймаут ожидания команды в очереди (мс).
    /// </summary>
    public int CommandWaitTimeoutMs { get; set; } = 100;

    /// <summary>
    /// Интервал ping keep-alive (мс).
    /// </summary>
    public int PingIntervalMs { get; set; } = 5000;
}
