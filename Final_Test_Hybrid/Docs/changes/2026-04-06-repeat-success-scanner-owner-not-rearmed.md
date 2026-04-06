# 2026-04-06 repeat success scanner owner not rearmed

## Failure mode

- После сценария `PLC reset -> post-AskEnd repeat -> успешное завершение теста` ordinary scanner owner мог остаться в состоянии `None`.
- Визуально это проявлялось так:
  - completion-flow завершался штатно;
  - sequence UI очищался до scan-строки;
  - `BoilerInfo` оставался read-only;
  - ordinary raw scanner не запускал новый pre-execution цикл.
- Следующий PLC reset возвращал scanner owner в `PreExecution`, поэтому дефект выглядел как "после нового reset само починилось".

## Root cause

- В change-set `scanner ownership` обычный scan-mode стал зависеть не только от `IsAcceptingInput`, но и от явного owner `PreExecution`.
- В repeat-сценарии после `post-AskEnd` controller корректно пропускал немедленный возврат ordinary scanner-ready (`repeat` outcome не должен был поднимать scan-mode сразу).
- После этого следующий тест запускался через `_skipNextScan`, без нового обычного scan-step.
- При его штатном завершении completion-path очищал состояние, но не выполнял явного возврата ordinary scanner owner.
- В результате система могла снова войти в ожидание barcode с `owner=None`.

## Resolution

- `ScanModeController` получил узкий self-heal ordinary scanner owner в `HandlePreExecutionStateChanged()`.
- Self-heal срабатывает только если одновременно:
  - `PreExecutionCoordinator.IsAcceptingInput = true`
  - `AutoReady = true`
  - PLC connected
  - reset не активен
  - controller уже активирован
  - текущий owner = `None`
- При active `Dialog` owner self-heal не срабатывает.
- `ScanSessionManager.AcquireSession(...)` больше не считает cached handler достаточным признаком живой ordinary session:
  если reset уже снял `PreExecution` owner через `ReleaseAllForReset()`, повторный `AcquireSession(...)` обязан заново вызвать `EnsurePreExecutionOwner(...)`.
- Completion-handshake, `StepTimingService`, `BoilerState` timers и changeover-path этим fix не менялись.

## Verification

- Добавлен regression-test на сценарий `repeat -> success -> возврат в ожидание barcode -> owner rearm`.
- Добавлен regression-test на сценарий `same handler + owner dropped by reset -> session rearm`.
- Добавлены негативные проверки:
  - `Dialog` owner не перехватывается назад;
  - `AutoReady=false` не даёт rearm;
  - `PLC disconnected` не даёт rearm;
  - `IsAcceptingInput=false` не даёт rearm.
- `dotnet build Final_Test_Hybrid.slnx` — passed.
- Целевой `dotnet test` по scanner/pre-execution/completion — passed (`20/20`).
- Дополнительно подтверждено логами production-like прогона:
  - штатный completion завершался;
  - обычный owner не возвращался до следующего PLC reset;
  - после reset owner снова становился `PreExecution`.

## Notes

- Это новый подтверждённый failure mode runtime scanner ownership.
- Stable docs и `AGENTS.md` дополнительно закрепляют regression-guard:
  scanner unlock / `BoilerInfo` unlock должны оставаться единым runtime-state и не должны менять `StepTimingService`/`BoilerState` timer ownership contract.
- Отдельного incident registry в репозитории нет; данный change-doc используется как явная фиксация инцидента и должен быть связан с active impact.
