## MODIFIED Requirements

### Requirement: ExecutionFlowState uses flags enum

ExecutionFlowState SHALL use a single `[Flags] enum ExecutionStopFlags` instead of separate `ExecutionStopReason` enum and `_stopAsFailure` bool fields.

The `ExecutionStopFlags` enum SHALL include:
- `None` (0) - no stop requested
- `Failure` (1) - stop is a failure (OR-semantics)
- `Operator` (2) - stop by operator request
- `AutoModeDisabled` (4) - stop due to AutoMode disabled
- `PlcForceStop` (8) - stop forced by PLC
- `PlcSoftReset` (16) - soft reset from PLC
- `PlcHardReset` (32) - hard reset from PLC

#### Scenario: First reason is preserved

- **WHEN** `RequestStop(Operator)` is called
- **AND** `RequestStop(PlcForceStop | Failure)` is called subsequently
- **THEN** reason bits SHALL be `Operator` (first reason preserved)
- **AND** `Failure` flag SHALL be set (ORed)

#### Scenario: Failure is ORed across calls

- **WHEN** `RequestStop(Operator | Failure)` is called
- **AND** `RequestStop(PlcForceStop)` is called subsequently
- **THEN** reason bits SHALL be `Operator` (first reason preserved)
- **AND** `Failure` flag SHALL remain set (ORed from first call)

#### Scenario: Multiple non-failure calls preserve first reason

- **WHEN** `RequestStop(Operator)` is called
- **AND** `RequestStop(PlcForceStop)` is called subsequently
- **THEN** reason bits SHALL be `Operator`
- **AND** `Failure` flag SHALL NOT be set

#### Scenario: ClearStop resets all flags

- **WHEN** `ClearStop()` is called
- **THEN** flags SHALL be `None`
- **AND** `IsStopRequested` SHALL return false

## ADDED Requirements

### Requirement: Coordinators use SystemLifecycleManager transitions

All coordinators SHALL call `SystemLifecycleManager.Transition(SystemTrigger)` at appropriate lifecycle points.

The existing `SystemPhase` enum defines phases: `Idle`, `WaitingForBarcode`, `Preparing`, `Testing`, `Completed`, `Resetting`.

The existing `SystemTrigger` enum defines transitions: `ScanModeEnabled`, `ScanModeDisabled`, `BarcodeReceived`, `PreparationCompleted`, `PreparationFailed`, `TestFinished`, `RepeatRequested`, `TestAcknowledged`, `ResetRequestedSoft`, `ResetRequestedHard`, `ResetCompletedSoft`, `ResetCompletedHard`.

#### Scenario: ScanModeController activates scan mode

- **WHEN** `ScanModeController.PerformInitialActivation()` is called
- **THEN** `Transition(ScanModeEnabled)` SHALL be called
- **AND** Phase SHALL become `WaitingForBarcode`

#### Scenario: ScanModeController deactivates scan mode

- **WHEN** `ScanModeController.PerformFullDeactivation()` is called
- **THEN** `Transition(ScanModeDisabled)` SHALL be called
- **AND** Phase SHALL become `Idle`

#### Scenario: PreExecutionCoordinator receives barcode

- **WHEN** `PreExecutionCoordinator.SubmitBarcode(barcode)` is called
- **THEN** `Transition(BarcodeReceived, barcode)` SHALL be called
- **AND** Phase SHALL become `Preparing`
- **AND** `CurrentBarcode` SHALL be set to barcode

#### Scenario: PreExecutionCoordinator completes preparation

- **WHEN** `StartTestExecution()` returns true
- **THEN** `Transition(PreparationCompleted)` SHALL be called
- **AND** Phase SHALL become `Testing`

#### Scenario: PreExecutionCoordinator fails preparation

- **WHEN** pipeline fails during preparation
- **THEN** `Transition(PreparationFailed)` SHALL be called
- **AND** Phase SHALL become `WaitingForBarcode`
- **AND** `CurrentBarcode` SHALL be preserved

#### Scenario: TestExecutionCoordinator completes test

- **WHEN** `HandleTestCompleted()` is called
- **THEN** `Transition(TestFinished)` SHALL be called
- **AND** Phase SHALL become `Completed`

#### Scenario: Soft reset preserves barcode

- **WHEN** PLC reset occurs with `wasInScanPhase = true`
- **THEN** `Transition(ResetRequestedSoft)` SHALL be called
- **AND** after reset completion `Transition(ResetCompletedSoft)` SHALL be called
- **AND** Phase SHALL become `WaitingForBarcode`
- **AND** `CurrentBarcode` SHALL be preserved

#### Scenario: Hard reset clears barcode

- **WHEN** PLC reset occurs with `wasInScanPhase = false`
- **THEN** `Transition(ResetRequestedHard)` SHALL be called
- **AND** after reset completion `Transition(ResetCompletedHard)` SHALL be called
- **AND** Phase SHALL become `Idle`
- **AND** `CurrentBarcode` SHALL be cleared

### Requirement: ScanModeController uses SystemLifecycleManager for state

ScanModeController SHALL NOT maintain separate boolean flags for activation and reset state.
Instead, it SHALL query `SystemLifecycleManager.Phase`.

#### Scenario: Activation state derived from lifecycle

- **WHEN** checking if scan mode is activated
- **THEN** ScanModeController SHALL check `Phase != SystemPhase.Idle`
- **AND** SHALL NOT use a separate `_isActivated` field

#### Scenario: Reset state derived from lifecycle

- **WHEN** checking if reset is in progress
- **THEN** ScanModeController SHALL check `Phase == SystemPhase.Resetting`
- **AND** SHALL NOT use a separate `_isResetting` field

#### Scenario: Scanning phase derived from lifecycle

- **WHEN** checking `IsInScanningPhase`
- **THEN** ScanModeController SHALL check `Phase == SystemPhase.WaitingForBarcode`
