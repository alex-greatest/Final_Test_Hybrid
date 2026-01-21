# Diagnostic Service Spec Delta

## ADDED Requirements

### Requirement: Manual Connection Lifecycle

The Diagnostic Service SHALL require explicit manual start via `IModbusDispatcher.StartAsync()` before any read/write operations.

The system SHALL NOT auto-start the dispatcher on first read/write operation.

The system SHALL support restart after `StopAsync()` — calling `StartAsync()` again SHALL work.

#### Scenario: Operation without StartAsync via RegisterReader
- **WHEN** `RegisterReader.ReadUInt16Async()` is called without prior `StartAsync()`
- **THEN** `QueuedModbusClient` throws `InvalidOperationException`
- **AND** `RegisterReader` catches it and returns `DiagnosticReadResult.Fail`

#### Scenario: Operation without StartAsync via direct IModbusClient
- **WHEN** `IModbusClient.ReadHoldingRegistersAsync()` is called directly without prior `StartAsync()`
- **THEN** `InvalidOperationException` is thrown to the caller

#### Scenario: Restart after stop
- **WHEN** `StopAsync()` was called previously
- **AND** `StartAsync()` is called again
- **THEN** new channels are created (empty queue)
- **AND** connection attempt begins

### Requirement: Connection State Based on Command Success

After `StartAsync()` and port opening, `IsConnected` SHALL remain `false` until the first successful Modbus command.

Any successful command (ping or user) SHALL set `IsConnected` to `true`.

Any communication error (timeout) SHALL set `IsConnected` to `false` and trigger reconnect.

#### Scenario: IsConnected false after port opens
- **WHEN** `StartAsync()` is called
- **AND** COM port opens successfully
- **THEN** `IsConnected` remains `false`
- **AND** `IsReconnecting` is `false`

#### Scenario: IsConnected true after successful command
- **WHEN** port is open and `IsConnected` is `false`
- **AND** a Modbus command (ping or user) executes successfully
- **THEN** `IsConnected` becomes `true`
- **AND** `Connected` event is raised

#### Scenario: IsConnected false after timeout
- **WHEN** `IsConnected` is `true`
- **AND** a Modbus command times out (communication error)
- **THEN** `IsConnected` becomes `false`
- **AND** reconnect loop starts with fixed 5 second interval

### Requirement: Ping Keep-Alive

The Diagnostic Service SHALL periodically send a low-priority ping command to detect connection loss when idle.

Ping SHALL read useful diagnostic data (not just connectivity check):
- **ModeKey** (addresses 1000-1001, Modbus 999-1000): uint32 indicating current access mode
- **BoilerStatus** (address 1005, Modbus 1004): int16 indicating boiler state (-1 to 10)

Ping SHALL return `DiagnosticPingData` record with extensible structure for future parameters.

Ping SHALL use `CommandPriority.Low` so user commands always execute first.

Ping interval SHALL be configurable via `PingIntervalMs` setting.

The dispatcher SHALL expose `LastPingData` property for UI to display current boiler state.

#### Scenario: Ping runs when idle
- **WHEN** no user commands are pending
- **AND** ping interval has elapsed
- **THEN** ping command is enqueued with low priority
- **AND** ping executes and verifies connection

#### Scenario: User command preempts ping
- **WHEN** ping is pending in the queue
- **AND** user sends a high-priority command
- **THEN** user command executes first
- **AND** ping executes after user command completes

#### Scenario: Ping detects connection loss
- **WHEN** ping command times out
- **THEN** `IsConnected` becomes `false`
- **AND** reconnect loop starts

### Requirement: Queue Management on Stop

When `StopAsync()` is called, the system SHALL cancel all pending commands in both High and Low priority queues.

New channels SHALL be created on next `StartAsync()` to guarantee empty queue.

#### Scenario: Commands cancelled on stop
- **WHEN** commands are pending in the queue
- **AND** `StopAsync()` is called
- **THEN** all pending commands are cancelled

#### Scenario: Clean queue on restart
- **WHEN** `StartAsync()` is called after `StopAsync()`
- **THEN** channels are recreated
- **AND** queue is guaranteed empty

### Requirement: PLC Reset Integration

The Diagnostic Service SHALL stop when PLC reset occurs.

On both Soft Reset (ForceStop) and Hard Reset, the system SHALL call `StopAsync()`.

#### Scenario: Soft reset stops diagnostic
- **WHEN** PLC soft reset occurs (`PlcResetCoordinator.OnForceStop` event)
- **THEN** `IModbusDispatcher.StopAsync()` is called
- **AND** connection is closed
- **AND** pending commands are cancelled

#### Scenario: Hard reset stops diagnostic
- **WHEN** PLC hard reset occurs (`ErrorCoordinator.OnReset` event)
- **THEN** `IModbusDispatcher.StopAsync()` is called
- **AND** connection is closed
- **AND** pending commands are cancelled

#### Scenario: StopAsync is idempotent
- **WHEN** `StopAsync()` is called multiple times
- **THEN** only first call performs cleanup
- **AND** subsequent calls return immediately without error
