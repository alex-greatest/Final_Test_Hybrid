using System.IO.Ports;

namespace Final_Test_Hybrid.Services.Diagnostic.Connection;

/// <summary>
/// Настройки подключения к ЭБУ котла через COM-порт.
/// </summary>
public class DiagnosticSettings
{
    /// <summary>
    /// Имя COM-порта (например, "COM3").
    /// </summary>
    public string PortName { get; set; } = "COM1";

    /// <summary>
    /// Скорость передачи данных (бод).
    /// </summary>
    public int BaudRate { get; set; } = 115200;

    /// <summary>
    /// Количество бит данных.
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// Контроль чётности.
    /// </summary>
    public Parity Parity { get; set; } = Parity.None;

    /// <summary>
    /// Стоповые биты.
    /// </summary>
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>
    /// Адрес ведомого устройства ModBus (Slave ID).
    /// </summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>
    /// Таймаут чтения в миллисекундах.
    /// </summary>
    public int ReadTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// Таймаут записи в миллисекундах.
    /// </summary>
    public int WriteTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// Интервал автопереподключения в миллисекундах.
    /// </summary>
    public int ReconnectIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Смещение базового адреса регистров.
    /// </summary>
    /// <remarks>
    /// Адреса из документации (BaseAddress=1) уменьшаются на это значение при обращении к Modbus.
    /// По умолчанию 1 (адрес 1005 в документации → 1004 в Modbus).
    /// </remarks>
    public ushort BaseAddressOffset { get; set; } = 1;
}
