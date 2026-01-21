using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol;

/// <summary>
/// Интерфейс клиента Modbus для чтения и записи регистров.
/// </summary>
public interface IModbusClient : IAsyncDisposable
{
    /// <summary>
    /// Читает holding регистры (функция 0x03).
    /// </summary>
    /// <param name="address">Начальный адрес.</param>
    /// <param name="count">Количество регистров.</param>
    /// <param name="priority">Приоритет команды.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Массив значений регистров.</returns>
    Task<ushort[]> ReadHoldingRegistersAsync(
        ushort address,
        ushort count,
        CommandPriority priority = CommandPriority.High,
        CancellationToken ct = default);

    /// <summary>
    /// Записывает один регистр (функция 0x06).
    /// </summary>
    /// <param name="address">Адрес регистра.</param>
    /// <param name="value">Значение для записи.</param>
    /// <param name="priority">Приоритет команды.</param>
    /// <param name="ct">Токен отмены.</param>
    Task WriteSingleRegisterAsync(
        ushort address,
        ushort value,
        CommandPriority priority = CommandPriority.High,
        CancellationToken ct = default);

    /// <summary>
    /// Записывает несколько регистров (функция 0x10).
    /// </summary>
    /// <param name="address">Начальный адрес.</param>
    /// <param name="values">Значения для записи.</param>
    /// <param name="priority">Приоритет команды.</param>
    /// <param name="ct">Токен отмены.</param>
    Task WriteMultipleRegistersAsync(
        ushort address,
        ushort[] values,
        CommandPriority priority = CommandPriority.High,
        CancellationToken ct = default);
}
