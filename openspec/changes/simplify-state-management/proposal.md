# Change: simplify-state-management

## Why

Текущая система использует множество булевых флагов для управления состоянием,
что приводит к сложности понимания и риску race conditions.
SystemLifecycleManager создан и работает, но ScanModeController дублирует состояние через `_isActivated` и `_isResetting`.

## What Changes

### Phase 1: ExecutionFlowState

Заменить `ExecutionStopReason` enum + `_stopAsFailure` bool на единый `[Flags] enum ExecutionStopFlags` с сохранением семантики:
- **First-reason-wins**: первая причина остановки сохраняется
- **Failure OR**: флаг failure объединяется через OR

### Phase 2: Интеграция SystemLifecycleManager

Добавить вызовы `Transition(SystemTrigger)` в координаторы:
- `ScanModeController`: `ScanModeEnabled`, `ScanModeDisabled`
- `PreExecutionCoordinator`: `BarcodeReceived`, `PreparationCompleted`, `PreparationFailed`
- `PlcResetCoordinator`: `ResetRequestedSoft/Hard`, `ResetCompletedSoft/Hard`

### Phase 3: Упрощение ScanModeController

Удалить дублирующие флаги `_isActivated` и `_isResetting`, заменив на проверки `SystemLifecycleManager.Phase`.

## Impact

- Affected specs: execution-flow
- Affected code:
  - `ExecutionFlowState.cs`
  - `ScanModeController.cs`
  - `PreExecutionCoordinator.cs` (MainLoop, точки перехода)
  - `PlcResetCoordinator.cs`
  - `TestExecutionCoordinator.cs` (call sites)
- **BREAKING**: Нет — поведение сохраняется

## Future Work (отложено)

### UI-компоненты (BoilerInfo, SwitchMes)

- **Причина отложения:** `IsScannerInputEnabled` не покрывает все условия блокировки
- **Решение:** Создать InputGateService — агрегатор всех условий блокировки
- **Зависимость:** После стабилизации SystemLifecycleManager

### PreExecutionCoordinator (разбиение на классы)

- **Причина отложения:** Хрупкие гонки (`_resetSequence`, `_resetCts`, `_askEndSignal`)
- **Решение:** Извлечь stateless helper классы
- **Зависимость:** Требует детального анализа порядка вызовов
