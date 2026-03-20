namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue.Internal;

/// <summary>
/// Создаёт единый тип ошибки для fail-fast поведения очереди при reconnect.
/// </summary>
internal static class ModbusReconnectExceptionFactory
{
    private const string BaseMessage = "Команда прервана: начато переподключение Modbus до начала выполнения";

    public static Exception CreateForPendingCommand(IModbusCommand command)
    {
        return Create(command, "pending");
    }

    public static Exception CreateForRejectedCommand(IModbusCommand command)
    {
        return Create(command, "rejected");
    }

    private static IOException Create(IModbusCommand command, string state)
    {
        return new IOException(
            $"{BaseMessage}. State={state}, Source={command.Source}, Command={command.CommandName}, Priority={command.Priority}, Details={command.Details}");
    }
}
