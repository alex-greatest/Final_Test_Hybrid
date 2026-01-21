# Tasks: Manual Start/Stop + Ping Keep-Alive + PLC Reset

## 1. Remove Auto-Start from QueuedModbusClient

- [ ] 1.1 Update XML comment — "Требует ручного запуска диспетчера"
- [ ] 1.2 Remove `_startLock` field
- [ ] 1.3 Replace `EnsureDispatcherStartedAsync()` calls with `ThrowIfNotStarted()`
- [ ] 1.4 Add `ThrowIfNotStarted()` method
- [ ] 1.5 Remove `EnsureDispatcherStartedAsync()` method
- [ ] 1.6 Update `RegisterReader` to catch `InvalidOperationException` and return `DiagnosticReadResult.Fail`
- [ ] 1.7 Update `RegisterWriter` to catch `InvalidOperationException` and return `DiagnosticWriteResult.Fail`

## 2. Enable Restart in ModbusDispatcher

- [ ] 2.1 Remove `_wasStopped` flag — allow restart
- [ ] 2.2 Make channels nullable instance fields (not readonly)
- [ ] 2.3 Add `RecreateChannels()` private method — creates new bounded channels
- [ ] 2.4 Call `RecreateChannels()` in `StartAsync()` only when starting from stopped state
- [ ] 2.5 Keep `TryComplete()` in `StopAsync()` to prevent enqueue hang
- [ ] 2.6 Add `_isStopped` flag, set in `StopAsync()`, clear in `StartAsync()`
- [ ] 2.7 In `EnqueueAsync()`: throw if `_isStopped`

## 3. Change IsConnected Logic

- [ ] 3.1 `IsConnected = false` after port opens (not true)
- [ ] 3.2 `IsConnected = true` only after first successful command
- [ ] 3.3 Any timeout (communication error) → `IsConnected = false` → reconnect
- [ ] 3.9 **Simplify reconnect**: replace exponential backoff with fixed 5 sec interval (remove InitialReconnectDelayMs, MaxReconnectDelayMs, ReconnectBackoffMultiplier from options)
- [ ] 3.4 Remove `_isConnected = true` from `EnsureConnectedAsync()` after `Connect()`
- [ ] 3.5 Add `_isConnected = true` in `ExecuteCommandAsync()` on success
- [ ] 3.6 Move `Connected?.Invoke()` from `EnsureConnectedAsync()` to first successful command
- [ ] 3.7 **Decouple command processing from IsConnected**: `ProcessCommandsLoopAsync` should run when port is open, not when `_isConnected`
- [ ] 3.8 Add `_isPortOpen` flag or use `_connectionManager.IsConnected` for loop guard

## 4. Error Propagation for Reconnect

- [ ] 4.1 **Commands must propagate errors to dispatcher**: currently `ModbusCommandBase` sets exception on TCS but dispatcher catches and continues
- [ ] 4.2 In `ExecuteCommandAsync`: if communication error → rethrow to exit `ProcessCommandsLoopAsync`
- [ ] 4.3 Ensure `IsCommunicationError()` catches TimeoutException, IOException

## 5. Add Ping Keep-Alive Task

- [ ] 5.1 **Don't use PollingService** — it drops failures. Implement ping directly in dispatcher
- [ ] 5.2 Add `PingIntervalMs` to `ModbusDispatcherOptions` (default 5000ms)
- [ ] 5.3 Create `DiagnosticPingData` record: `ModeKey` (uint), `BoilerStatus` (short) — расширяемый
- [ ] 5.4 Create `PingCommand : ModbusCommandBase<DiagnosticPingData>` that reads 6 registers (999-1004)
- [ ] 5.5 Ping uses `CommandPriority.Low`
- [ ] 5.6 Dispatcher enqueues ping periodically when idle (PeriodicTimer in separate task)
- [ ] 5.7 Ping failure → caught by dispatcher → `IsConnected = false` → reconnect
- [ ] 5.8 Start ping task in `StartAsync()`, stop in `StopAsync()`
- [ ] 5.9 Expose `LastPingData` property on dispatcher for UI to read

## 6. PLC Reset Integration + Explicit StartAsync

- [ ] 6.1 In `Form1.cs`: call `dispatcher.StartAsync()` during initialization (after DI setup)
- [ ] 6.2 Subscribe to `PlcResetCoordinator.OnForceStop` → `dispatcher.StopAsync()`
- [ ] 6.3 Subscribe to `ErrorCoordinator.OnReset` → `dispatcher.StopAsync()`
- [ ] 6.4 Ensure `StopAsync()` is safe to call multiple times (idempotent)

## 7. Update Documentation

- [ ] 7.1 Update `DiagnosticGuide.md`:
  - Remove "автоматически при первом обращении"
  - Add mandatory `StartAsync()` section
  - Document new IsConnected behavior
  - Document ping keep-alive
  - Document PLC reset integration
  - Document restart capability

## 8. Verification

- [ ] 8.1 Build passes (`dotnet build`)
- [ ] 8.2 Test restart: `StartAsync()` → `StopAsync()` → `StartAsync()` — works
- [ ] 8.3 Test IsConnected: after `StartAsync()` = false, after first command = true
- [ ] 8.4 Test IsReconnecting: after `StartAsync()` and port opens = false
- [ ] 8.5 Test ping timeout: port exists, no boiler → `IsConnected` stays false → reconnect
- [ ] 8.6 Test command timeout: `IsConnected = false` → reconnect
- [ ] 8.7 Test without `StartAsync()` via RegisterReader: read → `DiagnosticReadResult.Fail`
- [ ] 8.8 Test without `StartAsync()` via direct IModbusClient: read → `InvalidOperationException`
- [ ] 8.9 Test PLC soft reset → diagnostic stops
- [ ] 8.10 Test PLC hard reset → diagnostic stops
- [ ] 8.11 Test queue clearing: enqueue commands → `StopAsync()` → commands cancelled
- [ ] 8.12 Test ping keep-alive: no commands for N seconds → ping runs
