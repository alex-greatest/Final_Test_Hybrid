using NModbus;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Базовый класс для команд Modbus с поддержкой TaskCompletionSource.
/// </summary>
/// <typeparam name="T">Тип результата команды.</typeparam>
public abstract class ModbusCommandBase<T> : IModbusCommand
{
    private readonly TaskCompletionSource<T> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Инициализирует команду с указанным приоритетом и токеном отмены.
    /// </summary>
    protected ModbusCommandBase(CommandPriority priority, CancellationToken ct)
    {
        Priority = priority;
        CancellationToken = ct;
    }

    /// <summary>
    /// Task для ожидания результата выполнения команды.
    /// </summary>
    public Task<T> Task => _tcs.Task;

    /// <inheritdoc />
    public CommandPriority Priority { get; }

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Реализует выполнение конкретной Modbus операции.
    /// </summary>
    protected abstract Task<T> ExecuteCoreAsync(IModbusMaster master, byte slaveId, CancellationToken ct);

    /// <inheritdoc />
    async Task IModbusCommand.ExecuteAsync(IModbusMaster master, byte slaveId, CancellationToken ct)
    {
        try
        {
            var result = await ExecuteCoreAsync(master, slaveId, ct).ConfigureAwait(false);
            _tcs.TrySetResult(result);
        }
        catch (OperationCanceledException)
        {
            _tcs.TrySetCanceled(ct);
        }
        catch (Exception ex)
        {
            _tcs.TrySetException(ex);
        }
    }

    /// <inheritdoc />
    public void SetException(Exception ex) => _tcs.TrySetException(ex);

    /// <inheritdoc />
    public void SetCanceled() => _tcs.TrySetCanceled();
}

/// <summary>
/// Базовый класс для команд Modbus без возвращаемого значения.
/// </summary>
public abstract class ModbusCommandBase : IModbusCommand
{
    private readonly TaskCompletionSource _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Инициализирует команду с указанным приоритетом и токеном отмены.
    /// </summary>
    protected ModbusCommandBase(CommandPriority priority, CancellationToken ct)
    {
        Priority = priority;
        CancellationToken = ct;
    }

    /// <summary>
    /// Task для ожидания завершения команды.
    /// </summary>
    public Task Task => _tcs.Task;

    /// <inheritdoc />
    public CommandPriority Priority { get; }

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Реализует выполнение конкретной Modbus операции.
    /// </summary>
    protected abstract Task ExecuteCoreAsync(IModbusMaster master, byte slaveId, CancellationToken ct);

    /// <inheritdoc />
    async Task IModbusCommand.ExecuteAsync(IModbusMaster master, byte slaveId, CancellationToken ct)
    {
        try
        {
            await ExecuteCoreAsync(master, slaveId, ct).ConfigureAwait(false);
            _tcs.TrySetResult();
        }
        catch (OperationCanceledException)
        {
            _tcs.TrySetCanceled(ct);
        }
        catch (Exception ex)
        {
            _tcs.TrySetException(ex);
        }
    }

    /// <inheritdoc />
    public void SetException(Exception ex) => _tcs.TrySetException(ex);

    /// <inheritdoc />
    public void SetCanceled() => _tcs.TrySetCanceled();
}
