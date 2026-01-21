# Tasks: Manual Start + Ping Check

## 1. Remove Auto-Start from QueuedModbusClient

- [ ] 1.1 Update XML comment — change "Автоматически запускает" to "Требует ручного запуска"
- [ ] 1.2 Remove `_startLock` field (no longer needed)
- [ ] 1.3 Replace `EnsureDispatcherStartedAsync()` calls with `ThrowIfNotStarted()`
- [ ] 1.4 Add `ThrowIfNotStarted()` method that throws `InvalidOperationException` if not started
- [ ] 1.5 Remove `EnsureDispatcherStartedAsync()` method

## 2. Add Ping Check to ModbusDispatcher

- [ ] 2.1 Inject `IOptions<DiagnosticSettings>` into `ModbusDispatcher` for BaseAddressOffset access
- [ ] 2.2 Add constant `PingRegisterBase = 1055` (Firmware Major from documentation)
- [ ] 2.3 Add `PingDeviceAsync()` private method — calculates address as `PingRegisterBase - settings.BaseAddressOffset`
- [ ] 2.4 Call `PingDeviceAsync()` in `EnsureConnectedAsync()` after `_connectionManager.Connect()`
- [ ] 2.5 If ping throws (timeout) — let it propagate to trigger reconnect loop

## 3. Update Documentation

- [ ] 3.1 Update `DiagnosticGuide.md` — remove "автоматически при первом обращении"
- [ ] 3.2 Add section about mandatory `StartAsync()` call before usage
- [ ] 3.3 Document ping check behavior and address calculation

## 4. Verification

- [ ] 4.1 Build passes (`dotnet build`)
- [ ] 4.2 Test without boiler: `StartAsync()` → ping timeout → `IsReconnecting=true`
- [ ] 4.3 Test with boiler: `StartAsync()` → ping OK → `IsConnected=true`
- [ ] 4.4 Test without `StartAsync()`: any read/write → `InvalidOperationException`
- [ ] 4.5 Test `StartAsync()` after `StopAsync()`: → `InvalidOperationException`
