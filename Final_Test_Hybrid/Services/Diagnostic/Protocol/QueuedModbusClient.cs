using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol;

/// <summary>
/// Реализация IModbusClient через приоритетную очередь команд.
/// Все операции маршрутизируются через ModbusDispatcher.
/// Требует ручного запуска диспетчера через StartAsync().
/// </summary>
public class QueuedModbusClient(IModbusDispatcher dispatcher) : IModbusClient
{
    private readonly Lock _lock = new();
    private bool _disposed;
    private bool _disposing;

    /// <inheritdoc />
    public async Task<ushort[]> ReadHoldingRegistersAsync(
        ushort address,
        ushort count,
        CommandPriority priority = CommandPriority.High,
        CancellationToken ct = default)
    {
        ThrowIfDisposedOrDisposing();
        ThrowIfNotStarted();
        var command = new ReadHoldingRegistersCommand(address, count, priority, ct);
        await dispatcher.EnqueueAsync(command, ct).ConfigureAwait(false);
        return await command.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteSingleRegisterAsync(
        ushort address,
        ushort value,
        CommandPriority priority = CommandPriority.High,
        CancellationToken ct = default)
    {
        ThrowIfDisposedOrDisposing();
        ThrowIfNotStarted();
        var command = new WriteSingleRegisterCommand(address, value, priority, ct);
        await dispatcher.EnqueueAsync(command, ct).ConfigureAwait(false);
        await command.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteMultipleRegistersAsync(
        ushort address,
        ushort[] values,
        CommandPriority priority = CommandPriority.High,
        CancellationToken ct = default)
    {
        ThrowIfDisposedOrDisposing();
        ThrowIfNotStarted();
        var command = new WriteMultipleRegistersCommand(address, values, priority, ct);
        await dispatcher.EnqueueAsync(command, ct).ConfigureAwait(false);
        await command.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            if (_disposed || _disposing)
            {
                return;
            }

            _disposing = true;
        }

        try
        {
            await dispatcher.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            // Не диспоузим _startLock - SemaphoreSlim будет собран GC
            // Диспоуз вызывает гонку с WaitAsync в других потоках
            lock (_lock)
            {
                _disposed = true;
                _disposing = false;
            }
        }
    }

    /// <summary>
    /// Выбрасывает исключение если объект disposed или disposing.
    /// </summary>
    private void ThrowIfDisposedOrDisposing()
    {
        lock (_lock)
        {
            if (_disposed || _disposing)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }

    /// <summary>
    /// Выбрасывает исключение если диспетчер не запущен.
    /// </summary>
    private void ThrowIfNotStarted()
    {
        if (!dispatcher.IsStarted)
        {
            throw new InvalidOperationException("Диспетчер не запущен. Вызовите StartAsync() перед выполнением операций.");
        }
    }
}
