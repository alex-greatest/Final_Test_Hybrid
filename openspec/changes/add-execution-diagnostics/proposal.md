# Change: Add Execution Diagnostics Logging (Phase 0)

## Why
We have deterministic hangs and race conditions that are hard to reproduce. We need correlated diagnostics to reconstruct the full execution chain from a single log without changing behavior.

## What Changes
- Add correlation identifiers to execution/error logs (TestRunId, MapIndex, MapRunId, ColumnIndex, UiStepId, StepName, PlcBlockPath when available).
- Log gate state changes with reasons (_continueGate, _mapGate).
- Log start/end of key waitpoints (idle between maps, skip-reset, ask-repeat reset, completion End reset).

## Impact
- Affected specs: error-coordinator
- Affected code: TestExecutionCoordinator, ColumnExecutor, ErrorCoordinator, TestCompletionCoordinator
- Affected docs: none
