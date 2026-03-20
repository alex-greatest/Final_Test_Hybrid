using System.Threading.Channels;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue.Internal;

/// <summary>
/// Пассивная очередь команд Modbus.
/// Отвечает только за хранение и доступ к каналам.
/// Логика планирования (high > low, timeout) остаётся в воркере.
/// </summary>
internal sealed class ModbusCommandQueue
{
    /// <summary>
    /// Канал высокоприоритетных команд.
    /// </summary>
    public Channel<IModbusCommand>? HighQueue { get; private set; }

    /// <summary>
    /// Канал низкоприоритетных команд.
    /// </summary>
    public Channel<IModbusCommand>? LowQueue { get; private set; }

    /// <summary>
    /// Пересоздаёт каналы команд с указанными настройками.
    /// </summary>
    /// <param name="options">Настройки диспетчера.</param>
    public void RecreateChannels(ModbusDispatcherOptions options)
    {
        HighQueue = Channel.CreateBounded<IModbusCommand>(
            new BoundedChannelOptions(options.HighPriorityQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        LowQueue = Channel.CreateBounded<IModbusCommand>(
            new BoundedChannelOptions(options.LowPriorityQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
    }

    /// <summary>
    /// Завершает каналы, блокируя новые записи.
    /// </summary>
    public void CompleteChannels()
    {
        HighQueue?.Writer.TryComplete();
        LowQueue?.Writer.TryComplete();
    }

    /// <summary>
    /// Отменяет все ожидающие команды в очередях.
    /// </summary>
    public void CancelAllPendingCommands()
    {
        CancelPendingCommands(HighQueue);
        CancelPendingCommands(LowQueue);
    }

    /// <summary>
    /// Завершает все ожидающие команды ошибкой reconnect.
    /// </summary>
    public void FailPendingCommandsOnReconnect(Func<IModbusCommand, Exception> exceptionFactory)
    {
        ArgumentNullException.ThrowIfNull(exceptionFactory);

        FailPendingCommands(HighQueue, exceptionFactory);
        FailPendingCommands(LowQueue, exceptionFactory);
    }

    /// <summary>
    /// Возвращает канал для указанного приоритета.
    /// </summary>
    /// <param name="priority">Приоритет команды.</param>
    /// <returns>Соответствующий канал.</returns>
    public Channel<IModbusCommand>? GetChannel(CommandPriority priority)
    {
        return priority == CommandPriority.High ? HighQueue : LowQueue;
    }

    private static void CancelPendingCommands(Channel<IModbusCommand>? queue)
    {
        while (queue?.Reader.TryRead(out var cmd) == true)
        {
            cmd.SetCanceled();
        }
    }

    private static void FailPendingCommands(
        Channel<IModbusCommand>? queue,
        Func<IModbusCommand, Exception> exceptionFactory)
    {
        while (queue?.Reader.TryRead(out var cmd) == true)
        {
            cmd.SetException(exceptionFactory(cmd));
        }
    }
}
