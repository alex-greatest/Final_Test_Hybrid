## Phase 1: ExecutionFlowState

### 1.1 Создать [Flags] enum

- [ ] 1.1.1 Определить `ExecutionStopFlags` с битами: `None`, `Failure`, `Operator`, `AutoModeDisabled`, `PlcForceStop`, `PlcSoftReset`, `PlcHardReset`
- [ ] 1.1.2 Добавить `ReasonMask` константу для выделения причины
- [ ] 1.1.3 Реализовать `RequestStop(ExecutionStopFlags)` с first-reason-wins + failure-OR
- [ ] 1.1.4 Обновить `GetSnapshot()` для работы с flags
- [ ] 1.1.5 Сохранить `ClearStop()` без изменений

**Validation:** Unit-тесты OR-логики проходят

### 1.2 Миграция call sites

- [ ] 1.2.1 `TestExecutionCoordinator.Execution.cs:159` — обновить вызов RequestStop
- [ ] 1.2.2 `TestExecutionCoordinator.ErrorHandling.cs:138` — обновить проверки
- [ ] 1.2.3 `PreExecutionCoordinator.cs:232` — обновить вызов RequestStop
- [ ] 1.2.4 `PreExecutionCoordinator.Retry.cs:40` — обновить проверки
- [ ] 1.2.5 Grep по `StopAsFailure` — убедиться все места обновлены
- [ ] 1.2.6 Grep по `StopReason` — убедиться все места обновлены

**Validation:** Проект компилируется, тесты проходят

### 1.3 Удаление старого API

- [ ] 1.3.1 Удалить enum `ExecutionStopReason`
- [ ] 1.3.2 Удалить поле `_stopAsFailure`
- [ ] 1.3.3 Удалить property `StopAsFailure`
- [ ] 1.3.4 Обновить property `StopReason` для извлечения reason из flags

**Validation:** Нет warnings, чистая компиляция

## Phase 2: Интеграция SystemLifecycleManager

### 2.1 ScanModeController

- [ ] 2.1.1 Добавить `SystemLifecycleManager` в конструктор
- [ ] 2.1.2 В `PerformInitialActivation()`: `_lifecycle.Transition(SystemTrigger.ScanModeEnabled)`
- [ ] 2.1.3 В `PerformFullDeactivation()`: `_lifecycle.Transition(SystemTrigger.ScanModeDisabled)`
- [ ] 2.1.4 Проверить что soft deactivation НЕ вызывает transition

**Validation:** Phase корректно переходит Idle ↔ WaitingForBarcode

### 2.2 PreExecutionCoordinator

- [ ] 2.2.1 Добавить `SystemLifecycleManager` в конструктор
- [ ] 2.2.2 В `SubmitBarcode()`: `_lifecycle.Transition(SystemTrigger.BarcodeReceived, barcode)`
- [ ] 2.2.3 При успешном `StartTestExecution()`: `_lifecycle.Transition(SystemTrigger.PreparationCompleted)`
- [ ] 2.2.4 При `PipelineFailed`: `_lifecycle.Transition(SystemTrigger.PreparationFailed)`
- [ ] 2.2.5 Обработать edge case: reset во время preparation

**Validation:** Phase корректно переходит WaitingForBarcode → Preparing → Testing/WaitingForBarcode

### 2.3 TestExecutionCoordinator

- [ ] 2.3.1 Добавить `SystemLifecycleManager` в конструктор
- [ ] 2.3.2 В `HandleTestCompleted()`: `_lifecycle.Transition(SystemTrigger.TestFinished)`
- [ ] 2.3.3 При repeat: `_lifecycle.Transition(SystemTrigger.RepeatRequested)`
- [ ] 2.3.4 При acknowledge: `_lifecycle.Transition(SystemTrigger.TestAcknowledged)`

**Validation:** Phase корректно переходит Testing → Completed → WaitingForBarcode

### 2.4 PlcResetCoordinator

- [ ] 2.4.1 Добавить `SystemLifecycleManager` в конструктор
- [ ] 2.4.2 В `ExecuteResetStepsAsync()`: определить soft/hard по `wasInScanPhase`
- [ ] 2.4.3 При soft: `_lifecycle.Transition(SystemTrigger.ResetRequestedSoft)`
- [ ] 2.4.4 При hard: `_lifecycle.Transition(SystemTrigger.ResetRequestedHard)`
- [ ] 2.4.5 В `ExecuteSmartReset()` soft: `_lifecycle.Transition(SystemTrigger.ResetCompletedSoft)`
- [ ] 2.4.6 В `ExecuteSmartReset()` hard: `_lifecycle.Transition(SystemTrigger.ResetCompletedHard)`

**Validation:**
- Soft reset: barcode сохранён, Phase = WaitingForBarcode
- Hard reset: barcode очищен, Phase = Idle

## Phase 3: Упрощение ScanModeController

### 3.1 Замена флагов на lifecycle

- [ ] 3.1.1 Заменить `_isActivated` на `_lifecycle.Phase != SystemPhase.Idle`
- [ ] 3.1.2 Заменить `_isResetting` на `_lifecycle.Phase == SystemPhase.Resetting`
- [ ] 3.1.3 Обновить `IsInScanningPhase` — использовать `_lifecycle.Phase == SystemPhase.WaitingForBarcode`

### 3.2 Удаление флагов

- [ ] 3.2.1 Удалить поле `_isActivated`
- [ ] 3.2.2 Удалить поле `_isResetting`
- [ ] 3.2.3 Удалить `IsInScanningPhaseUnsafe` (теперь thread-safe через lifecycle)

### 3.3 Рефакторинг методов

- [ ] 3.3.1 `HandleResetStarting()` — убрать установку `_isResetting`
- [ ] 3.3.2 `TransitionToReadyInternal()` — убрать сброс `_isResetting`
- [ ] 3.3.3 Проверить все условия где использовались флаги

**Validation:**
- Поведение ScanModeController не изменилось
- Нет дублирования состояния

## Финальная проверка

- [ ] Все unit-тесты проходят
- [ ] Нет регрессий в UI
- [ ] Lifecycle корректно отслеживает все фазы
- [ ] OR-логика сохранена
- [ ] Soft/hard reset работают корректно
- [ ] Barcode сохраняется/очищается по правилам
