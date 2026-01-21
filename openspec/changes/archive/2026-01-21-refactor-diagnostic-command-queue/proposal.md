# Change: Refactor Diagnostic Service to Command Queue Architecture

## Why

The current Diagnostic Service architecture has critical synchronization problems:

1. **Polling Deadlock**: `PollingTask.EnterPoll()` → `ModbusClient` → `PauseAsync()` waits for `_activePollCount == 0`, causing self-deadlock
2. **Eternal Pause**: `PauseAsync` on cancellation leaves `_pauseCount > 0` permanently paused
3. **TOCTOU Race**: `WaitIfPausedAsync()` and `EnterPoll()` are not atomic, allowing race conditions
4. **Dispose During Operation**: `Disconnect/Reconnect` not coordinated with `ModbusClient._semaphore`
5. **No Reconnect Gate**: Operations fail with `ModbusMaster == null` during reconnection

## What Changes

- **BREAKING**: Replace `PollingPauseCoordinator` with Command Queue pattern
- Add `IModbusClient` interface for abstraction
- Implement `ModbusDispatcher` with priority-based `Channel<T>` queues
- Add `QueuedModbusClient` that routes operations through dispatcher
- Simplify `PollingTask` to enqueue low-priority commands
- Move connection ownership to dispatcher (single worker thread owns SerialPort)

## Impact

- **Affected specs**: diagnostic-service (new)
- **Affected code**:
  - `Protocol/ModbusClient.cs` - wrapped by interface
  - `Protocol/RegisterReader.cs` - use `IModbusClient`
  - `Protocol/RegisterWriter.cs` - use `IModbusClient`
  - `Polling/PollingPauseCoordinator.cs` - **REMOVED**
  - `Polling/PollingTask.cs` - simplified, no pause coordination
  - `Polling/PollingService.cs` - remove coordinator dependency
  - `Connection/DiagnosticConnectionService.cs` - delegate to dispatcher
  - `DependencyInjection/DiagnosticServiceExtensions.cs` - new registrations
