## ADDED Requirements
### Requirement: Correlated Diagnostics Logging
The system SHALL emit diagnostic logs with correlation identifiers for execution and error-resolution waitpoints.

#### Scenario: Waitpoint logging
- **WHEN** a waitpoint begins or ends (idle-between-maps, skip-reset, ask-repeat reset, completion End reset)
- **THEN** the log entry includes correlation identifiers and the waitpoint name
- **AND** the end log includes the outcome or reason for completion
