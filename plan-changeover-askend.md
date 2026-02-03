# Plan: Changeover Timer Must Not Start Before AskEnd (PLC Reset)

## Summary
Fix: while PLC reset is in progress and AskEnd has not been received yet, changeover timer MUST NOT start/reset.
Exception: when interrupt-reason dialog is required, changeover must remain stopped after AskEnd until reason is successfully saved (MES/DB). If a second reset happens during the dialog, the dialog closes and changeover starts after AskEnd of the second reset (existing behavior).

Safety-critical: keep diff minimal and local, no public API changes.

## Current Critical Flow
- PlcResetCoordinator raises OnAskEndReceived after AskEnd.
- PreExecutionCoordinator.HandleGridClear() runs after AskEnd.
- ExecuteGridClearAsync() calls RecordAskEndSequence() early, but CompletePlcReset() only after dialog completes.
Therefore we must gate "start is allowed" on RecordAskEndSequence/reset-seq match, not on _askEndSignal completion.

## Intended Behavior (Decision Complete)
1) Before AskEnd (for current reset-seq): changeover timer must not start/reset.
2) After AskEnd:
- If no interrupt dialog required: changeover starts immediately after AskEnd.
- If interrupt dialog required: changeover stays stopped until reason is saved successfully.
3) If second reset occurs during dialog: dialog closes; changeover starts after AskEnd of the second reset.
4) AskEnd timeout/TagTimeout path: preserve existing behavior (Reset via interrupt).

## Implementation (Minimal Diff)

### A) Force PLC resets into AskEnd-gated modes (no Immediate)
File: Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Changeover.cs

Update GetChangeoverResetMode():
- if ShouldDelayChangeoverStart() => WaitForReason
- else if stopReason in {PlcSoftReset, PlcHardReset, PlcForceStop} => WaitForAskEndOnly
- else => Immediate

Rationale: currently Immediate can happen on PLC reset when test was running but dialog is not shown (missing SN / UseInterruptReason=false), causing early changeover start.

### B) Add a hard guard at StartChangeoverTimerImmediate()
Same file.

At the beginning:
- currentSeq = GetResetSequenceSnapshot()
- if stopReason in {PlcSoftReset, PlcHardReset, PlcForceStop} AND _changeoverAskEndSequence != currentSeq:
  - trigger = ShouldDelayChangeoverStart() ? ChangeoverTriggerReasonSaved : ChangeoverTriggerAskEndOnly
  - TryArmChangeoverPending(trigger)
  - return

Then proceed with existing start (store started-seq and call BoilerState.ResetAndStartChangeoverTimer()).

This prevents any path from starting changeover before AskEnd, and preserves "wait for reason" behavior when the dialog is required.

## Validation

### Build
- dotnet build (Final_Test_Hybrid/Final_Test_Hybrid.csproj)

### Manual Acceptance Scenarios
1) PLC reset started, AskEnd not yet:
- changeover timer does not start/reset.

2) AskEnd received, NO reason dialog (SN missing or UseInterruptReason=false):
- changeover starts immediately after AskEnd.

3) AskEnd received, reason dialog shown:
- changeover does not run while dialog is open.
- after successful save to MES/DB: changeover starts.

4) Second reset during reason dialog:
- dialog closes.
- after AskEnd of second reset: changeover starts (no input).

## Risks / Mitigations
- Timing regression risk: mitigated by local changes + guard at start point + mandatory manual scenarios.
- StopReason mapping risk: verify logs/behavior for PlcSoftReset/PlcHardReset/PlcForceStop.
