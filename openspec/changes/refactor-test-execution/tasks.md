## 1. Proposal Alignment
- [ ] 1.1 Confirm overlap handling with active changes (`update-skip-hang-guard`, `refactor-execution-state-machine`)
- [ ] 1.2 Enforce AGENTS.md constraints: one control-flow per method (guard clauses ok), `var` + `{}` everywhere, services/files <300 lines (split into partials)

## 2. Phase 0 — Observability + Logging Scheme
- [ ] 2.1 Introduce unified execution logging helper and event vocabulary
- [ ] 2.2 Replace existing DIAG logs with new structured events
- [ ] 2.3 Populate `plan/waitpoints-audit-template.md` to 70–80%

## 3. Phase 1 — Final Resolution Barrier
- [ ] 3.1 Add end-of-map resolution barrier in `ColumnExecutor`
- [ ] 3.2 Ensure executor reaches execution-idle only after resolution

## 4. Phase 2 — Decouple Idle from UI
- [ ] 4.1 Add explicit execution-idle state in `ColumnExecutor`
- [ ] 4.2 Update `WaitForExecutorsIdleAsync` to use execution-idle

## 5. Phase 3 — Waitpoints + Timeouts
- [ ] 5.1 Replace recursion in `WaitForMapAccessAsync` with loop + rate-limited logging
- [ ] 5.2 Add bounded timeout policy for completion End reset wait
- [ ] 5.3 Normalize pause policy for waits (preserve current behavior)

## 6. Phase 4 — PLC Impulse Handling
- [ ] 6.1 Apply direct read + subscription pattern to Retry/Skip waits
- [ ] 6.2 Apply pattern to completion End reset wait

## 7. Phase 5 — Manual Regression
- [ ] 7.1 Run edge-cases checklist after each phase (EC-1, EC-2, EC-21 minimum)
- [ ] 7.2 Summarize results in proposal notes
