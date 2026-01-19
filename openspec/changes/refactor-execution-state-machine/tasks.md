# Tasks: refactor-execution-state-machine

## Phase 1: Create SystemLifecycleManager (Foundation)

### 1.1 Create SystemPhase and SystemTrigger enums
- [x] Create `SystemPhase.cs` with states: Idle, WaitingForBarcode, Preparing, Testing, Completed, Resetting
- [x] Create `SystemTrigger.cs` with triggers: ScanModeEnabled, ScanModeDisabled, BarcodeReceived, PreparationCompleted, PreparationFailed, TestFinished, RepeatRequested, ResetRequestedHard, ResetRequestedSoft, ResetCompleted
- [x] Add XML documentation for each state/trigger

**Validation:** ✅ Файлы компилируются без ошибок

### 1.2 Implement SystemLifecycleManager
- [x] Create `SystemLifecycleManager.cs` with thread-safe phase transitions
- [x] Implement transition table (allowed phase changes)
- [x] Add `OnPhaseChanged` event with old/new phase
- [x] Add `OnTransitionFailed` event for debugging
- [x] Implement `CanTransition(trigger)` method
- [x] Add convenience properties: `IsScannerActive`, `IsScannerInputEnabled`, `CanInteractWithSettings`
- [x] Add `CurrentBarcode` management with lifecycle rules
- [x] Add DualLogger integration

**Validation:** ✅ Сборка проходит успешно

### 1.3 Register in DI
- [x] Add `SystemLifecycleManager` as singleton in `StepsServiceExtensions`
- [x] Verify resolution works

**Validation:** ✅ Приложение собирается, SystemLifecycleManager зарегистрирован

---

## Phase 2: Integrate ScanModeController

### 2.1 Inject SystemLifecycleManager
- [ ] Add `SystemLifecycleManager` dependency to constructor
- [ ] Keep existing logic working (dual state tracking temporarily)

**Validation:** Приложение работает как раньше

### 2.2 Replace _isActivated
- [ ] Subscribe to `Lifecycle.OnPhaseChanged`
- [ ] Replace `_isActivated` reads with phase checks
- [ ] Call `Lifecycle.Transition(ScanModeEnabled/Disabled)` instead of setting flag
- [ ] Remove `_isActivated` field

**Validation:** Scan mode активируется/деактивируется корректно

### 2.3 Replace _isResetting
- [ ] Replace `_isResetting` reads with `Phase == Resetting` check
- [ ] Remove `HandleResetStarting` → use `Lifecycle.OnPhaseChanged`
- [ ] Remove `HandleResetCompleted` → use `Lifecycle.OnPhaseChanged`
- [ ] Remove `_isResetting` field

**Validation:** PLC Reset работает корректно

### 2.4 Simplify scanner session management
- [ ] Move `AcquireSession`/`ReleaseSession` to `OnPhaseChanged` handler
- [ ] Ensure scanner active when `Lifecycle.IsScannerActive == true`
- [ ] Remove duplicate session management from `TryActivateScanMode`/`TryDeactivateScanMode`

**Validation:** Scanner включается/выключается в правильных состояниях

---

## Phase 3: Refactor PreExecutionCoordinator

### 3.1 Extract ExecutionLoopManager
- [ ] Create `ExecutionLoopManager.cs`
- [ ] Move `StartMainLoopAsync` logic
- [ ] Move `_currentCts` management
- [ ] Move `WaitForBarcodeAsync`
- [ ] Integrate with SystemLifecycleManager for phase transitions

**Validation:** Main loop работает через новый класс

### 3.2 Extract PreExecutionPipeline
- [ ] Create `PreExecutionPipeline.cs`
- [ ] Move `ExecutePreExecutionPipelineAsync`
- [ ] Move `ExecuteRepeatPipelineAsync`
- [ ] Move `ExecuteNokRepeatPipelineAsync`
- [ ] Move `ExecuteScanStepAsync`, `ExecuteBlockBoilerAdapterAsync`
- [ ] Remove state flag dependencies (get phase from SystemLifecycleManager)

**Validation:** Подготовка теста работает через новый класс

### 3.3 Refactor RetryCoordinator
- [ ] Rename `PreExecutionCoordinator.Retry.cs` → extract to separate class
- [ ] Remove event subscriptions (OnForceStop, OnAskEndReceived, OnReset)
- [ ] Keep only `ExecuteRetryLoopAsync` and `WaitForResolutionAsync`
- [ ] Inject dependencies instead of accessing through `coordinators`

**Validation:** Retry/Skip логика работает

### 3.4 Update PreExecutionCoordinator
- [ ] Convert to orchestrator that composes: LoopManager, Pipeline, RetryCoordinator
- [ ] Remove partial classes (now separate classes)
- [ ] Simplify to ~100 lines

**Validation:** Полный flow работает: scan → prepare → test → complete

---

## Phase 4: Cleanup and Consolidation

### 4.1 Remove ExecutionActivityTracker
- [ ] Update all usages to use `SystemLifecycleManager.Phase`
- [ ] Remove from DI registration
- [ ] Delete file

**Validation:** Нет ссылок на ExecutionActivityTracker

### 4.2 Consolidate Clear methods
- [ ] Create `ExecutionCleanupService` for state cleanup
- [ ] Move `ClearForTestCompletion`, `ClearStateOnReset`, `ClearForRepeat` etc.
- [ ] Trigger cleanup based on phase transitions

**Validation:** Очистка состояния работает корректно

### 4.3 Simplify PlcResetCoordinator integration
- [ ] Change from multiple events to single `Lifecycle.Transition(ResetRequestedHard/Soft)`
- [ ] Remove OnResetStarting, OnForceStop from PreExecutionCoordinator subscriptions
- [ ] Keep OnAskEndReceived for grid clearing only

**Validation:** PLC Reset flow работает через SystemLifecycleManager

### 4.4 Final code review
- [ ] Remove unused code
- [ ] Update XML documentation
- [ ] Verify logging covers all transitions
- [ ] Check for potential race conditions

**Validation:** Code review пройден

---

## Phase 5: Simplify UI Components

### 5.1 Update BoilerInfo.razor
- [ ] Inject `SystemLifecycleManager` instead of 4 separate services
- [ ] Replace `IsFieldReadOnly` with `!Lifecycle.IsScannerInputEnabled`
- [ ] **CRITICAL:** Ensure `IsFieldReadOnly` is synchronized with scanner session state
- [ ] Replace 4 event subscriptions with single `Lifecycle.OnPhaseChanged`
- [ ] Remove unused injected services
- [ ] Update Dispose to single unsubscription

**Validation:**
- Barcode input field enables/disables correctly
- Input field is enabled ↔ Scanner session is active ↔ `Phase == WaitingForBarcode`

### 5.2 Update SwitchMes.razor.cs
- [ ] Inject `SystemLifecycleManager`
- [ ] Replace `IsDisabled` with `!Lifecycle.CanInteractWithSettings`
- [ ] Replace 4 event subscriptions with single `Lifecycle.OnPhaseChanged`
- [ ] Remove `PreExecution`, `SettingsAccessState`, `PlcResetCoordinator`, `ErrorCoordinator` injections

**Validation:** MES switch enables/disables correctly

### 5.3 Update AdminAuthorizationQr.razor.cs
- [ ] Same pattern as SwitchMes
- [ ] Inject `SystemLifecycleManager`
- [ ] Replace `IsDisabled` logic with `!Lifecycle.CanInteractWithSettings`
- [ ] Replace 4 event subscriptions with single

**Validation:** Admin QR checkbox enables/disables correctly

### 5.4 Update OperatorAuthorizationQr.razor.cs
- [ ] Same pattern as SwitchMes
- [ ] Inject `SystemLifecycleManager`
- [ ] Replace `IsDisabled` logic with `!Lifecycle.CanInteractWithSettings`
- [ ] Replace 4 event subscriptions with single

**Validation:** Operator QR checkbox enables/disables correctly

### 5.5 Remove obsolete managers
- [ ] Delete `SettingsAccessStateManager.cs`
- [ ] Remove from DI registration
- [ ] Verify no remaining references

**Validation:** Build succeeds, no runtime errors

---

## Dependencies

```
Phase 1 (Foundation)
    │
    ▼
Phase 2 (ScanModeController)
    │
    ▼
Phase 3 (PreExecutionCoordinator)
    │
    ├──► Phase 4 (Cleanup)
    │
    └──► Phase 5 (UI Components)  ← Can start after Phase 3
```

**Parallelizable:**
- 1.1 и 1.2 могут выполняться параллельно
- 3.1, 3.2, 3.3 могут выполняться параллельно после 2.4
- 4.1, 4.2, 4.3 могут выполняться параллельно после 3.4
- 5.1, 5.2, 5.3, 5.4 могут выполняться параллельно после Phase 3
- **Phase 4 и Phase 5 могут выполняться параллельно**

---

## CurrentBarcode Lifecycle

| Trigger | CurrentBarcode | Reason |
|---------|----------------|--------|
| `ScanModeDisabled` | **CLEARED** | Оператор вышел |
| `ResetRequestedHard` | **CLEARED** | Полный сброс |
| `ResetRequestedSoft` | **PRESERVED** | Мягкий сброс |
| `RepeatRequested` | **PRESERVED** | Повтор теста |
| `PreparationFailed` | **PRESERVED** | Повтор подготовки |

---

## Estimated Effort

| Phase | Tasks | Complexity | Lines Changed |
|-------|-------|------------|---------------|
| Phase 1 | 3 | Low | +255 (new files) |
| Phase 2 | 4 | Medium | -150 (simplify) |
| Phase 3 | 4 | High | ~0 (refactor) |
| Phase 4 | 4 | Medium | -138 (delete) |
| Phase 5 | 5 | Low | -209 (simplify) |

**Total Estimated:**
- Lines removed: ~497
- Lines added: ~255
- **Net reduction: ~242 lines (-27%)**
