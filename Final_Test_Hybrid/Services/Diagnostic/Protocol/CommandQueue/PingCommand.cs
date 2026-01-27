using Final_Test_Hybrid.Services.Diagnostic.Models;
using NModbus;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Команда ping для проверки связи и чтения базовых параметров.
/// Читает 6 регистров: ModeKey (2) + reserved (3) + BoilerStatus (1).
/// Дополнительно читает регистр ошибки (soft-fail).
/// </summary>
public class PingCommand : ModbusCommandBase<DiagnosticPingData>
{
    private const ushort ModeKeyAddressDoc = 1000;   // Документация: 1000-1001
    private const ushort LastErrorAddressDoc = 1047; // Документация: 1047
    private const ushort RegisterCount = 6;          // 6 регистров

    private readonly ushort _baseAddress;
    private readonly ushort _baseAddressOffset;

    /// <summary>
    /// Создаёт команду ping.
    /// </summary>
    /// <param name="priority">Приоритет команды (обычно Low).</param>
    /// <param name="baseAddressOffset">Смещение базового адреса из настроек.</param>
    /// <param name="ct">Токен отмены.</param>
    public PingCommand(CommandPriority priority, ushort baseAddressOffset, CancellationToken ct)
        : base(priority, ct)
    {
        _baseAddressOffset = baseAddressOffset;
        _baseAddress = (ushort)(ModeKeyAddressDoc - baseAddressOffset);
    }

    /// <inheritdoc />
    protected override Task<DiagnosticPingData> ExecuteCoreAsync(IModbusMaster master, byte slaveId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Основное чтение — если падает, весь ping падает (это OK)
        var registers = master.ReadHoldingRegisters(slaveId, _baseAddress, RegisterCount);

        // ModeKey: регистры 0-1 (999-1000) — Big Endian
        var modeKey = ((uint)registers[0] << 16) | registers[1];

        // BoilerStatus: регистр 5 (1004)
        var boilerStatus = (short)registers[5];

        // Чтение ошибки — soft-fail (не ломает ping)
        var lastErrorId = ReadLastErrorSoftFail(master, slaveId, ct);

        var pingData = new DiagnosticPingData
        {
            ModeKey = modeKey,
            BoilerStatus = boilerStatus,
            LastErrorId = lastErrorId
        };

        return System.Threading.Tasks.Task.FromResult(pingData);
    }

    /// <summary>
    /// Читает регистр ошибки с soft-fail — при ошибке возвращает null, не ломает основной ping.
    /// </summary>
    private ushort? ReadLastErrorSoftFail(IModbusMaster master, byte slaveId, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var errorAddress = (ushort)(LastErrorAddressDoc - _baseAddressOffset);
            var errorRegisters = master.ReadHoldingRegisters(slaveId, errorAddress, 1);
            return errorRegisters[0];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // soft-fail: возвращаем null, ping продолжает работать
            return null;
        }
    }
}
