# Design: refactor-execution-state-machine

## Overview

Документ описывает архитектурное решение для рефакторинга системы управления состоянием выполнения тестов.

**ВАЖНО:** В системе уже существует `ExecutionStateManager` с `ExecutionState` enum для управления фазой выполнения теста (Running, PausedOnError). Наш рефакторинг создаёт **ОТДЕЛЬНЫЙ** state machine для управления жизненным циклом системы на более высоком уровне.

---

## Two-Level State Machine Architecture

### Уровень 1: SystemLifecycleManager (NEW)

Управляет **фазами системы** — от авторизации оператора до завершения теста:

```
Idle → WaitingForBarcode → Preparing → Testing → Completed → WaitingForBarcode
                              ↑                       │
                              └───────────────────────┘ (Repeat)
```

### Уровень 2: ExecutionStateManager (EXISTS)

Управляет **выполнением теста** — внутри фазы `Testing`:

```
Idle → Running ⇄ PausedOnError → Completed/Failed
```

### Взаимодействие уровней

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    SystemLifecycleManager (NEW)                         │
│  ┌──────┐  ScanMode   ┌─────────────────┐  Barcode  ┌───────────┐      │
│  │ Idle │ ──────────► │WaitingForBarcode│ ────────► │ Preparing │      │
│  └──────┘   Enabled   └─────────────────┘  Received └─────┬─────┘      │
│      ▲                         ▲                          │            │
│      │ ScanMode                │ Repeat                   │ Completed  │
│      │ Disabled                │ Requested                ▼            │
│      │                  ┌──────┴─────┐           ┌───────────────┐     │
│      │                  │ Completed  │ ◄──────── │    Testing    │     │
│      │                  └────────────┘  Finished └───────┬───────┘     │
│      │                                                   │             │
│      │                         ┌───────────────┐         │             │
│      └─────────────────────────│   Resetting   │◄────────┘             │
│           ResetCompleted(hard) └───────────────┘  ResetRequested       │
└─────────────────────────────────────────────────────────────────────────┘
                                        │
                                        │ Phase == Testing
                                        ▼
              ┌─────────────────────────────────────────────────────┐
              │           ExecutionStateManager (EXISTS)            │
              │  ┌──────┐      ┌─────────┐      ┌─────────────────┐│
              │  │ Idle │ ───► │ Running │ ◄──► │  PausedOnError  ││
              │  └──────┘      └────┬────┘      └─────────────────┘│
              │                     │                              │
              │                     ▼                              │
              │            ┌──────────────────┐                    │
              │            │ Completed/Failed │                    │
              │            └──────────────────┘                    │
              └─────────────────────────────────────────────────────┘
```

---

## Current Architecture Analysis

### Компоненты и их текущие обязанности

#### ScanModeController (250 строк)
```
Responsibilities:
├── Управление режимом сканирования (_isActivated)
├── Управление сессией сканера (ScanSessionManager)
├── Запуск/остановка main loop (_loopCts)
├── Обработка событий PlcReset
├── Управление таймингами (StepTimingService)
└── Уведомление подписчиков (OnStateChanged)

Dependencies:
├── ScanSessionManager
├── OperatorState
├── AutoReadySubscription
├── StepStatusReporter
├── PreExecutionCoordinator ←── сильная связь
├── PlcResetCoordinator
├── StepTimingService
└── ExecutionActivityTracker
```

#### PreExecutionCoordinator (~500 строк, 4 partial files)
```
Responsibilities:
├── Main loop (MainLoop.cs)
│   ├── Цикл ожидания barcode
│   ├── Управление _currentCts
│   └── HandleCycleExit
├── Pipeline (Pipeline.cs)
│   ├── ExecutePreExecutionPipelineAsync
│   ├── ExecuteRepeatPipelineAsync
│   └── ExecuteNokRepeatPipelineAsync
├── Retry (Retry.cs)
│   ├── Подписки на события сброса ←── плохое место
│   ├── ExecuteRetryLoopAsync
│   └── WaitForResolutionAsync
└── Base (.cs)
    ├── Очистка состояния (Clear* методы)
    ├── SubmitBarcode
    └── State flags (IsAcceptingInput, CurrentBarcode)

Dependencies:
├── PreExecutionSteps
├── PreExecutionInfrastructure
├── PreExecutionCoordinators
│   ├── TestExecutionCoordinator
│   ├── ErrorCoordinator
│   ├── PlcResetCoordinator
│   └── ScanDialogCoordinator
└── PreExecutionState
    ├── BoilerState
    ├── ExecutionActivityTracker
    └── ExecutionPhaseState
```

### Проблемы текущей архитектуры

#### 1. Split Brain при сбросе

```
Timeline при PLC Reset:
─────────────────────────────────────────────────────────────►
     │                    │                    │
     │ OnResetStarting    │ OnForceStop       │ OnResetCompleted
     │       ↓            │       ↓           │       ↓
     │ ScanModeController │ PreExecution      │ ScanModeController
     │ _isResetting=true  │ _pendingExitReason│ _isResetting=false
     │ ReleaseSession()   │ _currentCts.Cancel│ AcquireSession()
     │                    │                    │
     │                    │ ← race condition → │

Проблема: Порядок ReleaseSession и Cancel не определён.
UI может показывать поле ввода когда scanner session уже отпущена.
```

#### 2. Дублирование состояния

```csharp
// Все эти флаги должны быть согласованы:
ScanModeController._isActivated           // Scan mode ON
PreExecutionCoordinator.IsAcceptingInput  // Waiting for barcode
ExecutionActivityTracker.IsPreExecutionActive // PreExecution running
BoilerState.IsTestRunning                 // Test in progress

// Но управляются из разных мест!
```

#### 3. Рассинхронизация Scanner и Input Field

```csharp
// BoilerInfo.razor - текущая логика
private bool IsFieldReadOnly => !PreExecution.IsAcceptingInput
    || !IsOnActiveScanStep
    || PlcResetCoordinator.IsActive
    || ErrorCoordinator.CurrentInterrupt != null;

// ScanModeController - управление сканером
_sessionManager.AcquireSession()  // Сканер активен
_sessionManager.ReleaseSession()  // Сканер выключен

// ПРОБЛЕМА: Эти два состояния не синхронизированы напрямую!
```

---

## Proposed Architecture

### Naming Convention (избегаем конфликта с ExecutionState)

| Existing | New (Proposed) | Purpose |
|----------|----------------|---------|
| `ExecutionState` | — | Test execution states (EXISTS, NO CHANGE) |
| `ExecutionStateManager` | — | Test execution management (EXISTS, NO CHANGE) |
| — | `SystemPhase` | System lifecycle phases (NEW) |
| — | `SystemLifecycleManager` | System lifecycle management (NEW) |

### SystemPhase Enum

```csharp
/// <summary>
/// Фазы жизненного цикла системы.
/// НЕ путать с ExecutionState (состояния выполнения теста).
/// </summary>
public enum SystemPhase
{
    /// <summary>
    /// Система неактивна. Оператор не авторизован или AutoReady выключен.
    /// Scanner: OFF, Input: DISABLED
    /// </summary>
    Idle,

    /// <summary>
    /// Ожидание штрихкода. Scanner session активна.
    /// Scanner: ON, Input: ENABLED
    /// </summary>
    WaitingForBarcode,

    /// <summary>
    /// Выполнение подготовки (ScanStep, BlockBoilerAdapter).
    /// Scanner: ON (для retry), Input: DISABLED
    /// </summary>
    Preparing,

    /// <summary>
    /// Выполнение тестовых шагов. TestExecutionCoordinator активен.
    /// Scanner: OFF, Input: DISABLED
    /// </summary>
    Testing,

    /// <summary>
    /// Тест завершён. Ожидание подтверждения/повтора.
    /// Scanner: OFF, Input: DISABLED
    /// </summary>
    Completed,

    /// <summary>
    /// Выполняется сброс по сигналу PLC.
    /// Scanner: OFF, Input: DISABLED
    /// </summary>
    Resetting
}
```

### State-to-Feature Mapping (КРИТИЧЕСКИ ВАЖНО)

| SystemPhase | Scanner | Input Field | Settings | CurrentBarcode |
|-------------|---------|-------------|----------|----------------|
| `Idle` | OFF | DISABLED | ENABLED | null |
| `WaitingForBarcode` | **ON** | **ENABLED** | ENABLED | null or previous |
| `Preparing` | ON* | DISABLED | DISABLED | set |
| `Testing` | OFF | DISABLED | DISABLED | set |
| `Completed` | OFF | DISABLED | DISABLED | set |
| `Resetting` | OFF | DISABLED | DISABLED | cleared on hard |

*Scanner ON during Preparing только для повторного сканирования при ошибке

### SystemLifecycleManager Design

```csharp
public class SystemLifecycleManager : IDisposable
{
    private readonly Lock _lock = new();
    private SystemPhase _phase = SystemPhase.Idle;
    private string? _currentBarcode;

    // === State ===
    public SystemPhase Phase { get { lock (_lock) return _phase; } }
    public string? CurrentBarcode { get { lock (_lock) return _currentBarcode; } }

    // === Derived Properties for UI ===

    /// <summary>
    /// Сканер должен быть активен (Session acquired).
    /// Синхронизирован с IsScannerInputEnabled.
    /// </summary>
    public bool IsScannerActive => Phase is SystemPhase.WaitingForBarcode or SystemPhase.Preparing;

    /// <summary>
    /// Поле ввода штрихкода должно быть активно.
    /// ВСЕГДА синхронизировано с IsScannerActive для WaitingForBarcode.
    /// </summary>
    public bool IsScannerInputEnabled => Phase == SystemPhase.WaitingForBarcode;

    /// <summary>
    /// Настройки (SwitchMes, QR Auth) могут быть изменены.
    /// </summary>
    public bool CanInteractWithSettings => Phase is SystemPhase.Idle or SystemPhase.WaitingForBarcode;

    /// <summary>
    /// Система заблокирована (тест выполняется или сброс).
    /// </summary>
    public bool IsBlocked => Phase is SystemPhase.Preparing
                          or SystemPhase.Testing
                          or SystemPhase.Completed
                          or SystemPhase.Resetting;

    // === Events ===
    public event Action<SystemPhase, SystemPhase>? OnPhaseChanged;

    // === Transitions ===
    public bool Transition(SystemTrigger trigger);
    public bool Transition(SystemTrigger trigger, string? barcode);
}
```

### SystemTrigger Enum

```csharp
public enum SystemTrigger
{
    // Activation
    ScanModeEnabled,        // Operator authenticated + AutoReady
    ScanModeDisabled,       // Operator logged out or AutoReady off

    // Barcode Flow
    BarcodeReceived,        // Barcode scanned/entered
    PreparationCompleted,   // ScanStep + BlockBoilerAdapter done
    PreparationFailed,      // Pipeline failed, return to scanning

    // Test Flow
    TestFinished,           // TestExecutionCoordinator completed
    RepeatRequested,        // Operator requested repeat
    TestAcknowledged,       // Operator acknowledged result

    // Reset Flow
    ResetRequestedHard,     // PLC Reset signal (hard reset)
    ResetRequestedSoft,     // PLC Reset signal (soft reset)
    ResetCompletedSoft,     // Soft reset → WaitingForBarcode
    ResetCompletedHard      // Hard reset → Idle
}
```

### Transition Table

```
┌───────────────────┬────────────────────┬───────────────────┬─────────────────────┐
│ From              │ Trigger            │ To                │ Side Effects        │
├───────────────────┼────────────────────┼───────────────────┼─────────────────────┤
│ Idle              │ ScanModeEnabled    │ WaitingForBarcode │ AcquireScanner      │
│ WaitingForBarcode │ ScanModeDisabled   │ Idle              │ ReleaseScanner,     │
│                   │                    │                   │ ClearBarcode        │
│ WaitingForBarcode │ BarcodeReceived    │ Preparing         │ SetBarcode          │
│ Preparing         │ PreparationCompleted│ Testing          │ ReleaseScanner      │
│ Preparing         │ PreparationFailed  │ WaitingForBarcode │ —                   │
│ Testing           │ TestFinished       │ Completed         │ —                   │
│ Completed         │ RepeatRequested    │ WaitingForBarcode │ AcquireScanner      │
│ Completed         │ TestAcknowledged   │ WaitingForBarcode │ AcquireScanner,     │
│                   │                    │                   │ ClearBarcode        │
│ *                 │ ResetRequestedHard │ Resetting         │ ReleaseScanner      │
│ *                 │ ResetRequestedSoft │ Resetting         │ ReleaseScanner      │
│ Resetting         │ ResetCompletedSoft │ WaitingForBarcode │ AcquireScanner,     │
│                   │                    │                   │ ClearBarcode        │
│ Resetting         │ ResetCompletedHard │ Idle              │ ClearBarcode        │
└───────────────────┴────────────────────┴───────────────────┴─────────────────────┘

* = any phase except Idle and Resetting
```

---

## UI Components Integration

### BoilerInfo.razor — ГАРАНТИЯ СИНХРОНИЗАЦИИ

**Текущая проблема:**
```csharp
// 4 разных источника, не синхронизированы со сканером
private bool IsFieldReadOnly => !PreExecution.IsAcceptingInput
    || !IsOnActiveScanStep
    || PlcResetCoordinator.IsActive
    || ErrorCoordinator.CurrentInterrupt != null;
```

**После рефакторинга:**
```csharp
@inject SystemLifecycleManager Lifecycle

// ОДИН источник истины, ГАРАНТИРОВАННО синхронизирован со сканером
private bool IsFieldReadOnly => !Lifecycle.IsScannerInputEnabled;

// Значение поля
private string GetDisplayValue() =>
    BoilerState.SerialNumber ?? Lifecycle.CurrentBarcode ?? _manualInput;
```

**Гарантия синхронизации:**
```
SystemLifecycleManager.IsScannerInputEnabled == true
    ↔ SystemLifecycleManager.Phase == WaitingForBarcode
    ↔ ScanSessionManager.HasActiveSession == true
    ↔ BoilerInfo input field is ENABLED
```

Эта связь **атомарна** — все три состояния меняются в одной транзакции внутри `SystemLifecycleManager.Transition()`.

### SwitchMes, AdminAuthorizationQr, OperatorAuthorizationQr

**Текущая проблема:**
```csharp
// Одинаковая логика в 3 компонентах, 4 подписки на события
private bool IsDisabled => PreExecution.IsProcessing
    || !SettingsAccessState.CanInteract
    || PlcResetCoordinator.IsActive
    || ErrorCoordinator.CurrentInterrupt != null;
```

**После рефакторинга:**
```csharp
@inject SystemLifecycleManager Lifecycle

// ОДИН источник истины
private bool IsDisabled => !Lifecycle.CanInteractWithSettings;
```

### Event Subscription Simplification

**BEFORE (per component):**
```csharp
protected override void OnInitialized()
{
    PreExecution.OnStateChanged += HandleStateChanged;
    SettingsAccessState.OnStateChanged += HandleStateChanged;
    PlcResetCoordinator.OnActiveChanged += HandleStateChanged;
    ErrorCoordinator.OnInterruptChanged += HandleStateChanged;
}

public void Dispose()
{
    PreExecution.OnStateChanged -= HandleStateChanged;
    SettingsAccessState.OnStateChanged -= HandleStateChanged;
    PlcResetCoordinator.OnActiveChanged -= HandleStateChanged;
    ErrorCoordinator.OnInterruptChanged -= HandleStateChanged;
}
```

**AFTER (per component):**
```csharp
protected override void OnInitialized()
{
    Lifecycle.OnPhaseChanged += HandlePhaseChanged;
}

public void Dispose()
{
    Lifecycle.OnPhaseChanged -= HandlePhaseChanged;
}
```

---

## CurrentBarcode Management

### Жизненный цикл CurrentBarcode

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         CurrentBarcode Lifecycle                         │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  WaitingForBarcode ───────────────────────────────────────────────────►  │
│       │                                                                  │
│       │ BarcodeReceived(barcode)                                         │
│       │ CurrentBarcode = barcode ◄─────────────────────────────────────┐ │
│       ▼                                                                │ │
│  Preparing ─────────────────────────────────────────────────────────►  │ │
│       │                                                                │ │
│       │ PreparationCompleted                                           │ │
│       ▼                                                                │ │
│  Testing ───────────────────────────────────────────────────────────►  │ │
│       │                                                                │ │
│       │ TestFinished                                                   │ │
│       ▼                                                                │ │
│  Completed ─────────────────────────────────────────────────────────►  │ │
│       │                                                                │ │
│       ├─ RepeatRequested ──────────────────────────────────────────────┘ │
│       │  (CurrentBarcode PRESERVED for repeat)                           │
│       │                                                                  │
│       └─ ScanModeDisabled / ResetCompletedHard ──────────────────────►  │
│          CurrentBarcode = null                                           │
│                                                                          │
│  Resetting ─────────────────────────────────────────────────────────►   │
│       │                                                                  │
│       ├─ ResetCompletedSoft                                              │
│       │  CurrentBarcode PRESERVED (мягкий сброс)                         │
│       │                                                                  │
│       └─ ResetCompletedHard                                              │
│          CurrentBarcode = null (жёсткий сброс)                           │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

### Clearing Logic

| Trigger | CurrentBarcode | Reason |
|---------|----------------|--------|
| `ScanModeDisabled` | **CLEARED** | Оператор вышел, начинаем заново |
| `ResetCompletedHard` | **CLEARED** | Полный сброс, всё очищается |
| `ResetCompletedSoft` | **CLEARED** | Мягкий сброс, новый цикл |
| `TestAcknowledged` | **CLEARED** | Тест завершён, новый цикл |
| `RepeatRequested` | **PRESERVED** | Повтор с тем же штрихкодом |
| `PreparationFailed` | **PRESERVED** | Можно повторить подготовку |

---

## File Structure (After Refactoring)

```
Services/Steps/Infrastructure/Execution/
├── Lifecycle/                          (NEW folder)
│   ├── SystemLifecycleManager.cs       (NEW ~180 lines)
│   ├── SystemPhase.cs                  (NEW ~30 lines)
│   ├── SystemTrigger.cs                (NEW ~25 lines)
│   └── TransitionTable.cs              (NEW ~80 lines)
├── Scanning/
│   ├── ScanModeController.cs           (SIMPLIFIED ~80 lines)
│   ├── ScanSessionManager.cs           (unchanged)
│   └── ScanDialogCoordinator.cs        (unchanged)
├── PreExecution/
│   ├── PreExecutionPipeline.cs         (EXTRACTED ~150 lines)
│   ├── PreExecutionContext.cs          (unchanged)
│   └── PreExecutionDependencies.cs     (SIMPLIFIED)
├── Coordinator/
│   ├── TestExecutionCoordinator.cs     (UNCHANGED)
│   ├── TestExecutionCoordinator.*.cs   (UNCHANGED)
│   └── ColumnExecutor.cs               (UNCHANGED)
└── ErrorCoordinator/
    └── *.cs                            (UNCHANGED)

Models/Steps/
├── ExecutionStateManager.cs            (UNCHANGED - test execution)
└── ExecutionState.cs                   (UNCHANGED)

Services/Common/
├── ExecutionActivityTracker.cs         (DELETED - replaced by SystemLifecycleManager)

Services/Main/
├── SettingsAccessStateManager.cs       (DELETED - replaced by SystemLifecycleManager)
```

---

## Line Count Summary

### Backend Services

| Component | BEFORE | AFTER | Change |
|-----------|--------|-------|--------|
| **ScanModeController** | 250 | ~80 | -68% |
| **PreExecutionCoordinator** (all) | 500 | ~330 | -34% |
| **ExecutionActivityTracker** | 64 | 0 | DELETED |
| **SettingsAccessStateManager** | 74 | 0 | DELETED |
| **SystemLifecycleManager** (NEW) | 0 | ~180 | NEW |
| **SystemPhase/Trigger** (NEW) | 0 | ~55 | NEW |
| **TransitionTable** (NEW) | 0 | ~80 | NEW |
| **ExecutionStateManager** | 95 | 95 | UNCHANGED |
| **TestExecutionCoordinator** (all) | 610 | 610 | UNCHANGED |

**Backend Total:**
- Refactored: 888 → 645 lines (-27%)
- Unchanged: 705 lines
- **Net change: -243 lines**

### UI Components

| Component | BEFORE | AFTER | Change |
|-----------|--------|-------|--------|
| **BoilerInfo.razor** | 381 | ~280 | -26% |
| **SwitchMes.razor.cs** | 120 | ~70 | -42% |
| **AdminAuthorizationQr.razor.cs** | 79 | ~50 | -37% |
| **OperatorAuthorizationQr.razor.cs** | 79 | ~50 | -37% |

**UI Total:**
- BEFORE: 659 lines
- AFTER: ~450 lines
- **Reduction: -32%**

---

## Scanner-Input Synchronization Guarantee

### Implementation in SystemLifecycleManager

```csharp
public bool Transition(SystemTrigger trigger, string? barcode = null)
{
    lock (_lock)
    {
        var newPhase = _transitionTable.GetNextPhase(_phase, trigger);
        if (newPhase == null) return false;

        var oldPhase = _phase;
        _phase = newPhase.Value;

        // Barcode management (внутри lock!)
        switch (trigger)
        {
            case SystemTrigger.BarcodeReceived:
                _currentBarcode = barcode;
                break;
            case SystemTrigger.ScanModeDisabled:
            case SystemTrigger.ResetCompletedHard:
                _currentBarcode = null;
                break;
        }

        // Side effects определяются по newPhase
        _pendingNotification = (oldPhase, newPhase.Value);
    }

    // Уведомления ВНЕ lock
    NotifyPhaseChanged();
    return true;
}
```

### ScanSessionManager Integration

```csharp
// ScanModeController (simplified)
public class ScanModeController : IDisposable
{
    private readonly SystemLifecycleManager _lifecycle;
    private readonly ScanSessionManager _sessionManager;

    public ScanModeController(...)
    {
        _lifecycle.OnPhaseChanged += HandlePhaseChanged;
    }

    private void HandlePhaseChanged(SystemPhase oldPhase, SystemPhase newPhase)
    {
        // Scanner session синхронизирована с Phase
        var shouldHaveScanner = newPhase is SystemPhase.WaitingForBarcode
                             or SystemPhase.Preparing;

        if (shouldHaveScanner && !_sessionManager.HasActiveSession)
        {
            _sessionManager.AcquireSession(HandleBarcodeScanned);
        }
        else if (!shouldHaveScanner && _sessionManager.HasActiveSession)
        {
            _sessionManager.ReleaseSession();
        }
    }

    private void HandleBarcodeScanned(string barcode)
    {
        _lifecycle.Transition(SystemTrigger.BarcodeReceived, barcode);
    }
}
```

### Guarantees

1. **Atomicity:** Phase и CurrentBarcode меняются атомарно внутри lock
2. **Consistency:** Scanner session управляется по Phase, не по отдельным флагам
3. **Synchronization:** UI свойства (`IsScannerInputEnabled`) вычисляются из Phase
4. **No Race Conditions:** Все проверки внутри одного lock

---

## Integration with Existing Components

### TestExecutionCoordinator — БЕЗ ИЗМЕНЕНИЙ

```csharp
// Существующий код продолжает работать
public class TestExecutionCoordinator
{
    public ExecutionStateManager StateManager { get; }  // НЕ МЕНЯЕТСЯ

    public async Task StartAsync()
    {
        StateManager.TransitionTo(ExecutionState.Running);  // НЕ МЕНЯЕТСЯ
        // ...
    }
}
```

### PreExecutionCoordinator — Интеграция

```csharp
// После рефакторинга
public class PreExecutionCoordinator
{
    private readonly SystemLifecycleManager _lifecycle;

    public async Task<bool> ExecutePipelineAsync(CancellationToken ct)
    {
        // Pipeline execution...

        if (success)
        {
            _lifecycle.Transition(SystemTrigger.PreparationCompleted);
            return true;
        }
        else
        {
            _lifecycle.Transition(SystemTrigger.PreparationFailed);
            return false;
        }
    }
}
```

### PlcResetCoordinator — Интеграция

```csharp
// Существующий PlcResetCoordinator определяет тип сброса и вызывает:
var isInScanPhase = _lifecycle.IsScannerInputEnabled;
if (isInScanPhase)
    _lifecycle.Transition(SystemTrigger.ResetRequestedSoft);
else
    _lifecycle.Transition(SystemTrigger.ResetRequestedHard);

// После завершения сброса:
if (isInScanPhase)
    _lifecycle.Transition(SystemTrigger.ResetCompletedSoft);
else
    _lifecycle.Transition(SystemTrigger.ResetCompletedHard);
```

---

## Testing Considerations

### Unit Tests для SystemLifecycleManager

```csharp
[Fact]
public void WaitingForBarcode_ScannerInputEnabled_ReturnsTrue()
{
    var sm = new SystemLifecycleManager();
    sm.Transition(SystemTrigger.ScanModeEnabled);

    Assert.Equal(SystemPhase.WaitingForBarcode, sm.Phase);
    Assert.True(sm.IsScannerInputEnabled);
    Assert.True(sm.IsScannerActive);
}

[Fact]
public void BarcodeReceived_SetsCurrentBarcode()
{
    var sm = new SystemLifecycleManager();
    sm.Transition(SystemTrigger.ScanModeEnabled);
    sm.Transition(SystemTrigger.BarcodeReceived, "12345");

    Assert.Equal(SystemPhase.Preparing, sm.Phase);
    Assert.Equal("12345", sm.CurrentBarcode);
    Assert.False(sm.IsScannerInputEnabled);  // Input disabled during preparation
}

[Fact]
public void ResetCompletedHard_ClearsBarcode()
{
    var sm = new SystemLifecycleManager();
    sm.Transition(SystemTrigger.ScanModeEnabled);
    sm.Transition(SystemTrigger.BarcodeReceived, "12345");
    sm.Transition(SystemTrigger.ResetRequestedHard);
    sm.Transition(SystemTrigger.ResetCompletedHard);

    Assert.Equal(SystemPhase.Idle, sm.Phase);
    Assert.Null(sm.CurrentBarcode);
}

[Fact]
public void RepeatRequested_PreservesBarcode()
{
    var sm = new SystemLifecycleManager();
    // ... setup to Completed state with barcode "12345"

    sm.Transition(SystemTrigger.RepeatRequested);

    Assert.Equal(SystemPhase.WaitingForBarcode, sm.Phase);
    Assert.Equal("12345", sm.CurrentBarcode);  // Preserved!
}
```

### Integration Tests

- Сценарий: Scanner + Input Field синхронизация
- Сценарий: Сброс во время подготовки
- Сценарий: Повтор после NOK (barcode preserved)
- Сценарий: Hard reset (barcode cleared)

---

## Risks and Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Регрессия при миграции | Medium | High | Фазовое внедрение, A/B testing |
| Deadlock в SystemLifecycleManager | Low | High | События вне lock, unit tests |
| Неучтённые edge cases | Medium | Medium | Подробное логирование transitions |
| Рассинхронизация scanner/input | Low | High | Атомарные transitions, unit tests |
| UI не обновляется | Medium | Medium | Verify InvokeAsync pattern preserved |
| Конфликт с ExecutionStateManager | None | — | Разные уровни, разные enum |
