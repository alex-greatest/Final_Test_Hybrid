## ADDED Requirements

### Requirement: Map Transition Depends On Execution-Idle
The system SHALL transition to the next map only when all column executors are execution-idle, independent of UI visibility.

#### Scenario: UI still visible but executors idle
- **WHEN** all column executors report execution-idle
- **AND** UI status for one or more columns remains visible
- **THEN** the next map MAY start without waiting on UI visibility

### Requirement: Final Error Resolution Barrier
A column executor SHALL not complete a map while it remains in a failed state after the final step; it SHALL wait for error resolution before becoming execution-idle.

#### Scenario: Last step fails then Retry succeeds
- **WHEN** the last step of a map fails
- **AND** the operator selects Retry
- **THEN** the column waits for retry completion before becoming execution-idle
- **AND** the next map start is not blocked by stale UI status

### Requirement: Skip Resolution Ordering
On Skip resolution, the system SHALL dequeue the error before clearing failed state to avoid race conditions with the error queue.

#### Scenario: Skip clears error without losing queued errors
- **WHEN** Skip is selected for a failed step
- **THEN** the current error is dequeued before the executor clears failed state
- **AND** subsequent queued errors remain pending

### Requirement: Waitpoint Observability
All execution waitpoints SHALL emit structured start/end log events with correlation identifiers.

#### Scenario: Waitpoint logs include correlation ids
- **WHEN** the system enters and exits a waitpoint
- **THEN** logs include TestRunId, MapIndex, MapRunId, ColumnIndex, UiStepId, StepName, and Waitpoint name

### Requirement: Non-Recursive Map Access Wait
Map access waiting SHALL be implemented as a bounded loop and MUST be cancellable.

#### Scenario: Map gate changes while waiting
- **WHEN** a column waits for map access and the active map changes
- **THEN** the wait continues in a loop without recursion
- **AND** cancellation stops the wait promptly

### Requirement: Bounded Completion End Reset Wait
Waiting for PLC End reset after test completion SHALL be bounded by a timeout and a defined policy.

#### Scenario: End reset timeout
- **WHEN** PLC End does not reset within the configured timeout
- **THEN** the system applies the configured timeout policy (pause or stop)
- **AND** logs include current PLC tag snapshot
