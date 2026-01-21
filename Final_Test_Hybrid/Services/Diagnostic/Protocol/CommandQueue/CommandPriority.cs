namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Приоритет команды в очереди Modbus.
/// </summary>
public enum CommandPriority
{
    /// <summary>
    /// Высокий приоритет для разовых операций (чтение/запись по запросу пользователя).
    /// </summary>
    High,

    /// <summary>
    /// Низкий приоритет для фоновых операций (периодический polling).
    /// </summary>
    Low
}
