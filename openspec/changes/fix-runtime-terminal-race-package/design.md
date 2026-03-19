## Context

Пакет затрагивает несколько связанных контуров:
- completion/post-AskEnd terminal handshake;
- ownership interrupt'ов в `ErrorCoordinator`;
- wait helper `WaitForFalseAsync`;
- ownership shared `IModbusDispatcher` в `ConnectionTestPanel`.

Нельзя расширять `ExecutionActivityTracker` и нельзя массово мигрировать все `GetValue<bool>` по репозиторию.

## Goals / Non-Goals

- Goals:
  - Убрать false-finish/false-cleanup на `unknown` cache.
  - Сделать terminal ownership явным и узким.
  - Не ломать shared dispatcher при закрытии `ConnectionTestPanel`.
  - Оставить changeover/timer semantics без архитектурной переделки.
- Non-Goals:
  - Массовый рефактор state machine.
  - Изменение поведения manual screens во время runtime.
  - Переделка `WaitGroup/WaitForAllTrue`.

## Decisions

- Decision: использовать отдельный `RuntimeTerminalState`, а не расширять `ExecutionActivityTracker`.
  - Why: terminal window не является active phase, но должно участвовать в owner-решениях `ErrorCoordinator`.
- Decision: safe-read вводится только через `TryGetValue<T>` и только для safety-critical decision-loop'ов.
  - Why: пакет чинит подтверждённую дыру без массовой смены семантики runtime-cache.
- Decision: `WaitForFalseAsync` исправляется локально raw-cache recheck'ом после subscribe/resume.
  - Why: именно этот helper давал подтверждённый false-success; generic пути остаются вне scope.
- Decision: в `ConnectionTestPanel` сохраняется только ownership `startedByPanel`.
  - Why: это локально убирает риск остановки shared dispatcher и не меняет поведение manual tools во время runtime.

## Risks / Trade-offs

- Terminal ownership становится отдельным state-object'ом, значит его нужно обновлять строго в `try/finally` и cancel-path'ах.
- Поведение manual screens во время runtime остаётся прежним; пакет это сознательно не пересматривает.
- Test harness использует white-box/reflection для приватных helper-path'ов, чтобы не раздувать production API.

## Migration Plan

1. Реализовать helper/state и ownership shared dispatcher.
2. Добавить unit tests на подтверждённые failure mode.
3. Синхронизировать stable docs и active impact.
4. Прогнать `dotnet build`, `dotnet test`, `dotnet format`, `inspectcode`, `openspec validate`.

## Open Questions

- Нужен ли отдельный follow-up пакет на manual screens, если их поведение во время runtime всё же потребуется пересмотреть отдельно?
