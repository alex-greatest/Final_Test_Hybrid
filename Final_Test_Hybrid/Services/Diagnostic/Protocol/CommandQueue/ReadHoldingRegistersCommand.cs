using NModbus;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Команда чтения holding регистров.
/// </summary>
public class ReadHoldingRegistersCommand : ModbusCommandBase<ushort[]>
{
    private readonly ushort _address;
    private readonly ushort _count;

    /// <summary>
    /// Создаёт команду чтения регистров.
    /// </summary>
    public ReadHoldingRegistersCommand(
        ushort address,
        ushort count,
        CommandPriority priority,
        CancellationToken ct)
        : base(priority, ct)
    {
        _address = address;
        _count = count;
    }

    /// <inheritdoc />
    protected override Task<ushort[]> ExecuteCoreAsync(IModbusMaster master, byte slaveId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = master.ReadHoldingRegisters(slaveId, _address, _count);
        return System.Threading.Tasks.Task.FromResult(result);
    }
}
