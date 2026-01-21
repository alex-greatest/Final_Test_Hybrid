# Diagnostic Service Spec Delta

## ADDED Requirements

### Requirement: Manual Connection Start

The Diagnostic Service SHALL require explicit manual start via `IModbusDispatcher.StartAsync()` before any read/write operations can be performed.

The system SHALL NOT auto-start the dispatcher on first read/write operation.

If a read/write operation is attempted before `StartAsync()` is called, `QueuedModbusClient` SHALL throw `InvalidOperationException` with a descriptive message before the operation reaches `RegisterReader`.

#### Scenario: Operation without StartAsync throws immediately
- **WHEN** `IModbusClient.ReadHoldingRegistersAsync()` is called without prior `StartAsync()`
- **THEN** `InvalidOperationException` is thrown with message "Диспетчер не запущен. Вызовите IModbusDispatcher.StartAsync() перед использованием."
- **AND** the exception propagates to the caller (not caught by RegisterReader)

#### Scenario: Manual start success
- **WHEN** `IModbusDispatcher.StartAsync()` is called
- **AND** the COM port exists and the boiler responds to ping
- **THEN** `IsConnected` becomes `true`
- **AND** `Connected` event is raised

#### Scenario: StartAsync after StopAsync is forbidden
- **WHEN** `IModbusDispatcher.StopAsync()` was called previously
- **AND** `IModbusDispatcher.StartAsync()` is called again
- **THEN** `InvalidOperationException` is thrown with message "Рестарт диспетчера после остановки не поддерживается"

### Requirement: Connection Health Check (Ping)

After opening the COM port, the Diagnostic Service SHALL verify that the boiler device responds by reading the Firmware Major register.

The ping register address SHALL be calculated as `1055 - BaseAddressOffset` to account for different Modbus address configurations.

If the device does not respond within the configured timeout, the system SHALL treat this as a connection failure and enter the reconnect loop with exponential backoff.

#### Scenario: Ping success
- **WHEN** COM port is opened successfully
- **AND** boiler responds to ping (Firmware Major register read succeeds)
- **THEN** `IsConnected` becomes `true`
- **AND** `Connected` event is raised
- **AND** log message "Ping успешен: устройство отвечает" is written

#### Scenario: Ping timeout (port exists, boiler not connected)
- **WHEN** COM port is opened successfully
- **AND** boiler does not respond to ping (TimeoutException)
- **THEN** `IsConnected` remains `false`
- **AND** `IsReconnecting` becomes `true`
- **AND** reconnect loop continues with exponential backoff (1s → 2s → 4s → ... → 30s max)

#### Scenario: Port does not exist
- **WHEN** COM port cannot be opened (e.g., device not present)
- **THEN** `IsConnected` remains `false`
- **AND** `IsReconnecting` becomes `true`
- **AND** reconnect loop continues with exponential backoff
