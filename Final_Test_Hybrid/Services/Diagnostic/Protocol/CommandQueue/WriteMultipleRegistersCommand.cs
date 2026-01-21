using NModbus;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Команда записи нескольких регистров.
/// </summary>
public class WriteMultipleRegistersCommand : ModbusCommandBase
{
    private readonly ushort _address;
    private readonly ushort[] _values;

    /// <summary>
    /// Создаёт команду записи нескольких регистров.
    /// </summary>
    public WriteMultipleRegistersCommand(
        ushort address,
        ushort[] values,
        CommandPriority priority,
        CancellationToken ct)
        : base(priority, ct)
    {
        _address = address;
        _values = values;
    }

    /// <inheritdoc />
    protected override Task ExecuteCoreAsync(IModbusMaster master, byte slaveId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        master.WriteMultipleRegisters(slaveId, _address, _values);
        return Task.CompletedTask;
    }
}
