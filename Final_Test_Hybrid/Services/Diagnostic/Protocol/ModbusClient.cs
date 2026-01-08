using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Polling;
using Microsoft.Extensions.Logging;
using NModbus;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol;

/// <summary>
/// Async обёртка над NModbus4 для работы с ModBus RTU.
/// Thread-safe: все операции синхронизированы через SemaphoreSlim.
/// При разовых операциях автоматически приостанавливает polling.
/// </summary>
public class ModbusClient(
    DiagnosticConnectionService connectionService,
    PollingPauseCoordinator pauseCoordinator,
    ILogger<ModbusClient> logger)
    : IAsyncDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    #region Public API

    /// <summary>
    /// Читает holding регистры (функция 0x03).
    /// Приостанавливает polling на время выполнения.
    /// </summary>
    public Task<ushort[]> ReadHoldingRegistersAsync(ushort address, ushort count, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteWithPausedPollingAsync(
            innerCt => ReadHoldingRegistersCoreAsync(address, count, innerCt),
            ct);
    }

    /// <summary>
    /// Записывает один регистр (функция 0x06).
    /// Приостанавливает polling на время выполнения.
    /// </summary>
    public Task WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteWithPausedPollingAsync(
            innerCt => WriteSingleRegisterCoreAsync(address, value, innerCt),
            ct);
    }

    /// <summary>
    /// Записывает несколько регистров (функция 0x10).
    /// Приостанавливает polling на время выполнения.
    /// </summary>
    public Task WriteMultipleRegistersAsync(ushort address, ushort[] values, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteWithPausedPollingAsync(
            innerCt => WriteMultipleRegistersCoreAsync(address, values, innerCt),
            ct);
    }

    #endregion

    #region Core Operations

    private Task<ushort[]> ReadHoldingRegistersCoreAsync(ushort address, ushort count, CancellationToken ct)
    {
        return ExecuteModbusOperationAsync(
            "ReadHoldingRegisters",
            address,
            ct,
            master => master.ReadHoldingRegisters(connectionService.SlaveId, address, count));
    }

    private Task WriteSingleRegisterCoreAsync(ushort address, ushort value, CancellationToken ct)
    {
        return ExecuteModbusOperationAsync(
            "WriteSingleRegister",
            address,
            ct,
            master => master.WriteSingleRegister(connectionService.SlaveId, address, value));
    }

    private Task WriteMultipleRegistersCoreAsync(ushort address, ushort[] values, CancellationToken ct)
    {
        return ExecuteModbusOperationAsync(
            "WriteMultipleRegisters",
            address,
            ct,
            master => master.WriteMultipleRegisters(connectionService.SlaveId, address, values));
    }

    #endregion

    #region Modbus Execution

    private async Task<T> ExecuteModbusOperationAsync<T>(
        string operationName,
        ushort address,
        CancellationToken ct,
        Func<IModbusMaster, T> operation)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var master = GetModbusMasterOrThrow();
            return await RunOnThreadPoolAsync(ct, () => operation(master)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HandleCommunicationError(ex, operationName, address);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ExecuteModbusOperationAsync(
        string operationName,
        ushort address,
        CancellationToken ct,
        Action<IModbusMaster> operation)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var master = GetModbusMasterOrThrow();
            await RunOnThreadPoolAsync(ct, () => operation(master)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HandleCommunicationError(ex, operationName, address);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private IModbusMaster GetModbusMasterOrThrow()
    {
        return connectionService.ModbusMaster
            ?? throw new InvalidOperationException("Нет подключения к ЭБУ котла");
    }

    private static async Task<T> RunOnThreadPoolAsync<T>(CancellationToken ct, Func<T> operation)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            return operation();
        }, ct).ConfigureAwait(false);
    }

    private static async Task RunOnThreadPoolAsync(CancellationToken ct, Action operation)
    {
        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            operation();
        }, ct).ConfigureAwait(false);
    }

    #endregion

    #region Polling Pause

    private async Task<T> ExecuteWithPausedPollingAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct)
    {
        await pauseCoordinator.PauseAsync(ct).ConfigureAwait(false);
        try
        {
            return await operation(ct).ConfigureAwait(false);
        }
        finally
        {
            pauseCoordinator.Resume();
        }
    }

    private async Task ExecuteWithPausedPollingAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken ct)
    {
        await pauseCoordinator.PauseAsync(ct).ConfigureAwait(false);
        try
        {
            await operation(ct).ConfigureAwait(false);
        }
        finally
        {
            pauseCoordinator.Resume();
        }
    }

    #endregion

    #region Error Handling

    private void HandleCommunicationError(Exception ex, string operation, ushort address)
    {
        LogCommunicationError(ex, operation, address);
        TriggerReconnectIfNeeded(ex);
    }

    private void LogCommunicationError(Exception ex, string operation, ushort address)
    {
        logger.LogError(ex, "Ошибка ModBus {Operation} по адресу {Address}", operation, address);
    }

    private void TriggerReconnectIfNeeded(Exception ex)
    {
        if (IsCommunicationLost(ex))
        {
            connectionService.StartReconnect();
        }
    }

    private static bool IsCommunicationLost(Exception ex)
    {
        return IsTimeoutError(ex)
            || IsIoError(ex)
            || IsPortError(ex);
    }

    private static bool IsTimeoutError(Exception ex)
    {
        return ex is TimeoutException;
    }

    private static bool IsIoError(Exception ex)
    {
        return ex is System.IO.IOException;
    }

    private static bool IsPortError(Exception ex)
    {
        return ex.Message.Contains("port", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Dispose

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _semaphore.Dispose();
        return ValueTask.CompletedTask;
    }

    #endregion
}
