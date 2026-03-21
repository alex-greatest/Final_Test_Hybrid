# 2026-03-21 boilerlock autoready interrupt overwrite gap

## Failure mode

- Во время active pre-execution/test execution пауза `BoilerLock` и пауза `AutoModeDisabled` используют общий `PauseToken` и общий `CurrentInterrupt`.
- Если `BoilerLock` уже активен, последующий `AutoReady OFF` может перезаписать `CurrentInterrupt` значением `AutoModeDisabled`.
- После такой перезаписи `AutoReady ON` видит уже `CurrentInterrupt == AutoModeDisabled` и имеет право выполнить `Resume()`.
- Визуально это выглядит как снятие паузы по потере автомата, хотя исходная причина паузы была связана с `BoilerLock`.
- Реальный `BoilerLock` затем возвращается только при следующем ping-цикле, если условие `1005 == 1` и whitelist ошибки всё ещё актуальны.

## Root cause

- `BoilerLockRuntimeService` поднимает `InterruptReason.BoilerLock` только если `CurrentInterrupt == null`, и не стартует поверх чужого interrupt.
- `ErrorCoordinator.HandleAutoReadyChanged()` вне terminal handshake при `AutoReady OFF` не проверяет already-active interrupt и вызывает `HandleInterruptAsync(InterruptReason.AutoModeDisabled)`.
- `ErrorCoordinator.ProcessInterruptAsync(...)` безусловно вызывает `SetCurrentInterrupt(reason)` после terminal/ready-check и тем самым заменяет прежний owner interrupt.
- `TryResumeFromPauseAsync()` корректно ownership-aware только относительно текущего значения `CurrentInterrupt`, но не восстанавливает исходный owner, если тот уже был перезаписан.

## Resolution

- Код в этом change-set не менялся.
- Stable docs синхронизированы с фактическим runtime-поведением:
  - broad-resume по `AutoReady ON` описан как безопасный только пока `CurrentInterrupt` остаётся non-`AutoModeDisabled`;
  - residual gap `BoilerLock -> AutoReady OFF -> AutoReady ON` зафиксирован явно;
  - `BoilerLock` guide теперь не утверждает implicit isolation от `AutoReady` ownership.
- Active impact того же workstream обновлён и больше не утверждает, что `BoilerLock recovery` уже полностью корректен.

## Verification

- Выполнена сверка source-of-truth docs с фактическим кодом:
  - `Docs/runtime/ErrorCoordinatorGuide.md`
  - `Docs/diagnostics/BoilerLockGuide.md`
  - `Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.cs`
  - `Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.Interrupts.cs`
  - `Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.Resolution.cs`
  - `Services/Diagnostic/Services/BoilerLockRuntimeService.cs`
- Подтверждено, что существующий unit-test покрывает только сценарий `BoilerLock -> AutoReady ON`, но не `BoilerLock -> AutoReady OFF -> AutoReady ON`.
- `dotnet build` и formatter-проверки не запускались: change-set документальный, repo-tracked runtime-код не менялся.

## Notes

- Отдельного incident-registry в репозитории не обнаружено; этот change-doc используется как явная фиксация нового failure mode и должен упоминаться из active impact.
- Это документирование несоответствия между кодом и прежней формулировкой docs, а не исправление runtime-логики.
