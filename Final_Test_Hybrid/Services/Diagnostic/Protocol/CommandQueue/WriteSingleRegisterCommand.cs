using NModbus;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Команда записи одного регистра.
/// </summary>
public class WriteSingleRegisterCommand : ModbusCommandBase
{
    private readonly ushort _address;
    private readonly ushort _value;

    /// <summary>
    /// Создаёт команду записи регистра.
    /// </summary>
    public WriteSingleRegisterCommand(
        ushort address,
        ushort value,
        CommandPriority priority,
        CancellationToken ct)
        : base(priority, ct)
    {
        _address = address;
        _value = value;
    }

    /// <inheritdoc />
    protected override Task ExecuteCoreAsync(IModbusMaster master, byte slaveId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        master.WriteSingleRegister(slaveId, _address, _value);
        return Task.CompletedTask;
    }
}
