## Context
Execution logic (`TestExecutionCoordinator` + `ColumnExecutor`) mixes UI status with execution semantics, contains recursive waits, and logs are inconsistent. There is a confirmed hang after Retry/Skip on the last step of a map, and map transitions depend on UI visibility instead of execution-idle.

## Goals / Non-Goals
- Goals:
  - Preserve external behavior and public APIs.
  - Make execution flow explicit and deterministic.
  - Ensure map transition depends on execution-idle only.
  - Standardize logs for diagnosability.
  - Replace recursive waits with bounded loops.
- Non-Goals:
  - Changing `ExecutionStateManager` or `TestExecutionCoordinator` public API.
  - Changing error resolution semantics (Retry/Skip decision rules).
  - Introducing new automated tests in this change.

## Decisions
- Decision: Enforce AGENTS.md constraints for this change.
  - Why: Keep methods compact (one control-flow per method), use `var` + `{}` everywhere, and split files into partials to stay under 300 lines.
- Decision: Introduce explicit execution-idle state in `ColumnExecutor`.
  - Why: UI visibility is not an execution invariant.
- Decision: Add final resolution barrier after last step when `HasFailed`.
  - Why: Prevent map completion before retry/skip resolution.
- Decision: New unified log schema for execution events.
  - Why: Deterministic analysis and reduced ambiguity across waits.
- Decision: Replace recursive waits with loop + rate-limited logs.
  - Why: Avoid stack growth and improve observability.
- Decision: Preserve current pause policy during waits.
  - Why: Behavior-preserving refactor, avoid operator-facing changes.

## Risks / Trade-offs
- Risk: Merge conflicts with active changes touching execution or skip handling.
  - Mitigation: Limit scope to execution flow and log schema; document overlap in proposal.
- Risk: Hidden dependencies on old log formats.
  - Mitigation: Keep old log fields available as structured data; update docs.

## Migration Plan
1. Phase 0: Logging refactor + waitpoint audit (no behavior change).
2. Phase 1: Final resolution barrier for last-step failure.
3. Phase 2: Decouple execution-idle from UI.
4. Phase 3: Replace recursion + add bounded timeouts.
5. Phase 4: Normalize PLC impulse handling.
6. Phase 5: Manual regression per checklist.

## Open Questions
- None (default: preserve existing pause and error resolution semantics).
