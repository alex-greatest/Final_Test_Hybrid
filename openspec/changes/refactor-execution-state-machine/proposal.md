# Proposal: refactor-execution-state-machine

## Summary

Масштабный рефакторинг системы управления состоянием для достижения чистой двухуровневой архитектуры, устранения гонок состояний и упрощения внесения изменений.

**ВАЖНО:** Рефакторинг НЕ затрагивает существующий `ExecutionStateManager` и `TestExecutionCoordinator`. Создаётся **новый** уровень управления — `SystemLifecycleManager`.

## Problem Statement

### Текущие проблемы

1. **Размытые границы ответственности:**
   - `ScanModeController` управляет: режимом сканирования, сессией сканера, запуском main loop, таймингами
   - `PreExecutionCoordinator` управляет: ожиданием barcode, pipeline execution, retry logic, очисткой состояния
   - Оба класса знают о состоянии друг друга через events

2. **Дублирование состояния (нет единого источника истины):**
   - `ScanModeController._isActivated`
   - `PreExecutionCoordinator.IsAcceptingInput`
   - `ExecutionActivityTracker.IsPreExecutionActive`
   - `BoilerState.IsTestRunning`

3. **Рассинхронизация Scanner и Input Field:**
   ```csharp
   // BoilerInfo: IsFieldReadOnly проверяет 4 разных источника
   // ScanModeController: AcquireSession/ReleaseSession отдельно
   // НЕ синхронизированы напрямую!
   ```

4. **Race conditions:**
   - При сбросе PLC оба класса реагируют на события независимо
   - `_loopCts` и `_currentCts` отменяются из разных мест без гарантии порядка
   - Scanner session может быть освобождена до/после отмены input

## Proposed Solution

### Двухуровневая архитектура State Machine

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    SystemLifecycleManager (NEW)                         │
│                    Управляет ФАЗАМИ СИСТЕМЫ                             │
│  Idle → WaitingForBarcode → Preparing → Testing → Completed             │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ Phase == Testing
                                    ▼
              ┌─────────────────────────────────────────────────────┐
              │           ExecutionStateManager (EXISTS)            │
              │           Управляет ВЫПОЛНЕНИЕМ ТЕСТА               │
              │  Idle → Running ⇄ PausedOnError → Completed/Failed  │
              └─────────────────────────────────────────────────────┘
```

### Naming Convention

| Existing (NO CHANGE) | New (Proposed) | Purpose |
|----------------------|----------------|---------|
| `ExecutionState` | — | Test execution states |
| `ExecutionStateManager` | — | Test execution management |
| — | `SystemPhase` | System lifecycle phases |
| — | `SystemLifecycleManager` | System lifecycle management |

### Гарантия синхронизации Scanner ↔ Input Field

```csharp
// SystemLifecycleManager
public bool IsScannerInputEnabled => Phase == SystemPhase.WaitingForBarcode;
public bool IsScannerActive => Phase is WaitingForBarcode or Preparing;

// BoilerInfo.razor — ОДИН источник истины
private bool IsFieldReadOnly => !Lifecycle.IsScannerInputEnabled;
```

**Гарантия:** `IsScannerInputEnabled == true` ↔ Scanner session активна ↔ Input field enabled

## Scope

### In Scope

1. Создание `SystemLifecycleManager` — управление фазами системы
2. Рефакторинг `ScanModeController` — делегирование lifecycle manager
3. Рефакторинг `PreExecutionCoordinator` — интеграция с lifecycle manager
4. Упрощение UI компонентов:
   - `BoilerInfo.razor` — синхронизация scanner/input
   - `SwitchMes.razor.cs` — блокировка настроек
   - `AdminAuthorizationQr.razor.cs` — блокировка настроек
   - `OperatorAuthorizationQr.razor.cs` — блокировка настроек
5. Удаление `ExecutionActivityTracker` и `SettingsAccessStateManager`

### Out of Scope (НЕ МЕНЯЕТСЯ)

- `TestExecutionCoordinator` и все его partial files
- `ExecutionStateManager` и `ExecutionState` enum
- `ColumnExecutor`
- `ErrorCoordinator` (кроме подписок)
- `PlcResetCoordinator` (кроме вызовов transitions)

## CurrentBarcode Management

| Trigger | CurrentBarcode | Reason |
|---------|----------------|--------|
| `ScanModeDisabled` | **CLEARED** | Оператор вышел |
| `ResetCompletedHard` | **CLEARED** | Полный сброс |
| `ResetCompletedSoft` | **CLEARED** | Мягкий сброс, новый цикл |
| `TestAcknowledged` | **CLEARED** | Тест завершён, новый цикл |
| `RepeatRequested` | **PRESERVED** | Повтор теста |
| `PreparationFailed` | **PRESERVED** | Повтор подготовки |

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Регрессия в логике сброса | High | Поэтапное внедрение |
| Рассинхронизация scanner/input | High | Атомарные transitions |
| Нарушение timing | Medium | Явные guards |
| Конфликт с ExecutionStateManager | **None** | Разные уровни |

## Dependencies

- `TestExecutionCoordinator` — интеграция через `Phase == Testing`
- `PlcResetCoordinator` — вызывает `Transition(ResetRequestedHard/Soft)`
- `OperatorState` / `AutoReadySubscription` — вызывают `Transition(ScanModeEnabled/Disabled)`

## Success Criteria

1. **Единый источник истины** — `SystemLifecycleManager.Phase`
2. **Scanner ↔ Input синхронизация** — гарантирована атомарностью
3. **CurrentBarcode** — корректно очищается/сохраняется
4. **UI упрощён** — 1 подписка вместо 4 на компонент
5. **Существующий код не сломан** — TestExecutionCoordinator без изменений

## Line Count Impact

| Category | BEFORE | AFTER | Change |
|----------|--------|-------|--------|
| Backend (refactored) | 888 | 645 | -27% |
| UI Components | 659 | 450 | -32% |
| **Total** | **1547** | **1095** | **-29%** |

## Implementation Order

1. **Phase 1:** Создать `SystemLifecycleManager` (foundation)
2. **Phase 2:** Интегрировать `ScanModeController`
3. **Phase 3:** Интегрировать `PreExecutionCoordinator`
4. **Phase 4:** Упростить UI компоненты
5. **Phase 5:** Удалить устаревшие классы

## Rationale

**Почему двухуровневая архитектура:**
- `SystemLifecycleManager` — отвечает на вопрос "в какой фазе система?"
- `ExecutionStateManager` — отвечает на вопрос "что происходит внутри теста?"
- Разделение concerns, нет конфликта

**Почему `SystemPhase` вместо `ExecutionState`:**
- `ExecutionState` уже занят для test execution
- Разные доменные понятия требуют разных имён
- Избегаем путаницы при code review

**Почему гарантируем scanner/input синхронизацию:**
- Текущий код использует 4 разных флага
- Race conditions при сбросе PLC
- Один источник истины решает проблему
