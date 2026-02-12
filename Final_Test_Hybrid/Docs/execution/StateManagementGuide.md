# StateManagementGuide.md

Руководство по управлению состоянием в системе выполнения тестов.

## Обзор компонентов

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Уровень 1: Системные фазы                          │
├─────────────────────────────────────────────────────────────────────────────┤
│  ScanModeController         │  Активация/деактивация режима сканирования   │
│  ├── _isActivated           │  Режим сканирования активен                  │
│  ├── _isResetting           │  PLC reset в процессе                        │
│  └── IsInScanningPhase      │  Для определения soft/hard reset             │
├─────────────────────────────────────────────────────────────────────────────┤
│  ExecutionActivityTracker   │  Какая фаза сейчас активна                   │
│  ├── IsPreExecutionActive   │  Подготовка (scan, validation)               │
│  └── IsTestExecutionActive  │  Выполнение тестовых шагов                   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       Уровень 2: Состояние выполнения                       │
├─────────────────────────────────────────────────────────────────────────────┤
│  ExecutionStateManager      │  State machine теста                         │
│  ├── State                  │  Idle/Processing/Running/PausedOnError/...   │
│  ├── ErrorQueue             │  Очередь ошибок для обработки                │
│  └── HasPendingErrors       │  Есть ошибки в очереди                       │
├─────────────────────────────────────────────────────────────────────────────┤
│  ExecutionFlowState         │  Причина остановки теста                     │
│  ├── StopReason             │  Operator/PlcForceStop/PlcSoftReset/...      │
│  ├── StopAsFailure          │  Остановка как ошибка (для отчёта)           │
│  └── IsStopRequested        │  Запрошена остановка                         │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Уровень 3: Координаторы                           │
├─────────────────────────────────────────────────────────────────────────────┤
│  ErrorCoordinator           │  Прерывания и восстановление                 │
│  ├── Reset()                │  Полный сброс                                │
│  └── ForceStop()            │  Мягкий сброс                                │
├─────────────────────────────────────────────────────────────────────────────┤
│  PlcResetCoordinator        │  Обработка Req_Reset от PLC                  │
│  ├── OnResetStarting        │  Возвращает wasInScanPhase                   │
│  └── ExecuteSmartReset()    │  Soft (ForceStop) или Hard (Reset)           │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Глоссарий терминов

| Термин | Значение |
|--------|----------|
| **Soft reset** | Сброс по Req_Reset когда `wasInScanPhase=true`. Вызывает `ForceStop()`. **До AskEnd** данные сохраняются, **после AskEnd** очищаются через `ClearStateOnReset()`. |
| **Hard reset** | Сброс по Req_Reset когда `wasInScanPhase=false`. Очистка состояния через `HandleGridClear()` по `OnAskEndReceived`. `Reset()` → `OnReset` только устанавливает stop-reason и чистит UI. |
| **ForceStop** | Метод `ErrorCoordinator.ForceStop()`. Resume + ClearInterrupt. НЕ вызывает OnReset. |
| **Reset** | Метод `ErrorCoordinator.Reset()`. Resume + ClearInterrupt + OnReset event. |
| **Soft deactivation** | `ScanModeController.PerformSoftDeactivation()`. Отпускает сессию сканера, но `_isActivated=true`. |
| **Full deactivation** | `ScanModeController.PerformFullDeactivation()`. Отменяет loop, `_isActivated=false`. |
| **Scan phase** | Режим сканирования активен и не в процессе reset (`_isActivated && !_isResetting`). |
| **OnForceStop** | Event `PlcResetCoordinator.OnForceStop`. Часть reset-потока, вызывается до AskEnd. |
| **PlcSoftReset** | `ExecutionStopReason`. Soft reset от PLC (`wasInScanPhase=true`). |
| **PlcHardReset** | `ExecutionStopReason`. Hard reset от PLC (`wasInScanPhase=false`). |
| **PausedOnError** | `ExecutionState`. Тест на паузе из-за ошибки шага, ожидает Retry/Skip. Переход в `TestExecutionCoordinator`. |
| **Processing** | `ExecutionState`. Существует в enum, но **не используется** в коде (TransitionTo не вызывается). |

---

## ExecutionFlowState

**Файл:** `Services/Steps/Infrastructure/Execution/ExecutionFlowState.cs`

Хранит причину остановки теста и флаг ошибки.

### Поля и свойства

| Член | Тип | Описание |
|------|-----|----------|
| `StopReason` | `ExecutionStopReason` | Причина остановки (первая сохраняется) |
| `StopAsFailure` | `bool` | Остановка является ошибкой (OR-семантика) |
| `IsStopRequested` | `bool` | `StopReason != None` |

### ExecutionStopReason enum

```csharp
public enum ExecutionStopReason
{
    None,              // Нет остановки
    Operator,          // Остановка оператором
    AutoModeDisabled,  // Пропал автомат
    PlcForceStop,      // Force stop от PLC
    PlcSoftReset,      // Мягкий сброс PLC
    PlcHardReset       // Жёсткий сброс PLC
}
```

### Методы

#### RequestStop(reason, stopAsFailure)

```csharp
public void RequestStop(ExecutionStopReason reason, bool stopAsFailure)
{
    lock (_lock)
    {
        // First-wins: первая причина сохраняется
        if (_stopReason == ExecutionStopReason.None)
        {
            _stopReason = reason;
        }
        // OR-семантика: failure объединяется
        _stopAsFailure |= stopAsFailure;
    }
    OnChanged?.Invoke();
}
```

**Семантика:**
- **First-wins**: Если уже есть причина, новая игнорируется
- **OR для failure**: `stopAsFailure` накапливается через OR

**Примеры:**
```
RequestStop(Operator, false) → RequestStop(PlcForceStop, true)
→ StopReason = Operator, StopAsFailure = true

RequestStop(Operator, true) → RequestStop(PlcForceStop, false)
→ StopReason = Operator, StopAsFailure = true

RequestStop(Operator, false) → RequestStop(PlcForceStop, false)
→ StopReason = Operator, StopAsFailure = false
```

#### ClearStop()

```csharp
public void ClearStop()
{
    lock (_lock)
    {
        _stopReason = ExecutionStopReason.None;
        _stopAsFailure = false;
    }
    OnChanged?.Invoke();
}
```

#### GetSnapshot()

```csharp
public (ExecutionStopReason Reason, bool StopAsFailure) GetSnapshot()
{
    lock (_lock)
    {
        return (_stopReason, _stopAsFailure);
    }
}
```

### Использование

| Компонент | Метод | Когда |
|-----------|-------|-------|
| `TestExecutionCoordinator` | `RequestStop()` | Остановка теста |
| `PreExecutionCoordinator` | `RequestStop()` | Отмена подготовки, PLC reset |
| Координаторы | `ClearStop()` | Сброс для нового теста |
| `TestExecutionCoordinator` | `GetSnapshot()` | Логирование результата, определение причины остановки |

**Примечание:** `OnChanged` event не имеет подписчиков. UI не использует `GetSnapshot()` напрямую.

---

## ScanModeController

**Файл:** `Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs`

Управляет режимом сканирования на основе состояния оператора и автомата.

### Флаги

| Флаг | Тип | Описание |
|------|-----|----------|
| `_isActivated` | `bool` | Режим сканирования активирован |
| `_isResetting` | `bool` | PLC reset в процессе |
| `IsScanModeEnabled` | `bool` | Условие активации (computed) |
| `IsInScanningPhase` | `bool` | Для soft/hard reset (computed) |

### IsScanModeEnabled

```csharp
public bool IsScanModeEnabled => _operatorState.IsAuthenticated && _autoReady.IsReady;
```

Режим сканирования **включён** когда:
- Оператор авторизован (`IsAuthenticated`)
- Автомат готов (`AutoReady`)

### Гейтинг barcode в ожидании скана

`IsScanModeEnabled` не является достаточным условием для приёма barcode.

Фактический контракт приёма barcode в pre-execution:

```csharp
canAcceptBarcode = PreExecutionCoordinator.IsAcceptingInput
                   && OpcUaConnectionState.IsConnected;
```

Следствия:
- При `IsConnected = false` ручной ввод в `BoilerInfo` блокируется (read-only).
- При `IsConnected = false` аппаратный скан игнорируется в `BarcodeDebounceHandler`.
- При `IsConnected = false` во время ожидания barcode (`IsAcceptingInput = true`) Scan-таймер ставится на паузу; после reconnect возобновляется только при активном scan-mode и вне reset-фазы.
- Изменение только `ScanModeController` без этого гейтинга считается неполным фиксом.

### IsInScanningPhase

```csharp
public bool IsInScanningPhase
{
    get
    {
        lock (_stateLock)
        {
            return _isActivated && !_isResetting;
        }
    }
}
```

Используется `PlcResetCoordinator` для определения типа сброса:
- `true` → **Soft reset** (ForceStop) — данные очищаются по OnAskEndReceived через HandleGridClear()
- `false` → **Hard reset** (Reset) — данные также очищаются по OnAskEndReceived; OnReset только устанавливает stop-reason

### Диаграмма состояний

```
                    IsScanModeEnabled
                          │
              ┌───────────┴───────────┐
              ▼                       ▼
         true (enable)           false (disable)
              │                       │
              ▼                       ▼
     ┌────────────────┐      ┌────────────────┐
     │TryActivate     │      │TryDeactivate   │
     │ScanMode()      │      │ScanMode()      │
     └────────────────┘      └────────────────┘
              │                       │
     _isResetting?           _isResetting?
        │    │                  │    │
       yes   no                yes   no
        │    │                  │    │
        ▼    ▼                  ▼    ▼
     return  continue        return  continue
              │                       │
     _isActivated?           _isActivated?
        │    │                  │    │
       yes   no                yes   no
        │    │                  │    │
        ▼    ▼                  ▼    ▼
     Refresh Initial          Full/Soft return
     Session Activate        Deactivate
```

### Методы активации/деактивации

#### PerformInitialActivation()

```csharp
private void PerformInitialActivation()
{
    _isActivated = true;
    _sessionManager.AcquireSession(HandleBarcodeScanned);
    AddScanStepToGrid();
    StartScanTiming();
    StartMainLoop();
}
```

Вызывается при первом включении режима сканирования.

#### PerformFullDeactivation()

```csharp
private void PerformFullDeactivation()
{
    _loopCts?.Cancel();
    _isActivated = false;
    _sessionManager.ReleaseSession();
    if (!_operatorState.IsAuthenticated)
    {
        _statusReporter.ClearAllExceptScan();
    }
}
```

Полная деактивация — отменяет main loop, отпускает сессию.

#### PerformSoftDeactivation()

```csharp
private void PerformSoftDeactivation()
{
    _sessionManager.ReleaseSession();
}
```

Мягкая деактивация — только отпускает сессию сканера, но `_isActivated` остаётся `true`.

**Когда используется:**
- AutoMode потерян, но оператор авторизован
- Выполняется тест (execution active)
- Ожидаем ввод штрихкода

### PLC Reset handling

#### HandleResetStarting()

```csharp
private bool HandleResetStarting()
{
    lock (_stateLock)
    {
        var wasInScanPhase = IsInScanningPhaseUnsafe;
        _isResetting = true;
        _stepTimingService.PauseAllColumnsTiming();
        _sessionManager.ReleaseSession();
        return wasInScanPhase;
    }
}
```

Возвращает `wasInScanPhase` для `PlcResetCoordinator`:
- `true` → был в scan step → soft reset
- `false` → был в тесте → hard reset

#### HandleResetCompleted()

```csharp
private void HandleResetCompleted()
{
    lock (_stateLock)
    {
        TransitionToReadyInternal();
    }
}

private void TransitionToReadyInternal()
{
    _isResetting = false;
    if (!IsScanModeEnabled)
    {
        _loopCts?.Cancel();
        _isActivated = false;
        _stepTimingService.PauseAllColumnsTiming();
        return;
    }
    if (!_isActivated)
    {
        PerformInitialActivation();
        return;
    }
    _stepTimingService.ResetScanTiming();
    _sessionManager.AcquireSession(HandleBarcodeScanned);
}
```

После сброса восстанавливает состояние в зависимости от `IsScanModeEnabled`.

---

## ExecutionStateManager

**Файл:** `Models/Steps/ExecutionStateManager.cs`

State machine для управления состоянием выполнения теста.

### ExecutionState enum

```csharp
public enum ExecutionState
{
    Idle,           // Ожидание старта
    Processing,     // PreExecution шаги выполняются
    Running,        // Test шаги выполняются
    PausedOnError,  // Ошибка шага (ожидание Retry/Skip/Reset)
    Completed,      // Тест завершён
    Failed          // Тест провален (критическая ошибка)
}
```

### Диаграмма переходов

```
              ┌────────────────────────────────────────┐
              │                                        │
              ▼                                        │
           ┌──────┐                                    │
 ┌────────►│ Idle │◄──────────────────────────────────┤
 │         └──────┘                                    │
 │            │                                        │
 │    Test start (TryStartInBackground)                │
 │            │                                        │
 │            ▼                                        │
 │      ┌─────────┐                                    │
 │      │ Running │ ─── Test шаги на 4 колонках        │
 │      └─────────┘                                    │
 │            │                                        │
 │     Step Error                                      │
 │            │                                        │
 │            ▼                                        │
 │   ┌───────────────┐                                 │
 │   │ PausedOnError │ ◄── TestExecutionCoordinator    │
 │   └───────────────┘     HandleStepErrorAsync()      │
 │         │     │                                     │
 │     Retry   Skip                                    │
 │         │     │                                     │
 │         ▼     ▼                                     │
 │      ┌─────────┐                                    │
 │      │ Running │                                    │
 │      └─────────┘                                    │
 │            │                                        │
 │       All steps done                                │
 │            │                                        │
 │            ▼                                        │
 │     ┌───────────┐                                   │
 │     │ Completed │───────────────────────────────────┘
 │     └───────────┘
 │
 │     ┌────────┐
 └─────│ Failed │ ◄── Reset() / критическая ошибка
       └────────┘
```

**Примечания:**
- `Processing` существует в enum, но **не используется** — нет TransitionTo(Processing)
- PreExecution не меняет ExecutionStateManager
- Пауза управляется через `IInterruptContext.Pause()` → `PauseTokenSource.Pause()`
- Переход в `PausedOnError` делает `TestExecutionCoordinator.HandleErrorsIfAny()` — **СРАЗУ** при ошибке через Channel-сигнал (см. секцию "Channel-based Error Handling")

### API

| Член | Описание |
|------|----------|
| `State` | Текущее состояние |
| `TransitionTo(state)` | Переход в новое состояние |
| `EnqueueError(error)` | Добавить ошибку в очередь |
| `DequeueError()` | Извлечь ошибку из очереди |
| `ClearErrors()` | Очистить очередь ошибок |
| `HasPendingErrors` | Есть ошибки в очереди |
| `CurrentError` | Текущая ошибка (peek) |
| `ErrorCount` | Количество ошибок |
| `IsActive` | `Running`, `Processing`, или `PausedOnError` |
| `CanProcessSignals` | `State == PausedOnError` |
| `HadSkippedError` | Была пропущенная ошибка в этом тесте |
| `MarkErrorSkipped()` | Отметить что ошибка была пропущена |
| `ResetErrorTracking()` | Сбросить флаг пропущенных ошибок |
| `OnStateChanged` | Событие смены состояния |

---

## TestExecutionCoordinator: Channel-based Error Handling

**Файлы:**
- `Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorSignals.cs`
- `Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorQueue.cs`
- `Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs`
- `Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs`
- `Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.Execution.cs`

### Проблема (до)

При ошибке в одной колонке система ждала завершения ВСЕХ колонок, и только потом показывала диалог Retry/Skip.

### Решение (после)

Диалог ошибки показывается **СРАЗУ** при обнаружении. Другие колонки **продолжают работать**.

### Архитектура: Channel<bool> как async auto-reset сигнал

```csharp
private Channel<bool>? _errorSignalChannel;
```

**Почему Channel:**
- Ёмкость 1 + DropWrite = async auto-reset сигнал (лишние сигналы не копятся)
- `ReadAllAsync` корректно обрабатывает отмену
- Закрытие канала гарантирует выход цикла
- Thread-safe через `Interlocked`/`Volatile`

### Диаграмма выполнения

```
ExecuteMapOnAllColumns()
│
├─ StartErrorSignalChannel()
│
├─ RunErrorHandlingLoopAsync() ────────────────────┐
│  └─ await foreach (ReadAllAsync)                 │ PARALLEL
│                                                   │
├─ RunExecutorsAsync() ────────────────────────────┤
│  ├─ executor[0].ExecuteMapAsync() ──┐            │
│  ├─ executor[1].ExecuteMapAsync() ──┼─ PARALLEL  │
│  ├─ executor[2].ExecuteMapAsync() ──┤            │
│  └─ executor[3].ExecuteMapAsync() ──┘            │
│                                                   │
│  [Column 0 fails]                                │
│  └─ EnqueueFailedExecutors()                     │
│     └─ SignalErrorDetected() → channel.TryWrite  │
│                                                   │
│  [Error loop receives signal]                    │
│  └─ HandleErrorsIfAny()                          │
│     └─ OnErrorOccurred → UI диалог СРАЗУ         │
│     └─ WaitForResolution                         │
│     └─ Retry/Skip                                │
│                                                   │
│  [Columns 1-3 keep running...]                   │
│                                                   │
├─ executionTask completes                         │
│  └─ ContinueWith → CompleteErrorSignalChannel()  │
│     └─ TryWrite(true) + TryComplete()            │
│                                                   │
└─ Task.WhenAll(execution, errorLoop, completion)  │
   └─ errorLoop exits via channel completion ──────┘
```

### Ключевые методы

| Метод | Описание |
|-------|----------|
| `StartErrorSignalChannel()` | Создаёт bounded канал (capacity=1, DropWrite) |
| `SignalErrorDetected()` | Отправляет сигнал в канал через `Volatile.Read` |
| `CompleteErrorSignalChannel()` | Закрывает канал после завершения выполнения |
| `EnqueueFailedExecutors()` | Добавляет ошибки в очередь, сигналит если появились новые |
| `ProcessErrorSignalsAsync()` | Цикл обработки сигналов через `ReadAllAsync` |

### Thread-safety

| Механизм | Применение |
|----------|------------|
| `Interlocked.Exchange` | Атомарная замена канала в `Start/Complete` |
| `Volatile.Read` | Чтение канала в `SignalErrorDetected` |
| `lock (_enqueueLock)` | Защита очереди ошибок в `EnqueueFailedExecutors` |
| `BoundedChannelOptions.SingleReader` | Гарантирует один reader |

### Edge Cases

| Сценарий | Поведение |
|----------|-----------|
| Две колонки падают одновременно | Обе в очередь, DropWrite отбросит лишний сигнал, `HandleErrorsIfAny` обработает всю очередь |
| Колонка падает во время диалога | Добавится в очередь (state=PausedOnError разрешён), обработается после текущей |
| Все колонки успешны | Канал закроется через ContinueWith, цикл выйдет |
| Отмена во время диалога | `ReadAllAsync` выбросит `OperationCanceledException`, цикл выйдет чисто |

---

## ExecutionActivityTracker

**Файл:** `Services/Common/ExecutionActivityTracker.cs`

Отслеживает какая фаза выполнения сейчас активна.

### API

| Член | Тип | Описание |
|------|-----|----------|
| `IsPreExecutionActive` | `bool` | PreExecution шаги выполняются |
| `IsTestExecutionActive` | `bool` | Test шаги выполняются |
| `IsAnyActive` | `bool` | Любая фаза активна |
| `SetPreExecutionActive(bool)` | `void` | Установить флаг PreExecution |
| `SetTestExecutionActive(bool)` | `void` | Установить флаг TestExecution |
| `Clear()` | `void` | Сбросить оба флага |
| `OnChanged` | `Action` | Событие при изменении любого флага |

### Использование

```csharp
// PreExecutionCoordinator
_activityTracker.SetPreExecutionActive(true);
try
{
    await ExecutePipelineAsync(...);
}
finally
{
    _activityTracker.SetPreExecutionActive(false);
}

// TestExecutionCoordinator
_activityTracker.SetTestExecutionActive(true);
try
{
    await ExecuteColumnsAsync(...);
}
finally
{
    _activityTracker.SetTestExecutionActive(false);
}
```

### Связь с ScanModeController

```csharp
// ScanModeController.ShouldUseSoftDeactivation()
private bool ShouldUseSoftDeactivation()
{
    var isExecutionActive = _activityTracker.IsAnyActive;  // ← используется здесь
    // ...
}
```

---

## PlcResetCoordinator

**Файл:** `Services/Main/PlcReset/PlcResetCoordinator.cs`

Обрабатывает сигнал `Req_Reset` от PLC.

### События и API

| Член | Тип | Описание |
|------|-----|----------|
| `IsActive` | `bool` | Reset в процессе |
| `OnActiveChanged` | `Action` | Изменение IsActive |
| `OnForceStop` | `Action` | Сигнал остановки (до AskEnd) |
| `OnResetStarting` | `Func<bool>` | Возвращает wasInScanPhase |
| `OnAskEndReceived` | `Action` | AskEnd получен — очистка Grid |
| `OnResetCompleted` | `Action` | Reset завершён или отменён в runtime (кроме disposal) |
| `CancelCurrentReset()` | `void` | Отмена текущего reset |

### Типы сброса

| Тип | Условие | Метод | Очистка состояния |
|-----|---------|-------|-------------------|
| **Soft** | `wasInScanPhase = true` | `ForceStop()` | По `OnAskEndReceived` → `HandleGridClear()` → `ClearStateOnReset()` |
| **Hard** | `wasInScanPhase = false` | `Reset()` | По `OnAskEndReceived` → `HandleGridClear()` → `ClearStateOnReset()` |

**Важно:**
- Оба типа очищают состояние через `OnAskEndReceived`
- `OnReset` (только hard) → устанавливает stop-reason через `HandleHardReset()`
- **Timeout:** если AskEnd не приходит, очистка через `HandleCycleExit(HardReset)`

### Диаграмма (полная)

```
Req_Reset от PLC
       │
       ▼
OnResetStarting() ──► wasInScanPhase = ScanModeController.IsInScanningPhase
       │                               = (_isActivated && !_isResetting)
       ▼
OnForceStop() ──┬─► PreExecutionCoordinator.HandleSoftStop()
       │        │   → RequestStop(PlcSoftReset, true)
       │        │   → отмена текущей операции
       │        │
       │        └─► TestExecutionCoordinator.HandleForceStop()
       │            → RequestStop(PlcForceStop, true)
       │            → ClearErrors()
       ▼
SendDataToMesAsync() ──► Отправка данных в MES
       │
       ▼
WaitForAskEnd(AskEndTimeoutSec) ──► Ожидание Ask_End от PLC
       │
       ├── TimeoutException:
       │       │
       │       ▼
       │   HandleInterruptAsync(TagTimeout) ──► TagTimeoutBehavior
       │       │                                 показывает диалог
       │       ▼
       │   OnResetCompleted()
       │
       ▼ (успех)
OnAskEndReceived() ──► PreExecutionCoordinator.HandleGridClear()
       │                → ClearStateOnReset()
       │                → StatusReporter.ClearAllExceptScan()
       ▼
ExecuteSmartReset(wasInScanPhase)
       │
       ├── wasInScanPhase=true:
       │       ForceStop() → Resume + ClearInterrupt
       │                     (Grid НЕ очищается — уже очищен в OnAskEndReceived)
       │
       └── wasInScanPhase=false:
               Reset() → Resume + ClearInterrupt + OnReset event
                         ├─► PreExecutionCoordinator.HandleHardReset()
                         └─► TestExecutionCoordinator.HandleReset() → ClearErrors()
       │
       ▼
OnResetCompleted() ──► ScanModeController.HandleResetCompleted()
                       → TransitionToReadyInternal()
```

### Таймауты reset-flow

- `PlcResetCoordinator` использует `OpcUa:ResetFlowTimeouts`:
  - `AskEndTimeoutSec` — лимит ожидания `AskEnd`;
  - `ReconnectWaitTimeoutSec` — лимит одного ожидания reconnect;
  - `ResetHardTimeoutSec` — общий дедлайн reset-flow.
- Ожидание reconnect (`connectionState.WaitForConnectionAsync`) ограничивается `min(ReconnectWaitTimeoutSec, remaining HardTimeout)`.
- При истечении любого из лимитов выполняется timeout-path: `HandleInterruptAsync(TagTimeout)` и `OnResetCompleted`.

### Обработка исключений

```csharp
private async Task HandleResetExceptionAsync(Exception ex)
{
    switch (ex)
    {
        case OperationCanceledException when _disposed:
            // Disposal — без OnResetCompleted
            break;
        case OperationCanceledException:
            // Runtime-отмена — возвращаем scan-mode через OnResetCompleted
            InvokeEventSafe(OnResetCompleted);
            break;
        case TimeoutException:
            // Таймаут AskEnd или reconnect-window → HandleInterruptAsync(TagTimeout)
            await _errorCoordinator.HandleInterruptAsync(InterruptReason.TagTimeout);
            InvokeEventSafe(OnResetCompleted);
            break;
        default:
            // Неожиданная ошибка → полный Reset()
            _errorCoordinator.Reset();
            InvokeEventSafe(OnResetCompleted);
            break;
    }
}
```

---

## ErrorCoordinator: Reset vs ForceStop

**Файл:** `Services/Steps/Infrastructure/Execution/ErrorCoordinator/*.cs`

### Что делают методы

Оба метода выполняют **минимальные действия**:
- Resume (снятие паузы через `_pauseToken`)
- ClearCurrentInterrupt (очистка текущего прерывания)

**Очистка данных** происходит через **event subscribers**:
- Grid, BoilerState → `PreExecutionCoordinator` (OnForceStop, OnAskEndReceived, OnReset)
- Errors → `TestExecutionCoordinator` (OnForceStop, OnReset) через `ClearErrors()`

### Сравнение методов

| Аспект | ForceStop() | Reset() |
|--------|-------------|---------|
| **Назначение** | Мягкий сброс | Полный сброс |
| **Resume** | Да | Да |
| **ClearCurrentInterrupt** | Да | Да |
| **OnReset event** | **Нет** | **Да** |
| **Когда** | Soft reset PLC | Hard reset, timeout, критическая ошибка |

### ForceStop()

```csharp
public void ForceStop()
{
    _logger.LogInformation("=== МЯГКИЙ СБРОС (снятие прерывания) ===");
    _pauseToken.Resume();
    ClearCurrentInterrupt();
    // ForceStop сам не чистит данные — очистка через OnAskEndReceived
}
```

### Reset()

```csharp
public void Reset()
{
    _logger.LogInformation("=== ПОЛНЫЙ СБРОС ===");
    _pauseToken.Resume();
    ClearCurrentInterrupt();
    InvokeEventSafe(OnReset, "OnReset");  // → PreExecutionCoordinator.HandleHardReset()
}
```

### Кто подписан на OnReset

```csharp
// PreExecutionCoordinator.Retry.cs
coordinators.ErrorCoordinator.OnReset += HandleHardReset;

private void HandleHardReset()
{
    TryCompletePlcReset();
    HandleStopSignal(PreExecutionResolution.HardReset);
    infra.StatusReporter.ClearAllExceptScan();  // ← Очистка Grid
}
```

**Примечание по pre-execution error-resolution:**  
`ErrorResolution.ConnectionLost` для pre-execution нормализуется в `PreExecutionResolution.HardReset`, а не в `Timeout`.

---

## Таблица очистки данных (кто что очищает)

| Событие | Компонент | Что очищает | Метод |
|---------|-----------|-------------|-------|
| **OnForceStop** | PreExecutionCoordinator | Отмена операции, RequestStop | `HandleSoftStop()` |
| **OnForceStop** | TestExecutionCoordinator | Errors, StopAsFailure | `HandleForceStop()` → `ClearErrors()` |
| **OnAskEndReceived** | PreExecutionCoordinator | Grid, состояние, BoilerState | `HandleGridClear()` → `ClearStateOnReset()` |
| **OnReset** | PreExecutionCoordinator | Stop-reason, статус UI | `HandleHardReset()` → `ClearAllExceptScan()` (только UI) |
| **OnReset** | TestExecutionCoordinator | Errors, StopAsFailure | `HandleReset()` → `ClearErrors()` |
| **Test completion** | PreExecutionCoordinator | Результаты, финализация | `HandleTestCompletionAsync()` |
| **ClearStop** | ExecutionFlowState | StopReason, StopAsFailure | Координаторы |

### Подробнее о PreExecutionCoordinator handlers

```csharp
// OnForceStop → HandleSoftStop()
private void HandleSoftStop()
{
    BeginPlcReset();
    HandleStopSignal(PreExecutionResolution.SoftStop);
    // → RequestStop(PlcSoftReset, true)
    // → CycleExitReason.SoftReset
}

// OnAskEndReceived → HandleGridClear()
private void HandleGridClear()
{
    CompletePlcReset();
    ClearStateOnReset();                    // ← Очистка состояния
    infra.StatusReporter.ClearAllExceptScan();  // ← Очистка Grid
}

// OnReset → HandleHardReset()
private void HandleHardReset()
{
    TryCompletePlcReset();
    HandleStopSignal(PreExecutionResolution.HardReset);
    // → RequestStop(PlcHardReset, true)
    // → CycleExitReason.HardReset
    infra.StatusReporter.ClearAllExceptScan();  // ← Очистка Grid
}
```

---

## SystemLifecycleManager (НЕ ИСПОЛЬЗУЕТСЯ)

**Файл:** `Services/Steps/Infrastructure/Execution/Lifecycle/SystemLifecycleManager.cs`

⚠️ **Этот класс создан, но НЕ интегрирован в координаторы.**

### Что это

State machine для фаз жизненного цикла системы:
- `Idle` → `WaitingForBarcode` → `Preparing` → `Testing` → `Completed` → ...

### Почему не используется

- Координаторы не вызывают `Transition()`
- Состояние дублируется в `ScanModeController` флагами
- `CurrentBarcode` хранится отдельно в `PreExecutionCoordinator`

### Известный баг (если бы использовался)

В `ApplyBarcodeRules` barcode очищается при `ResetCompletedSoft`, хотя по комментарию должен сохраняться.

### Решение

Класс можно:
1. Удалить как мёртвый код
2. Интегрировать (требует рефакторинг)

---

## Таблица флагов и где они используются

| Флаг/Свойство | Класс | Где читается | Где записывается |
|---------------|-------|--------------|------------------|
| `_isActivated` | ScanModeController | `IsInScanningPhase`, `TryActivate/Deactivate` | `PerformInitialActivation`, `PerformFullDeactivation`, `TransitionToReadyInternal` |
| `_isResetting` | ScanModeController | `IsInScanningPhase`, `TryActivate/Deactivate` | `HandleResetStarting`, `TransitionToReadyInternal` |
| `_scanPausedByConnectionLoss` | ScanModeController | `SyncScanTimingForConnectionUnsafe` | `TryPauseScanTimingOnConnectionLossUnsafe`, `TryResumeScanTimingAfterReconnectUnsafe`, reset/deactivate ветки |
| `StopReason` | ExecutionFlowState | TestExecutionCoordinator (GetSnapshot), PreExecutionCoordinator (TryGetStopExitReason) | `RequestStop`, `ClearStop` |
| `StopAsFailure` | ExecutionFlowState | TestExecutionCoordinator (GetSnapshot) | `RequestStop` (OnForceStop/OnReset handlers), `ClearStop` |
| `State` | ExecutionStateManager | UI (OnStateChanged), координаторы | `TransitionTo` |
| `IsPreExecutionActive` | ExecutionActivityTracker | ScanModeController, ErrorCoordinator (IsAnyActive) | PreExecutionCoordinator |
| `IsTestExecutionActive` | ExecutionActivityTracker | ScanModeController, ErrorCoordinator (IsAnyActive) | TestExecutionCoordinator |
| `HadSkippedError` | ExecutionStateManager | TestExecutionCoordinator | `MarkErrorSkipped()` |

---

## Рекомендации

### Добавление нового типа остановки

1. Добавить в `ExecutionStopReason`:
```csharp
public enum ExecutionStopReason
{
    // ...existing...
    MyNewReason  // ← NEW
}
```

2. Вызвать в нужном месте:
```csharp
_flowState.RequestStop(ExecutionStopReason.MyNewReason, stopAsFailure: true);
```

### Добавление нового состояния теста

1. Добавить в `ExecutionState`:
```csharp
public enum ExecutionState
{
    // ...existing...
    MyNewState  // ← NEW
}
```

2. Обновить `ExecutionStateManager.TransitionTo()` если нужна валидация переходов.

3. Обновить UI компоненты, подписанные на `OnStateChanged`.

### При работе с флагами

- **Читать:** Использовать свойства (`IsInScanningPhase`, не `_isActivated && !_isResetting`)
- **Thread-safety:** Использовать `lock(_stateLock)` или `Lock` class
- **Events:** Вызывать `OnChanged?.Invoke()` после изменения
