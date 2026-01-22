using Final_Test_Hybrid.Services.Diagnostic.Models;
using NModbus;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Команда ping для проверки связи и чтения базовых параметров.
/// Читает 6 регистров: ModeKey (2) + reserved (3) + BoilerStatus (1).
/// </summary>
public class PingCommand : ModbusCommandBase<DiagnosticPingData>
{
    private const ushort ModeKeyAddressDoc = 1000;  // Документация: 1000-1001
    private const ushort RegisterCount = 6;         // 6 регистров

    private readonly ushort _baseAddress;

    /// <summary>
    /// Создаёт команду ping.
    /// </summary>
    /// <param name="priority">Приоритет команды (обычно Low).</param>
    /// <param name="baseAddressOffset">Смещение базового адреса из настроек.</param>
    /// <param name="ct">Токен отмены.</param>
    public PingCommand(CommandPriority priority, ushort baseAddressOffset, CancellationToken ct)
        : base(priority, ct)
    {
        _baseAddress = (ushort)(ModeKeyAddressDoc - baseAddressOffset);
    }

    /// <inheritdoc />
    protected override Task<DiagnosticPingData> ExecuteCoreAsync(IModbusMaster master, byte slaveId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var registers = master.ReadHoldingRegisters(slaveId, _baseAddress, RegisterCount);

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
