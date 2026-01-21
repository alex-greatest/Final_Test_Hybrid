# Design: Command Queue Architecture for Diagnostic Service

## Context

The current Modbus communication architecture uses a `PollingPauseCoordinator` to synchronize polling operations with one-off read/write requests. This approach has critical race conditions and deadlock scenarios that cannot be solved without architectural change.

### Current Problems

| Severity | Problem | Root Cause |
|----------|---------|------------|
| CRITICAL | Polling self-deadlock | `EnterPoll()` → ModbusClient → `PauseAsync()` waits for `_activePollCount == 0` |
| HIGH | Eternal pause on cancel | `PauseAsync` cancellation leaves `_pauseCount > 0` |
| HIGH | TOCTOU race | `WaitIfPausedAsync()` and `EnterPoll()` not atomic |
| MEDIUM | Dispose during operation | Disconnect/Reconnect not coordinated with semaphore |
| MEDIUM | No reconnect gate | Operations fail on `ModbusMaster == null` |

## Goals / Non-Goals

**Goals:**
- Eliminate deadlock and race conditions
- Provide priority-based command execution (one-off > polling)
- Single owner of SerialPort/ModbusMaster (dispatcher worker)
- Automatic reconnection without blocking callers

**Non-Goals:**
- Change Modbus protocol or data format
- Support concurrent connections to multiple devices
- Real-time guarantees (best-effort priority)

## Architecture

### Command Queue Pattern

All operations become commands that flow through a priority dispatcher:

```
[UI/Services] ──► [IModbusClient] ──► [ModbusDispatcher] ──► [SerialPort/NModbus]
                       ▲                     ▲
[PollingService] ──────┘                     │
  (Low priority)                       State events
```

### Why This Solves Problems

| Problem | Solution |
|---------|----------|
| Deadlock | Polling enqueues low-priority commands, no pause needed |
| Eternal pause | No pause/resume mechanism at all |
| TOCTOU | All operations serialized by single worker |
| Dispose during op | Only worker owns connection |
| No reconnect gate | Commands wait in queue until connection restored |

## Decisions

### Decision 1: Two Priority Channels

Use `System.Threading.Channels.Channel<T>` for queueing:
- High priority: bounded channel for one-off operations
- Low priority: bounded channel for polling

Worker always drains high queue before processing low queue.

**Rationale**: Channels are lock-free, backpressure-aware, and integrate well with async/await.

### Decision 2: Single Worker Thread

One background Task processes commands sequentially.

**Rationale**: Eliminates all synchronization concerns. SerialPort is inherently single-threaded anyway.

**Trade-off**: No parallel Modbus operations. Acceptable because serial communication is sequential.

### Decision 3: Coalescing for Polling

If a polling command is already pending when the next tick fires, skip the tick.

**Rationale**: Prevents queue backlog during slow connections or high priority traffic.

### Decision 4: Feature Flag for Migration

`Diagnostic:UseCommandQueue` config flag enables new path.

**Rationale**: Safe rollout, easy rollback, parallel development.

## Components

### CommandPriority

```csharp
public enum CommandPriority { High, Low }
```

### IModbusCommand

```csharp
public interface IModbusCommand
{
    CommandPriority Priority { get; }
    CancellationToken CancellationToken { get; }
    Task ExecuteAsync(IModbusMaster master, CancellationToken ct);
    void SetException(Exception ex);
    void SetCanceled();
}
```

### ModbusCommandBase<T>

```csharp
public abstract class ModbusCommandBase<T> : IModbusCommand
{
    private readonly TaskCompletionSource<T> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<T> Task => _tcs.Task;
    public CommandPriority Priority { get; }
    public CancellationToken CancellationToken { get; }

    protected abstract Task<T> ExecuteCoreAsync(IModbusMaster master, CancellationToken ct);

    async Task IModbusCommand.ExecuteAsync(IModbusMaster master, CancellationToken ct)
    {
        try
        {
            var result = await ExecuteCoreAsync(master, ct);
            _tcs.TrySetResult(result);
        }
        catch (OperationCanceledException)
        {
            _tcs.TrySetCanceled();
        }
        catch (Exception ex)
        {
            _tcs.TrySetException(ex);
        }
    }

    public void SetException(Exception ex) => _tcs.TrySetException(ex);
    public void SetCanceled() => _tcs.TrySetCanceled();
}
```

### ModbusDispatcher

```csharp
public class ModbusDispatcher : IModbusDispatcher, IAsyncDisposable
{
    private readonly Channel<IModbusCommand> _highQueue;
    private readonly Channel<IModbusCommand> _lowQueue;
    private readonly ModbusConnectionManager _connectionManager;
    private Task? _workerTask;
    private CancellationTokenSource? _cts;

    public event Func<Task>? Disconnecting;
    public event Action? Connected;

    public async Task EnqueueAsync(IModbusCommand command, CancellationToken ct)
    {
        var channel = command.Priority == CommandPriority.High ? _highQueue : _lowQueue;
        await channel.Writer.WriteAsync(command, ct);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _workerTask = RunWorkerLoopAsync(_cts.Token);
    }

    private async Task RunWorkerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await EnsureConnectedAsync(ct);
            await ProcessCommandsAsync(ct);
        }
    }

    private async Task ProcessCommandsAsync(CancellationToken ct)
    {
        // Always drain high priority first
        while (_highQueue.Reader.TryRead(out var cmd))
        {
            await ExecuteCommandAsync(cmd, ct);
        }

        // Then process one low priority
        if (_lowQueue.Reader.TryRead(out var lowCmd))
        {
            await ExecuteCommandAsync(lowCmd, ct);
        }
        else
        {
            // Wait for any command
            await WaitForCommandAsync(ct);
        }
    }
}
```

### IModbusClient

```csharp
public interface IModbusClient
{
    Task<ushort[]> ReadHoldingRegistersAsync(
        ushort address,
        ushort count,
        CommandPriority priority = CommandPriority.High,
        CancellationToken ct = default);

    Task WriteSingleRegisterAsync(
        ushort address,
        ushort value,
        CommandPriority priority = CommandPriority.High,
        CancellationToken ct = default);

    Task WriteMultipleRegistersAsync(
        ushort address,
        ushort[] values,
        CommandPriority priority = CommandPriority.High,
        CancellationToken ct = default);
}
```

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Queue overflow | Bounded channels with backpressure; coalescing for polling |
| Worker crash | Restart loop with exponential backoff |
| Migration complexity | Feature flag allows gradual rollout |
| Performance regression | Single-threaded, but Modbus is serial anyway |

## Migration Plan

1. Add infrastructure without connecting (Step 1)
2. Introduce interface, keep legacy implementation (Step 2-3)
3. Add priority parameter throughout API (Step 4)
4. Convert polling to use low priority (Step 5)
5. Enable feature flag in dev/test (Step 6)
6. Remove legacy code after validation (Step 7)

**Rollback**: Set `UseCommandQueue=false` to revert to legacy behavior.

## Open Questions

None - architecture validated through Codex analysis.
