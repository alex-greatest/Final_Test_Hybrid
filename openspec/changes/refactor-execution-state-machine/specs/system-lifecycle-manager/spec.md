# Spec: system-lifecycle-manager

## Overview

Спецификация для `SystemLifecycleManager` — единого источника истины для фаз жизненного цикла системы.

**ВАЖНО:** Это НОВЫЙ уровень управления, НЕ замена `ExecutionStateManager`. Два уровня:
- `SystemLifecycleManager` — управляет фазами системы (Idle → Testing)
- `ExecutionStateManager` (EXISTS) — управляет состояниями внутри теста (Running ⇄ PausedOnError)

---

## ADDED Requirements

### Requirement: System Phase States

Система должна поддерживать следующие фазы жизненного цикла:

| Phase | Description |
|-------|-------------|
| `Idle` | Система неактивна (оператор не авторизован или AutoReady выключен) |
| `WaitingForBarcode` | Ожидание сканирования штрихкода |
| `Preparing` | Выполнение подготовки (ScanStep, BlockBoilerAdapter) |
| `Testing` | Выполнение тестовых шагов |
| `Completed` | Тест завершён, ожидание подтверждения |
| `Resetting` | Выполняется сброс по сигналу PLC |

#### Scenario: Initial phase
**Given** система только запущена
**When** SystemLifecycleManager создан
**Then** Phase = Idle

#### Scenario: Phase transitions are logged
**Given** SystemLifecycleManager в любой фазе
**When** происходит любой transition
**Then** логируется: previous phase, trigger, new phase

---

### Requirement: Transition Table

Система должна разрешать только определённые переходы между фазами:

| From | Trigger | To |
|------|---------|-----|
| Idle | ScanModeEnabled | WaitingForBarcode |
| WaitingForBarcode | ScanModeDisabled | Idle |
| WaitingForBarcode | BarcodeReceived | Preparing |
| Preparing | PreparationCompleted | Testing |
| Preparing | PreparationFailed | WaitingForBarcode |
| Testing | TestFinished | Completed |
| Completed | RepeatRequested | WaitingForBarcode |
| Completed | TestAcknowledged | WaitingForBarcode |
| * (except Idle) | ResetRequestedHard | Resetting |
| * (except Idle) | ResetRequestedSoft | Resetting |
| Resetting | ResetCompleted | Idle (hard) or WaitingForBarcode (soft) |

#### Scenario: Valid transition succeeds
**Given** SystemLifecycleManager в фазе Idle
**When** вызывается Transition(ScanModeEnabled)
**Then** Phase = WaitingForBarcode
**And** возвращается true

#### Scenario: Invalid transition fails
**Given** SystemLifecycleManager в фазе Idle
**When** вызывается Transition(BarcodeReceived)
**Then** Phase остаётся Idle
**And** возвращается false
**And** срабатывает OnTransitionFailed event

#### Scenario: Reset from any active phase
**Given** SystemLifecycleManager в фазе Testing
**When** вызывается Transition(ResetRequestedHard)
**Then** Phase = Resetting

#### Scenario: Reset from Idle is ignored
**Given** SystemLifecycleManager в фазе Idle
**When** вызывается Transition(ResetRequestedHard)
**Then** Phase остаётся Idle
**And** возвращается false

---

### Requirement: Thread Safety

SystemLifecycleManager должен быть thread-safe для concurrent access.

#### Scenario: Concurrent transitions
**Given** SystemLifecycleManager в фазе WaitingForBarcode
**When** одновременно вызываются Transition(BarcodeReceived) и Transition(ResetRequestedHard)
**Then** только один transition выполняется успешно
**And** нет data corruption

#### Scenario: Event notification outside lock
**Given** SystemLifecycleManager с подписчиком на OnPhaseChanged
**When** выполняется transition
**Then** OnPhaseChanged вызывается вне критической секции
**And** подписчик может безопасно вызывать другие методы SystemLifecycleManager

---

### Requirement: Phase Change Notification

SystemLifecycleManager должен уведомлять подписчиков об изменении фазы.

#### Scenario: OnPhaseChanged event
**Given** подписчик на OnPhaseChanged
**When** выполняется успешный transition Idle → WaitingForBarcode
**Then** OnPhaseChanged вызывается с (Idle, WaitingForBarcode)

#### Scenario: No notification on failed transition
**Given** подписчик на OnPhaseChanged
**When** выполняется неуспешный transition (Idle → BarcodeReceived)
**Then** OnPhaseChanged НЕ вызывается

---

### Requirement: Query Methods and Properties

SystemLifecycleManager должен предоставлять методы и свойства для query состояния.

#### Scenario: CanTransition check
**Given** SystemLifecycleManager в фазе Idle
**When** вызывается CanTransition(ScanModeEnabled)
**Then** возвращается true

#### Scenario: CanTransition negative check
**Given** SystemLifecycleManager в фазе Idle
**When** вызывается CanTransition(BarcodeReceived)
**Then** возвращается false

#### Scenario: IsScannerActive property
**Given** SystemLifecycleManager в фазе WaitingForBarcode
**When** проверяется IsScannerActive
**Then** возвращается true

**Given** SystemLifecycleManager в фазе Preparing
**When** проверяется IsScannerActive
**Then** возвращается true

**Given** SystemLifecycleManager в фазе Testing
**When** проверяется IsScannerActive
**Then** возвращается false

#### Scenario: IsScannerInputEnabled property
**Given** SystemLifecycleManager в фазе WaitingForBarcode
**When** проверяется IsScannerInputEnabled
**Then** возвращается true

**Given** SystemLifecycleManager в фазе Preparing
**When** проверяется IsScannerInputEnabled
**Then** возвращается false

#### Scenario: CanInteractWithSettings property
**Given** SystemLifecycleManager в фазе Idle
**When** проверяется CanInteractWithSettings
**Then** возвращается true

**Given** SystemLifecycleManager в фазе WaitingForBarcode
**When** проверяется CanInteractWithSettings
**Then** возвращается true

**Given** SystemLifecycleManager в фазе Testing
**When** проверяется CanInteractWithSettings
**Then** возвращается false

---

### Requirement: Scanner-Input Synchronization

Scanner session и Input field должны быть синхронизированы атомарно.

#### Scenario: Input field reflects scanner state
**Given** SystemLifecycleManager управляет scanner session
**When** Phase == WaitingForBarcode
**Then** Scanner session активна AND Input field enabled

**When** Phase != WaitingForBarcode
**Then** Input field disabled (может быть активна при Preparing для scan loop)

#### Scenario: Atomic synchronization
**Given** происходит transition из WaitingForBarcode в Preparing
**When** transition выполнен
**Then** Input field disabled ОДНОВРЕМЕННО с изменением Phase
**And** нет момента рассинхронизации

---

### Requirement: CurrentBarcode Lifecycle

SystemLifecycleManager должен управлять CurrentBarcode с правильным жизненным циклом.

#### Scenario: Barcode set on scan
**Given** SystemLifecycleManager в фазе WaitingForBarcode
**When** вызывается Transition(BarcodeReceived, barcode: "12345")
**Then** CurrentBarcode = "12345"

#### Scenario: Barcode cleared on logout
**Given** CurrentBarcode = "12345"
**When** вызывается Transition(ScanModeDisabled)
**Then** CurrentBarcode = null

#### Scenario: Barcode cleared on hard reset
**Given** CurrentBarcode = "12345"
**When** вызывается Transition(ResetRequestedHard)
**Then** CurrentBarcode = null (после ResetCompleted)

#### Scenario: Barcode preserved on soft reset
**Given** CurrentBarcode = "12345"
**When** вызывается Transition(ResetRequestedSoft)
**Then** CurrentBarcode = "12345" (после ResetCompleted)

#### Scenario: Barcode preserved on repeat
**Given** CurrentBarcode = "12345", Phase = Completed
**When** вызывается Transition(RepeatRequested)
**Then** CurrentBarcode = "12345"

---

## MODIFIED Requirements

### Requirement: ScanModeController uses SystemLifecycleManager

ScanModeController должен использовать SystemLifecycleManager вместо внутренних флагов.

#### Scenario: Scan mode enabled
**Given** оператор авторизован И AutoReady включен
**When** UpdateScanModeState вызывается
**Then** вызывается Lifecycle.Transition(ScanModeEnabled)
**And** scanner session приобретается на основе Lifecycle.IsScannerActive

#### Scenario: Scan mode disabled
**Given** оператор разлогинился
**When** UpdateScanModeState вызывается
**Then** вызывается Lifecycle.Transition(ScanModeDisabled)
**And** scanner session освобождается на основе Lifecycle.IsScannerActive

---

### Requirement: PreExecutionCoordinator uses SystemLifecycleManager

PreExecutionCoordinator должен использовать SystemLifecycleManager для координации.

#### Scenario: Barcode received
**Given** SystemLifecycleManager в фазе WaitingForBarcode
**When** barcode отсканирован
**Then** вызывается Lifecycle.Transition(BarcodeReceived, barcode)
**And** начинается подготовка

#### Scenario: Preparation completed
**Given** SystemLifecycleManager в фазе Preparing
**When** ScanStep и BlockBoilerAdapter выполнены успешно
**Then** вызывается Lifecycle.Transition(PreparationCompleted)
**And** запускается TestExecutionCoordinator

---

### Requirement: UI Components use SystemLifecycleManager

UI компоненты должны использовать единый источник истины.

#### Scenario: BoilerInfo.razor
**Given** BoilerInfo.razor отображается
**When** Lifecycle.IsScannerInputEnabled изменяется
**Then** Input field readonly state обновляется синхронно

#### Scenario: Settings components
**Given** SwitchMes/AdminAuthorizationQr/OperatorAuthorizationQr отображаются
**When** Lifecycle.CanInteractWithSettings изменяется
**Then** Disabled state обновляется синхронно

---

## REMOVED Requirements

### Requirement: ExecutionActivityTracker

`ExecutionActivityTracker` удаляется — его функционал заменяется `SystemLifecycleManager`.

Mapping:
- `IsPreExecutionActive` → `Phase == Preparing`
- `IsTestExecutionActive` → `Phase == Testing`
- `IsAnyActive` → `Phase != Idle && Phase != Completed`

### Requirement: SettingsAccessStateManager

`SettingsAccessStateManager` удаляется — его функционал заменяется `SystemLifecycleManager`.

Mapping:
- `CanInteractWithSettings` → `Lifecycle.CanInteractWithSettings`

---

## Related Capabilities

- **ScanModeController**: Интегрируется с SystemLifecycleManager для управления scanner session
- **PreExecutionCoordinator**: Интегрируется с SystemLifecycleManager для координации pipeline
- **PlcResetCoordinator**: Вызывает Lifecycle.Transition(ResetRequestedHard/Soft)
- **TestExecutionCoordinator**: НЕ МЕНЯЕТСЯ — использует существующий ExecutionStateManager
- **BoilerInfo.razor**: Использует Lifecycle.IsScannerInputEnabled
- **SwitchMes/AdminAuthorizationQr/OperatorAuthorizationQr**: Используют Lifecycle.CanInteractWithSettings
