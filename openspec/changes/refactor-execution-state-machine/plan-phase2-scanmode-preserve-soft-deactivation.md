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

#### Scenario Matrix (Characterization Contract)

Definitions:
- State:
  - Activation: Inactive|Active (equivalent to `_isActivated`)
  - IsResetting: false|true (equivalent to `_isResetting`)
- Facts:
  - IsOperatorAuthenticated, IsAutoReady, IsExecutionActive, IsWaitingForScan
  - IsScanModeEnabled = IsOperatorAuthenticated && IsAutoReady
- SoftReason (must match current `ShouldUseSoftDeactivation()`):
  - (IsOperatorAuthenticated && !IsAutoReady) || IsExecutionActive || (IsOperatorAuthenticated && IsWaitingForScan)
- Events:
  - ScanModeEnabled
  - ScanModeDisabledSoft
  - ScanModeDisabledHard(IsOperatorAuthenticated)
  - PlcResetStarting
  - PlcResetCompleted(IsScanModeEnabledNow)
- Actions:
  - AcquireSession, ReleaseSession
  - PauseAllTiming (PauseAllColumnsTiming), ResumeTiming (ResumeAllColumnsTiming)
  - EnsureScanStepInGrid
  - StartScanTiming, ResetScanTiming
  - StartMainLoop, CancelMainLoop
  - ClearGridAllExceptScan (only hard disable + !IsOperatorAuthenticated)
  - WasInScanPhase = (Activation==Active && IsResetting==false) [only on PlcResetStarting]

##### A. ScanModeEnabled

A1 Initial activation
- Given: (Inactive, false)
- When: ScanModeEnabled
- Then: (Active, false)
- Actions: AcquireSession + EnsureScanStepInGrid + StartScanTiming + StartMainLoop
- Must NOT: ResumeTiming, ResetScanTiming, CancelMainLoop, ReleaseSession

A2 Refresh (already Active)
- Given: (Active, false)
- When: ScanModeEnabled
- Then: (Active, false)
- Actions: AcquireSession + ResumeTiming
- Must NOT: StartMainLoop, EnsureScanStepInGrid, StartScanTiming, ResetScanTiming

A3 Enable while resetting ignored
- Given: (*, true)
- When: ScanModeEnabled
- Then: no-op
- Actions: None

##### B. ScanModeDisabledSoft (IsScanModeEnabled==false && SoftReason==true)

B1 Soft deactivation keeps activation context
- Given: (Active, false)
- When: ScanModeDisabledSoft
- Then: (Active, false)
- Actions: PauseAllTiming + ReleaseSession
- Must NOT: CancelMainLoop, ClearGridAllExceptScan

B2 Soft disable idempotent
- Given: (Active, false)
- When: ScanModeDisabledSoft again
- Then: (Active, false)
- Actions: PauseAllTiming + ReleaseSession (idempotent)

B3 Soft disable while inactive no-op
- Given: (Inactive, false)
- When: ScanModeDisabledSoft
- Then: no-op
- Actions: None

B4 Soft disable while resetting ignored
- Given: (*, true)
- When: ScanModeDisabledSoft
- Then: no-op
- Actions: None

B5 Logout + execution active still uses SOFT (current behavior)
- Facts: IsOperatorAuthenticated=false, IsExecutionActive=true
- Expected: ScanModeDisabledSoft path (PauseAllTiming + ReleaseSession), MUST NOT cancel loop or clear grid

##### C. ScanModeDisabledHard (IsScanModeEnabled==false && SoftReason==false)

C1 Hard deactivation cancels loop and clears activation
- Given: (Active, false)
- When: ScanModeDisabledHard(IsOperatorAuthenticated)
- Then: (Inactive, false)
- Actions: PauseAllTiming + ReleaseSession + CancelMainLoop
- Extra: if IsOperatorAuthenticated==false => + ClearGridAllExceptScan
- Must NOT: StartMainLoop, AcquireSession, EnsureScanStepInGrid, StartScanTiming, ResetScanTiming

C2 Hard disable while inactive no-op
- Given: (Inactive, false)
- When: ScanModeDisabledHard(any)
- Then: no-op
- Actions: None

C3 Hard disable while resetting ignored
- Given: (*, true)
- When: ScanModeDisabledHard(any)
- Then: no-op
- Actions: None

##### D. PLC reset start (HandleResetStarting equivalence)

D1 Reset start from scan phase
- Given: (Active, false)
- When: PlcResetStarting
- Then: (Active, true)
- WasInScanPhase: true
- Actions: PauseAllTiming + ReleaseSession

D2 Reset start from inactive
- Given: (Inactive, false)
- When: PlcResetStarting
- Then: (Inactive, true)
- WasInScanPhase: false
- Actions: PauseAllTiming + ReleaseSession

D3 Reset start when already resetting
- Given: (*, true)
- When: PlcResetStarting
- Then: no-op (or keep IsResetting true)
- WasInScanPhase: false
- Actions: PauseAllTiming + ReleaseSession allowed (idempotent), no other effects

##### E. PLC reset complete (HandleResetCompleted+TransitionToReadyInternal equivalence)

E1 Reset complete, scan mode disabled now => full stop
- Given: (*, true), IsScanModeEnabledNow=false
- When: PlcResetCompleted(false)
- Then: (Inactive, false)
- Actions: CancelMainLoop + PauseAllTiming
- Must NOT: AcquireSession, StartMainLoop, EnsureScanStepInGrid, StartScanTiming, ResetScanTiming, ResumeTiming

E2 Reset complete, enabled now, activation inactive => initial activation
- Given: (Inactive, true), IsScanModeEnabledNow=true
- When: PlcResetCompleted(true)
- Then: (Active, false)
- Actions: AcquireSession + EnsureScanStepInGrid + StartScanTiming + StartMainLoop
- Must NOT: ResumeTiming, ResetScanTiming

E3 Reset complete, enabled now, activation already active => ready refresh
- Given: (Active, true), IsScanModeEnabledNow=true
- When: PlcResetCompleted(true)
- Then: (Active, false)
- Actions: ResetScanTiming + AcquireSession
- Must NOT: ResumeTiming, StartMainLoop, EnsureScanStepInGrid, StartScanTiming, CancelMainLoop

##### OnStateChanged contract (Phase 2)

- OnStateChanged is raised only after UpdateScanModeState (operator/autoready changes and initial ctor call).
- PlcResetStarting/Completed do not raise OnStateChanged directly.

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
