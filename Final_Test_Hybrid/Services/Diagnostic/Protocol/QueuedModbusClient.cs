using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol;

/// <summary>
/// Реализация IModbusClient через приоритетную очередь команд.
/// Все операции маршрутизируются через ModbusDispatcher.
/// Автоматически запускает диспетчер при первом обращении.
/// </summary>
public class QueuedModbusClient(IModbusDispatcher dispatcher) : IModbusClient
{
    private readonly object _lock = new();
    private readonly SemaphoreSlim _startLock = new(1, 1);
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
        await EnsureDispatcherStartedAsync(ct).ConfigureAwait(false);
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
        await EnsureDispatcherStartedAsync(ct).ConfigureAwait(false);
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
        await EnsureDispatcherStartedAsync(ct).ConfigureAwait(false);
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
    /// Гарантирует запуск диспетчера.
    /// </summary>
    private async Task EnsureDispatcherStartedAsync(CancellationToken ct)
    {
        if (dispatcher.IsStarted)
        {
            return;
        }

        // Проверяем ещё раз перед ожиданием семафора
        ThrowIfDisposedOrDisposing();

        await _startLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfDisposedOrDisposing();

            if (!dispatcher.IsStarted)
            {
                await dispatcher.StartAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _startLock.Release();
        }
    }
}
