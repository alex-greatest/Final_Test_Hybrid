using System.IO.Ports;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NModbus;
using NModbus.Serial;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Управляет физическим подключением к устройству Modbus.
/// НЕ потокобезопасен - используется только из единственного воркера ModbusDispatcher.
/// </summary>
public class ModbusConnectionManager : IDisposable
{
    private readonly DiagnosticSettings _settings;
    private readonly ILogger _logger;

    private SerialPort? _serialPort;
    private SerialPortAdapter? _serialPortAdapter;
    private bool _disposed;

    /// <summary>
    /// Создаёт менеджер подключения.
    /// </summary>
    public ModbusConnectionManager(
        IOptions<DiagnosticSettings> settingsOptions,
        ILogger<ModbusConnectionManager> logger)
    {
        _settings = settingsOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// True если подключение установлено.
    /// </summary>
    public bool IsConnected => _serialPort?.IsOpen == true && ModbusMaster != null;

    /// <summary>
    /// Текущий Modbus master (null если не подключено).
    /// </summary>
    public IModbusMaster? ModbusMaster { get; private set; }

    /// <summary>
    /// ID ведомого устройства.
    /// </summary>
    public byte SlaveId => _settings.SlaveId;

    /// <summary>
    /// Открывает подключение к устройству.
    /// </summary>
    public void Connect()
    {
        ThrowIfDisposed();
        Close();

        var port = CreateSerialPort();
        SerialPortAdapter? adapter = null;
        try
        {
            port.Open();
            adapter = new SerialPortAdapter(port);
            var master = new ModbusFactory().CreateRtuMaster(adapter);

            // Присваиваем поля только после успешного создания всех ресурсов
            _serialPort = port;
            _serialPortAdapter = adapter;
            ModbusMaster = master;

            _logger.LogInformation("Подключено к ЭБУ котла через {Port}", _settings.PortName);
        }
        catch
        {
            // Очищаем созданные ресурсы в обратном порядке
            try
            {
                adapter?.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }

            try
            {
                if (port.IsOpen)
                {
                    port.Close();
                }

                port.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }

            throw;
        }
    }

    /// <summary>
    /// Закрывает подключение.
    /// </summary>
    public void Close()
    {
        try
        {
            DisposeModbusMaster();
        }
        finally
        {
            DisposeSerialResources();
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

    private void DisposeModbusMaster()
    {
        ModbusMaster?.Dispose();
        ModbusMaster = null;
    }

    private void DisposeSerialResources()
    {
        // Dispose в обратном порядке создания: adapter → port
        // Adapter first чтобы избежать использования закрытого порта
        var adapter = _serialPortAdapter;
        var port = _serialPort;

        _serialPortAdapter = null;
        _serialPort = null;

        try
        {
            adapter?.Dispose();
        }
        catch
        {
            // Ignore cleanup errors
        }

        try
        {
            if (port?.IsOpen == true)
            {
                port.Close();
            }

            port?.Dispose();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Close();
    }
}
