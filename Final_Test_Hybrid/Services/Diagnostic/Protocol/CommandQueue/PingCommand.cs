using Final_Test_Hybrid.Services.Diagnostic.Models;
using NModbus;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Команда ping для проверки связи и чтения базовых параметров.
/// Читает 6 регистров: ModeKey (2) + reserved (3) + BoilerStatus (1).
/// </summary>
public class PingCommand : ModbusCommandBase<DiagnosticPingData>
{
    // Modbus адреса (документация -1)
    private const ushort ModeKeyAddress = 999;  // Документация: 1000-1001
    private const ushort RegisterCount = 6;     // 999-1004

    /// <summary>
    /// Создаёт команду ping.
    /// </summary>
    /// <param name="priority">Приоритет команды (обычно Low).</param>
    /// <param name="ct">Токен отмены.</param>
    public PingCommand(CommandPriority priority, CancellationToken ct)
        : base(priority, ct)
    {
    }

    /// <inheritdoc />
    protected override Task<DiagnosticPingData> ExecuteCoreAsync(IModbusMaster master, byte slaveId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Читаем 6 регистров: 999-1004
        var registers = master.ReadHoldingRegisters(slaveId, ModeKeyAddress, RegisterCount);

        // ModeKey: регистры 0-1 (999-1000) — Big Endian
        var modeKey = ((uint)registers[0] << 16) | registers[1];

        // BoilerStatus: регистр 5 (1004)
        var boilerStatus = (short)registers[5];

        var pingData = new DiagnosticPingData
        {
            ModeKey = modeKey,
            BoilerStatus = boilerStatus
        };

        return System.Threading.Tasks.Task.FromResult(pingData);
    }
}
