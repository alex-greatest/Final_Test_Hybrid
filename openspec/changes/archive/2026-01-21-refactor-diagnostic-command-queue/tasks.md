# Tasks: Refactor Diagnostic Command Queue

## 1. Infrastructure (Step 1) ✅

- [x] 1.1 Create `Protocol/CommandQueue/CommandPriority.cs`
- [x] 1.2 Create `Protocol/CommandQueue/IModbusCommand.cs`
- [x] 1.3 Create `Protocol/CommandQueue/ModbusCommandBase.cs`
- [x] 1.4 Create `Protocol/CommandQueue/IModbusDispatcher.cs`
- [x] 1.5 Create `Protocol/CommandQueue/ModbusDispatcherOptions.cs`
- [x] 1.6 Create `Protocol/IModbusClient.cs`

**Checkpoint**: Build passes, no behavior change ✅

## 2. Interface Abstraction (Step 2) ✅

- [x] 2.1 Update `RegisterReader.cs` - inject `IModbusClient` instead of `ModbusClient`
- [x] 2.2 Update `RegisterWriter.cs` - inject `IModbusClient` instead of `ModbusClient`
- [x] 2.3 Update `ModbusClient.cs` - implement `IModbusClient`
- [x] 2.4 Update `DiagnosticServiceExtensions.cs` - register `IModbusClient` → `ModbusClient`

**Checkpoint**: All services work as before (legacy mode) ✅

## 3. Dispatcher Implementation (Step 3) ✅

- [x] 3.1 Create `Protocol/CommandQueue/ModbusConnectionManager.cs`
- [x] 3.2 Create `Protocol/CommandQueue/ModbusDispatcher.cs` (Channel-based worker)
- [x] 3.3 Create `Protocol/QueuedModbusClient.cs` (routes through dispatcher)
- [x] 3.4 Add conditional DI based on `Diagnostic:UseCommandQueue` config flag
- [x] 3.5 Create command classes: `ReadHoldingRegistersCommand`, `WriteSingleRegisterCommand`, `WriteMultipleRegistersCommand`

**Checkpoint**: Legacy mode default, new code not active ✅

## 4. Priority Support (Step 4) ✅

- [x] 4.1 Add priority parameter to `IModbusClient` methods
- [x] 4.2 Update `RegisterReader.cs` - add overloads with `CommandPriority` parameter
- [x] 4.3 Update `RegisterWriter.cs` - pass `CommandPriority.High` by default
- [x] 4.4 Update `ModbusClient.cs` - ignore priority (for compatibility)
- [x] 4.5 Add `UseCommandQueue` setting to `DiagnosticSettings.cs`

**Checkpoint**: Compiles, legacy path unchanged ✅

## 5. Polling Migration (Step 5) ✅

- [x] 5.1 Update `PollingTask.cs` - remove `WaitIfPausedAsync`
- [x] 5.2 Update `PollingTask.cs` - remove `EnterPoll/ExitPoll`
- [x] 5.3 Update `PollingTask.cs` - pass `CommandPriority.Low` to reader
- [x] 5.4 Add coalescing - skip tick if previous poll pending
- [x] 5.5 Update `PollingService.cs` - remove `PollingPauseCoordinator` dependency

**Checkpoint**: Polling works, one-off works ✅

## 6. Enable Command Queue (Step 6) ✅

- [x] 6.1 Add `UseCommandQueue` and `CommandQueue` settings to `appsettings.json`
- [x] 6.2 Update `Form1.cs` - conditional event subscription (legacy vs command queue mode)
- [x] 6.3 Add `IsStarted` property to `IModbusDispatcher`
- [x] 6.4 Add auto-start to `QueuedModbusClient` - starts dispatcher on first operation

**Critical Checkpoint**:
- [x] Read/write through queue (auto-start)
- [x] One-off commands have priority over polling (high-priority queue)
- [x] COM port disconnect triggers reconnect (dispatcher handles)
- [x] Queue doesn't grow unbounded (coalescing + bounded channels)

## 7. Cleanup (Step 7) ✅

- [x] 7.1 Delete `Polling/PollingPauseCoordinator.cs`
- [x] 7.2 Delete `Protocol/ModbusClient.cs`
- [x] 7.3 Delete `Connection/DiagnosticConnectionService.cs`
- [x] 7.4 Remove legacy DI registrations from `DiagnosticServiceExtensions.cs`
- [x] 7.5 Remove `UseCommandQueue` flag from `DiagnosticSettings.cs` and `appsettings.json`
- [x] 7.6 Simplify `Form1.cs` - remove legacy fallback

**Final Checkpoint**: Build passes ✅

## Deleted Files

| File | Description |
|------|-------------|
| `Polling/PollingPauseCoordinator.cs` | Legacy pause coordination |
| `Protocol/ModbusClient.cs` | Legacy Modbus client with pause |
| `Connection/DiagnosticConnectionService.cs` | Legacy connection management |

---

## Created Files

| File | Description |
|------|-------------|
| `Protocol/CommandQueue/CommandPriority.cs` | Enum: High, Low |
| `Protocol/CommandQueue/IModbusCommand.cs` | Command interface |
| `Protocol/CommandQueue/ModbusCommandBase.cs` | Base class with TCS |
| `Protocol/CommandQueue/IModbusDispatcher.cs` | Dispatcher interface |
| `Protocol/CommandQueue/ModbusDispatcherOptions.cs` | Dispatcher settings |
| `Protocol/CommandQueue/ModbusConnectionManager.cs` | SerialPort/NModbus owner |
| `Protocol/CommandQueue/ModbusDispatcher.cs` | Priority queue worker |
| `Protocol/CommandQueue/ReadHoldingRegistersCommand.cs` | Read command |
| `Protocol/CommandQueue/WriteSingleRegisterCommand.cs` | Write single command |
| `Protocol/CommandQueue/WriteMultipleRegistersCommand.cs` | Write multiple command |
| `Protocol/IModbusClient.cs` | Client interface |
| `Protocol/QueuedModbusClient.cs` | Queue-based client |

## Modified Files

| File | Changes |
|------|---------|
| `Protocol/ModbusClient.cs` | Implements `IModbusClient`, added priority param |
| `Protocol/RegisterReader.cs` | Uses `IModbusClient`, added priority overloads |
| `Protocol/RegisterWriter.cs` | Uses `IModbusClient` |
| `Connection/DiagnosticSettings.cs` | Added `UseCommandQueue` flag |
| `DependencyInjection/DiagnosticServiceExtensions.cs` | Conditional DI registration |
| `Polling/PollingTask.cs` | Removed pause coordinator, added coalescing, uses `CommandPriority.Low` |
| `Polling/PollingService.cs` | Removed `PollingPauseCoordinator` dependency |
| `Protocol/CommandQueue/IModbusDispatcher.cs` | Added `IsStarted` property |
| `Protocol/CommandQueue/ModbusDispatcher.cs` | Implemented `IsStarted` property |
| `Protocol/QueuedModbusClient.cs` | Added auto-start on first operation |
| `Form1.cs` | Conditional event subscription for both modes |
| `appsettings.json` | Added `UseCommandQueue` and `CommandQueue` section |
