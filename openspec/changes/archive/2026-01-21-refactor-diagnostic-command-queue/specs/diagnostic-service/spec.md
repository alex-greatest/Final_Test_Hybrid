## ADDED Requirements

### Requirement: Command Queue Architecture

The Diagnostic Service SHALL use a command queue pattern for all Modbus operations where:
- All read/write operations are encapsulated as commands
- Commands flow through a priority-based dispatcher
- A single worker thread owns the SerialPort/ModbusMaster connection

#### Scenario: One-off read has priority over polling
- **WHEN** a one-off read command is enqueued during active polling
- **THEN** the one-off command executes before pending polling commands

#### Scenario: Polling continues after one-off completes
- **WHEN** a one-off command completes
- **THEN** polling commands resume processing from the low-priority queue

### Requirement: Command Priority Levels

The system SHALL support two command priority levels:
- **High**: For user-initiated operations (one-off reads/writes)
- **Low**: For background operations (polling)

#### Scenario: High priority queue drains first
- **WHEN** both high and low priority commands are pending
- **THEN** all high priority commands execute before any low priority command

#### Scenario: Low priority processes when high is empty
- **WHEN** the high priority queue is empty
- **THEN** the dispatcher processes one low priority command

### Requirement: Single Connection Owner

The ModbusDispatcher worker SHALL be the only component that:
- Opens and closes the SerialPort
- Holds the IModbusMaster reference
- Executes Modbus operations

#### Scenario: Connection isolation
- **WHEN** multiple services request Modbus operations
- **THEN** all operations execute through the single dispatcher worker

#### Scenario: Clean disconnect
- **WHEN** the dispatcher is stopped
- **THEN** it completes pending commands and closes the connection

### Requirement: Automatic Reconnection

The ModbusDispatcher SHALL automatically reconnect on communication errors:
- Close failed connection
- Wait with exponential backoff
- Reopen connection
- Resume command processing

#### Scenario: Reconnect on timeout
- **WHEN** a Modbus operation times out
- **THEN** the dispatcher closes and reopens the connection

#### Scenario: Commands wait during reconnect
- **WHEN** the connection is being reestablished
- **THEN** pending commands wait in queue until connection is restored

### Requirement: Polling Coalescing

The polling system SHALL implement coalescing to prevent queue overflow:
- Skip tick if previous poll command is still pending
- Log skipped ticks for diagnostics

#### Scenario: Skip tick when pending
- **WHEN** a polling tick fires while previous poll is pending
- **THEN** the tick is skipped without enqueuing a new command

#### Scenario: Normal tick when idle
- **WHEN** a polling tick fires with no pending poll
- **THEN** a new poll command is enqueued

### Requirement: Graceful Shutdown

The ModbusDispatcher SHALL support graceful shutdown:
- Stop accepting new commands
- Complete or cancel pending commands
- Close connection cleanly

#### Scenario: Shutdown with pending commands
- **WHEN** shutdown is requested with commands pending
- **THEN** pending commands receive cancellation
- **AND** the connection closes after all commands complete or cancel

## ADDED Requirements

### Requirement: IModbusClient Interface

All Modbus operations SHALL go through the `IModbusClient` interface:
- `ReadHoldingRegistersAsync` with optional priority
- `WriteSingleRegisterAsync` with optional priority
- `WriteMultipleRegistersAsync` with optional priority

#### Scenario: Default high priority
- **WHEN** a method is called without specifying priority
- **THEN** it uses `CommandPriority.High`

#### Scenario: Explicit low priority for polling
- **WHEN** polling service reads registers
- **THEN** it specifies `CommandPriority.Low`

### Requirement: Feature Flag Migration

The system SHALL support gradual migration via config flag:
- `Diagnostic:UseCommandQueue = false`: Legacy ModbusClient
- `Diagnostic:UseCommandQueue = true`: QueuedModbusClient

#### Scenario: Legacy mode default
- **WHEN** config flag is not set
- **THEN** legacy ModbusClient is used

#### Scenario: Queue mode enabled
- **WHEN** `UseCommandQueue = true`
- **THEN** QueuedModbusClient routes through dispatcher
