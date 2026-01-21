# Tasks: Manual Start/Stop + Ping Keep-Alive + PLC Reset

## 1. Remove Auto-Start from QueuedModbusClient

- [ ] 1.1 Update XML comment — "Требует ручного запуска диспетчера"
- [ ] 1.2 Remove `_startLock` field
- [ ] 1.3 Replace `EnsureDispatcherStartedAsync()` calls with `ThrowIfNotStarted()`
- [ ] 1.4 Add `ThrowIfNotStarted()` method
- [ ] 1.5 Remove `EnsureDispatcherStartedAsync()` method

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
- [ ] 3.4 Remove `_isConnected = true` from `EnsureConnectedAsync()` after `Connect()`
- [ ] 3.5 Add `_isConnected = true` in `ExecuteCommandAsync()` on success

## 4. Add Ping Keep-Alive Task

- [ ] 4.1 Add `PingPollingTask` to `PollingService` or create dedicated service
- [ ] 4.2 Ping reads register `1055 - BaseAddressOffset` (Firmware Major)
- [ ] 4.3 Ping uses `CommandPriority.Low`
- [ ] 4.4 Ping interval configurable (default 5 seconds)
- [ ] 4.5 Start ping task on `StartAsync()`, stop on `StopAsync()`
- [ ] 4.6 Add `PingIntervalMs` to `ModbusDispatcherOptions`

## 5. PLC Reset Integration

- [ ] 5.1 Decide where to subscribe: `Form1.cs` or dedicated service
- [ ] 5.2 Subscribe to `PlcResetCoordinator.OnForceStop` → `dispatcher.StopAsync()`
- [ ] 5.3 Subscribe to `ErrorCoordinator.OnReset` → `dispatcher.StopAsync()`
- [ ] 5.4 Ensure `StopAsync()` is safe to call multiple times (idempotent)

## 6. Update Documentation

- [ ] 6.1 Update `DiagnosticGuide.md`:
  - Remove "автоматически при первом обращении"
  - Add mandatory `StartAsync()` section
  - Document new IsConnected behavior
  - Document ping keep-alive
  - Document PLC reset integration
  - Document restart capability

## 7. Verification

- [ ] 7.1 Build passes (`dotnet build`)
- [ ] 7.2 Test restart: `StartAsync()` → `StopAsync()` → `StartAsync()` — works
- [ ] 7.3 Test IsConnected: after `StartAsync()` = false, after first command = true
- [ ] 7.4 Test ping timeout: port exists, no boiler → `IsConnected` stays false → reconnect
- [ ] 7.5 Test command timeout: `IsConnected = false` → reconnect
- [ ] 7.6 Test without `StartAsync()`: read/write → `DiagnosticReadResult.Fail`
- [ ] 7.7 Test PLC soft reset → diagnostic stops
- [ ] 7.8 Test PLC hard reset → diagnostic stops
- [ ] 7.9 Test queue clearing: enqueue commands → `StopAsync()` → commands cancelled
- [ ] 7.10 Test ping keep-alive: no commands for N seconds → ping runs
