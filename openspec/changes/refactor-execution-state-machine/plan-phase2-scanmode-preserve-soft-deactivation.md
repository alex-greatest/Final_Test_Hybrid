# Plan: Phase 2 (ScanModeController) - Preserve Soft Deactivation Semantics

Context: This project is safety-critical (SCADA test system). The goal is to simplify the state machine and reduce coupling in:
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanSessionManager.cs`

While integrating with the existing `SystemLifecycleManager` foundation:
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Lifecycle/SystemLifecycleManager.cs`

Hard requirement: **NO behavioral regression**.

## Problem Statement (Phase 2 scope)

`ScanModeController` currently:
- merges concerns (scan-mode enablement, scanner session lifecycle, timing control, starting/stopping main loop, UI grid step presence)
- uses internal flags (`_isActivated`, `_isResetting`) as a local state machine
- depends on multiple external sources (`OperatorState`, `AutoReadySubscription`, `PlcResetCoordinator`, `ExecutionActivityTracker`, `PreExecutionCoordinator`)

This increases coupling and makes it hard to reason about changes safely.

## Key Behavioral Invariants (must preserve)

The following behaviors are considered "contract" for Phase 2:

1) Soft deactivation MUST remain soft
- Trigger condition: scan-mode becomes disabled *but* the system still has reasons to keep execution context alive
  (currently `ShouldUseSoftDeactivation()`).
- Effects:
  - Scanner session is released (no raw input takeover).
  - Timing is paused (`PauseAllColumnsTiming()`).
  - **Main loop is NOT cancelled**.
  - Activation context is preserved (today: `_isActivated == true`).

2) Re-enable after soft deactivation MUST be lightweight
- Effects:
  - Acquire scanner session again.
  - Resume timing.
  - **Do not** repeat "initial activation" side effects (no duplicate scan step creation, no duplicate main loop start).

3) Full deactivation MUST remain full
- Effects:
  - Main loop cancelled.
  - Activation context cleared (today: `_isActivated = false`).
  - Scanner session released.
  - UI grid clearing stays as-is (only when operator is not authenticated).

4) PLC reset semantics MUST remain identical
- `PlcResetCoordinator.OnResetStarting` expects `wasInScanPhase` with the same meaning as today:
  `IsInScanningPhase = _isActivated && !_isResetting`.
- Side effects remain the same:
  - pause timing, release scanner session at reset start
  - restore "ready" state at reset completion based on current enablement

5) Public surface MUST remain stable in Phase 2
- `IsScanModeEnabled`, `IsInScanningPhase`, `OnStateChanged` behavior stays compatible.

## Approach Overview (Strangler / No-regression)

We integrate `SystemLifecycleManager` gradually and make it capable of expressing the existing "soft deactivation" semantics.

Important: the current `SystemPhase` model (`Idle`, `WaitingForBarcode`, `Preparing`, `Testing`, `Completed`, `Resetting`)
does not explicitly represent: "**activated** but scanner session released" (soft deactivation).

Therefore Phase 2 requires extending lifecycle modeling so the "source of truth" can preserve:
- "Activated context" (loop/timing ownership)
- "Scanner session desired state" (acquire/release)
- "Input enabled" (UI read-only)

## Planned Changes (by step)

### Step 0 - Document the contract as scenarios (before code)
- Write a scenario table for:
  - activation path (inactive -> initial activation)
  - refresh path (already active -> acquire session + resume timing)
  - soft deactivation path
  - full deactivation path
  - reset start/complete paths (soft/hard meaning)
  - `OnStateChanged` expectations
- Source docs to align with:
  - `Final_Test_Hybrid/Docs/ScanModeControllerGuide.md`
  - `Final_Test_Hybrid/Docs/StateManagementGuide.md`
  - `Final_Test_Hybrid/Docs/PlcResetGuide.md`

### Step 1 - Add characterization tests (regression lock)
- Unit tests for `ScanSessionManager`:
  - idempotent acquire/release
  - dispose behavior
- Unit tests for `ScanModeController` using fakes/mocks for dependencies:
  - cover every scenario from Step 0
  - assert side effects: session calls, timing calls, loop cancellation, grid calls
  - assert `IsInScanningPhase` / `wasInScanPhase` semantics

Acceptance gate: tests pass on current implementation.

### Step 2 - Add "soft disable" capability to lifecycle modeling (design step)
Goal: allow lifecycle to represent the current behavior without changing semantics.

Design direction:
- Introduce a distinct concept for *activation context* vs *scan-mode enablement*.
- Add triggers (or equivalent mechanism) that represent:
  - ScanModeDisabledSoft (release scanner session; keep activation context alive; keep loop running)
  - ScanModeDisabledHard (cancel loop; clear activation context; transition to inactive/Idle)
- Provide lifecycle properties that `ScanModeController` can follow deterministically:
  - `IsScannerDesired` (acquire/release session)
  - `IsTimingDesired` (pause/resume timing)
  - `ShouldLoopBeRunning` (start/cancel loop)

Deliverable: update OpenSpec deltas for `system-lifecycle-manager` to include the new semantics,
and update design notes for Phase 2 integration.

### Step 3 - Shadow integration in ScanModeController (no side-effect switch yet)
- Inject `SystemLifecycleManager` into `ScanModeController`.
- On input events (Operator/AutoReady/Reset):
  - call lifecycle transitions in "shadow" mode
  - log mismatches between lifecycle-derived "desired" state and current flags/behavior
- Keep existing side-effect code path unchanged.

Acceptance gate: characterization tests still pass; no behavior changes observed.

### Step 4 - Single point of truth for scanner session (first real simplification)
- Move scanner session acquisition/release to one place, driven by lifecycle "desired" state.
- Remove duplicate calls scattered across activation/deactivation/reset paths.

Acceptance gate: characterization tests pass; manual smoke checks for scanner session behavior.

### Step 5 - Replace `_isResetting` with lifecycle state
- Stop using `_isResetting` as a local flag; derive from lifecycle state.
- Keep `wasInScanPhase` semantics identical to Step 0 contract.

Acceptance gate: characterization tests pass, especially PLC reset scenarios.

### Step 6 - Replace `_isActivated` with lifecycle (preserving soft deactivation semantics)
- Remove `_isActivated` after lifecycle can express:
  - "activated but soft-disabled" state (loop still running, scanner session released)
  - "fully inactive" state (loop cancelled)
- Simplify `TryActivateScanMode` / `TryDeactivateScanMode` / `TransitionToReadyInternal` into:
  - compute trigger -> `Lifecycle.Transition(...)`
  - let lifecycle handler apply effects deterministically

Acceptance gate: characterization tests pass; compare logs and manual reset/scan flow to baseline.

### Step 7 - Simplify ScanSessionManager API (optional, if it reduces coupling)
- Consider changing `AcquireSession/ReleaseSession` to a "desired state" API:
  - `EnsureActive(Action<string> handler)`
  - `EnsureInactive()`
- Goal: make ScanModeController logic linear and avoid duplication of "if session is null" checks.

Acceptance gate: no test regressions; code is simpler (fewer branching points).

## Risk Controls

- No-regression policy enforced via characterization tests (Step 1) before any semantic refactor.
- Each step must be small and independently reversible.
- Avoid holding locks while invoking external services/events (to reduce deadlock risk).
- Keep public surface stable during Phase 2.

## Completion Criteria (Phase 2)

- `ScanModeController` no longer stores `_isActivated` / `_isResetting` flags.
- Scanner session management is centralized and lifecycle-driven.
- Soft deactivation semantics are preserved exactly as in baseline.
- Tests cover the scenario matrix and pass consistently.

