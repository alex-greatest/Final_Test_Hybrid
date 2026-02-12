# Change: Refactor Test Execution Flow (Behavior-Preserving)

## Why
Исполнение тестов сложно понимать и поддерживать из-за смешения UI-статуса и execution-логики, разрозненных ожиданий и неравномерных логов. Есть подтвержденные hang-сценарии при Retry/Skip и переходе между картами. Нужна более прозрачная структура выполнения и единый формат диагностики при сохранении текущего поведения.

## What Changes
- Переписать внутренний поток выполнения в `TestExecutionCoordinator` и `ColumnExecutor` без изменения публичных API.
- Ввести явное состояние execution-idle (не зависящее от UI `IsVisible`).
- Ввести финальный барьер разрешения ошибки после последнего шага карты.
- Единая схема логирования (стандартизованные события/поля вместо существующих DIAG сообщений).
- Аудит и нормализация всех waitpoints (bounded waits, отсутствие рекурсии).
- Привести ожидания PLC импульсов к единому паттерну (direct read + subscription).

## Impact
- Affected specs: **new** `test-execution` (ADDED requirements)
- Affected code:
  - `Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.*.cs`
  - `Services/Steps/Infrastructure/Execution/ColumnExecutor.cs`
  - `Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.*.cs`
  - `Services/Steps/Infrastructure/Execution/ErrorCoordinator/*`
  - `Services/OpcUa/TagWaiter.cs`
- Affected docs:
  - `Docs/execution/StateManagementGuide.md`
  - `Docs/execution/RetrySkipGuide.md`
  - `Docs/execution/CancellationGuide.md`

## Notes on Existing Changes
- Есть активные изменения `update-skip-hang-guard` и `refactor-execution-state-machine`. Данный change затрагивает `TestExecutionCoordinator` и `ColumnExecutor`, поэтому возможны merge-конфликты. При реализации избегаем изменения областей, заявленных вне scope у `refactor-execution-state-machine`.
