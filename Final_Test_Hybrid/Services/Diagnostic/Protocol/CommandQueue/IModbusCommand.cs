using NModbus;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Интерфейс команды Modbus для выполнения через диспетчер.
/// </summary>
public interface IModbusCommand
{
    /// <summary>
    /// Приоритет команды.
    /// </summary>
    CommandPriority Priority { get; }

    /// <summary>
    /// Токен отмены операции.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Выполняет команду на указанном Modbus master.
    /// </summary>
    /// <param name="master">Modbus master для выполнения операции.</param>
    /// <param name="slaveId">ID ведомого устройства.</param>
    /// <param name="ct">Токен отмены.</param>
    Task ExecuteAsync(IModbusMaster master, byte slaveId, CancellationToken ct);

    /// <summary>
    /// Устанавливает исключение для команды.
    /// </summary>
    void SetException(Exception ex);

    /// <summary>
    /// Отменяет команду.
    /// </summary>
    void SetCanceled();
}
