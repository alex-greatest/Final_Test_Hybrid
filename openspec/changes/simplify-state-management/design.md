## Context

Система управления состоянием в Final Test Hybrid использует:
- `ExecutionFlowState` с `ExecutionStopReason` enum + `_stopAsFailure` bool
- `SystemLifecycleManager` с `SystemPhase` и `SystemTrigger` (уже реализован)
- `ScanModeController` с дублирующими флагами `_isActivated` и `_isResetting`

### Существующая модель SystemPhase

```
Idle ──ScanModeEnabled──► WaitingForBarcode
  ▲                              │
  │                    BarcodeReceived
  │                              ▼
  │                         Preparing
  │                         /       \
  │      PreparationFailed /         \ PreparationCompleted
  │                       ▼           ▼
  │              WaitingForBarcode   Testing
  │                                    │
  │                              TestFinished
  │                                    ▼
  │                               Completed
  │                              /         \
  │         RepeatRequested     /           \ TestAcknowledged
  │                            ▼             ▼
  │                   WaitingForBarcode   WaitingForBarcode
  │
  │    Reset (any phase except Idle) ──► Resetting
  │                                          │
  │         ResetCompletedHard ◄─────────────┤
  │                                          │
  └──────────────────────────────────────────┘
                               ResetCompletedSoft ──► WaitingForBarcode
```

### Критические замечания Codex Review

1. **API несоответствие**: Spec использовал несуществующие методы и фазы
2. **Soft/Hard reset**: Различная логика — soft сохраняет barcode, hard очищает
3. **Call sites**: Не все места использования `StopAsFailure` были учтены

## Goals / Non-Goals

### Goals

- Упростить `ExecutionFlowState` через `[Flags]` enum
- Интегрировать `SystemLifecycleManager` во все координаторы
- Убрать дублирование `_isActivated`/`_isResetting` из `ScanModeController`
- Сохранить существующее поведение (first-reason-wins + failure-OR)

### Non-Goals

- Изменение UI-компонентов
- Разбиение PreExecutionCoordinator
- Создание InputGateService

## Decisions

### Decision 1: [Flags] enum для ExecutionStopFlags

**Что:** Объединить `ExecutionStopReason` + `_stopAsFailure` в единый `[Flags] enum`

```csharp
[Flags]
public enum ExecutionStopFlags
{
    None = 0,
    Failure = 1 << 0,           // Флаг ошибки (OR-семантика)

    // Причины (первая сохраняется)
    Operator = 1 << 1,
    AutoModeDisabled = 1 << 2,
    PlcForceStop = 1 << 3,
    PlcSoftReset = 1 << 4,
    PlcHardReset = 1 << 5
}
```

**Семантика RequestStop:**
```csharp
public void RequestStop(ExecutionStopFlags flags)
{
    lock (_lock)
    {
        // Failure всегда OR
        var newFailure = (flags & ExecutionStopFlags.Failure) != 0;

        // Reason: first-wins
        var newReason = flags & ~ExecutionStopFlags.Failure;
        if ((_flags & ReasonMask) == 0 && newReason != 0)
        {
            _flags |= newReason;
        }

        if (newFailure)
        {
            _flags |= ExecutionStopFlags.Failure;
        }
    }
}
```

### Decision 2: Точки интеграции SystemTrigger

| Компонент | Событие | SystemTrigger |
|-----------|---------|---------------|
| ScanModeController | `PerformInitialActivation()` | `ScanModeEnabled` |
| ScanModeController | `PerformFullDeactivation()` | `ScanModeDisabled` |
| PreExecutionCoordinator | `SubmitBarcode()` | `BarcodeReceived` |
| PreExecutionCoordinator | `StartTestExecution()` успех | `PreparationCompleted` |
| PreExecutionCoordinator | `StartTestExecution()` ошибка | `PreparationFailed` |
| TestExecutionCoordinator | `HandleTestCompleted()` | `TestFinished` |
| PlcResetCoordinator | `HandleResetAsync()` soft | `ResetRequestedSoft` |
| PlcResetCoordinator | `HandleResetAsync()` hard | `ResetRequestedHard` |
| PlcResetCoordinator | `ExecuteSmartReset()` soft | `ResetCompletedSoft` |
| PlcResetCoordinator | `ExecuteSmartReset()` hard | `ResetCompletedHard` |

### Decision 3: Замена флагов в ScanModeController

| Старый код | Новый код |
|------------|-----------|
| `_isActivated` | `_lifecycle.Phase != Idle` |
| `_isResetting` | `_lifecycle.Phase == Resetting` |
| `IsInScanningPhase` | `_lifecycle.Phase == WaitingForBarcode` |

## Risks / Trade-offs

| Риск | Митигация |
|------|-----------|
| Нарушение OR-логики | Unit-тесты для всех комбинаций RequestStop |
| Race condition в Transition | SystemLifecycleManager уже thread-safe |
| Soft/Hard reset регрессия | Тестирование обоих сценариев с проверкой barcode |
| Call sites пропущены | Grep по `StopAsFailure`, `IsStopRequested`, `StopReason` |

## Migration Plan

### Phase 1: ExecutionFlowState (изолированное)

1. Создать `ExecutionStopFlags` [Flags] enum
2. Добавить новые методы с flags-семантикой
3. Обновить call sites:
   - `TestExecutionCoordinator.Execution.cs:159`
   - `TestExecutionCoordinator.ErrorHandling.cs:138`
   - `PreExecutionCoordinator.cs:232`
   - `PreExecutionCoordinator.Retry.cs:40`
4. Удалить старые поля

### Phase 2: Интеграция Transition

1. Добавить `SystemLifecycleManager` в DI координаторов
2. Добавить Transition вызовы согласно таблице выше
3. Проверить что все переходы успешны (CanTransition)

### Phase 3: Упрощение ScanModeController

1. Добавить зависимость от `SystemLifecycleManager`
2. Заменить `_isActivated` на проверку Phase
3. Заменить `_isResetting` на проверку Phase
4. Удалить поля `_isActivated`, `_isResetting`
5. Обновить `IsInScanningPhase` property

## Чек-лист проверок

### OR-логика ExecutionFlowState

- [ ] `RequestStop(Operator, false)` → `RequestStop(PlcForceStop, true)` → reason=Operator, failure=true
- [ ] `RequestStop(Operator, true)` → `RequestStop(PlcForceStop, false)` → reason=Operator, failure=true
- [ ] `RequestStop(Operator, false)` → `RequestStop(PlcForceStop, false)` → reason=Operator, failure=false
- [ ] `ClearStop()` → flags=None

### Lifecycle transitions

- [ ] Login + AutoReady → `ScanModeEnabled` → WaitingForBarcode
- [ ] Logout → `ScanModeDisabled` → Idle
- [ ] Barcode scanned → `BarcodeReceived` → Preparing
- [ ] Pipeline success → `PreparationCompleted` → Testing
- [ ] Pipeline fail → `PreparationFailed` → WaitingForBarcode
- [ ] Test done → `TestFinished` → Completed
- [ ] Soft reset → `ResetRequestedSoft` → Resetting → `ResetCompletedSoft` → WaitingForBarcode
- [ ] Hard reset → `ResetRequestedHard` → Resetting → `ResetCompletedHard` → Idle
