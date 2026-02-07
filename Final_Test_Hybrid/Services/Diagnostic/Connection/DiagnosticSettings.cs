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

    /// <summary>
    /// Задержка после записи перед чтением для верификации (мс).
    /// </summary>
    public int WriteVerifyDelayMs { get; set; } = 100;

    /// <summary>
    /// Настройки runtime-обработки блокировок котла по статусу 1005.
    /// </summary>
    public DiagnosticBoilerLockSettings BoilerLock { get; set; } = new();
}

/// <summary>
/// Флаги управления runtime-логикой блокировок котла.
/// </summary>
public class DiagnosticBoilerLockSettings
{
    /// <summary>
    /// Общий флаг включения логики блокировок.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Включить ветку pause для статуса 1005 == 1.
    /// </summary>
    public bool PauseOnStatus1Enabled { get; set; }

    /// <summary>
    /// Включить ветку PLC-сигнала для статуса 1005 == 2.
    /// </summary>
    public bool PlcSignalOnStatus2Enabled { get; set; }

    /// <summary>
    /// Тонкие настройки reset-flow для ветки BoilerLock status=1.
    /// </summary>
    public DiagnosticBoilerLockResetFlowSettings ResetFlow { get; set; } = new();
}

/// <summary>
/// Настройки ограниченных retry/cooldown для BoilerLock reset-flow.
/// </summary>
public class DiagnosticBoilerLockResetFlowSettings
{
    /// <summary>
    /// Требовать подтверждённый режим Stand перед записью 1153=0.
    /// </summary>
    public bool RequireStandForReset { get; set; } = true;

    /// <summary>
    /// Максимум попыток перевода в Stand в одном ping-цикле.
    /// </summary>
    public int ModeSwitchRetryMax { get; set; } = 2;

    /// <summary>
    /// Максимум попыток записи 1153=0 в одном ping-цикле.
    /// </summary>
    public int ResetRetryMax { get; set; } = 3;

    /// <summary>
    /// Задержка между retry-попытками (мс).
    /// </summary>
    public int RetryDelayMs { get; set; } = 250;

    /// <summary>
    /// Минимальный интервал между циклами попыток (мс).
    /// </summary>
    public int AttemptCooldownMs { get; set; } = 1000;

    /// <summary>
    /// Окно подавления новых попыток после серии ошибок (мс).
    /// </summary>
    public int ErrorSuppressMs { get; set; } = 5000;
}
