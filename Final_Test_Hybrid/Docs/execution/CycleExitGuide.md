# CycleExitGuide.md — Управление состояниями выхода из цикла PreExecution

## Обзор

`CycleExitReason` — enum для явного управления очисткой состояния при выходе из цикла PreExecution.

## Архитектура

```
┌─────────────────────────────────────────────────────────────────┐
│  RunSingleCycleAsync                                            │
├─────────────────────────────────────────────────────────────────┤
│  1. WaitForBarcodeAsync() → barcode                             │
│  2. ExecuteCycleAsync() → CycleExitReason                       │
│  3. HandleCycleExit(reason) → очистка по состоянию              │
└─────────────────────────────────────────────────────────────────┘
```

## Enum CycleExitReason

```csharp
public enum CycleExitReason
{
    PipelineFailed,     // Pipeline вернул ошибку
    PipelineCancelled,  // Pipeline отменён (не сброс)
    TestCompleted,      // Тест завершился нормально
    SoftReset,          // Мягкий сброс (wasInScanPhase = true)
    HardReset,          // Жёсткий сброс
    RepeatRequested,    // OK повтор теста
    NokRepeatRequested, // NOK повтор с подготовкой
}
```

## Flow определения состояния

```
ExecuteCycleAsync:
1. ExecutePreExecutionPipelineAsync()
   │
   ├─ _pendingExitReason != null? → return _pendingExitReason
   │
   ├─ result.Status != TestStarted?
   │    ├─ Cancelled → PipelineCancelled
   │    └─ иначе → PipelineFailed
   │
    └─ Ждём завершения теста
         │
         ├─ _pendingExitReason != null? → return _pendingExitReason
         │
         └─ HandleTestCompletionAsync()
               ├─ RepeatRequested
               ├─ NokRepeatRequested
               └─ TestCompleted
```

## Repeat-flow и scan-контекст

### Что переиспользуется на repeat

- После успешного `ScanStep` сохраняется `_lastSuccessfulContext`.
- Для `CycleExitReason.RepeatRequested` выполняется `HandleRepeatRequestedExit()`:
  - `_skipNextScan = true`
  - scan-step пропускается на следующем цикле.
- На старте repeat используется сохранённый контекст (`ExecuteRepeatPipelineAsync`), без повторного scan-step.
- Перед `StartTimer1`, `BlockBoilerAdapterStep` и стартом `TestExecution`
  repeat/pre-execution обязан снова пройти normal AutoReady gate.
- Если PLC-связь жива, но `AutoReady=false`, pipeline поднимает существующий
  `AutoModeDisabled` interrupt и ждёт resume вместо тихого продолжения по сохранённому контексту.

### Что читается заново

При любом старте теста (включая repeat) `InitializeTestRunningAsync()` вызывает
`WriteScanServiceResultsAsync()` сразу после `ClearForNewTestStart()`:

- Из `ScanServiceContext` (кэш): `App_Version`, `Plant_ID`, `Shift_No`, `Tester_No`.
- Из OPC (reread каждый старт): `Pres_atmosph.`, `Pres_in_gas`.

### Что происходит при ошибке OPC на repeat

- Ошибка чтения давлений возвращает `PreExecutionResult.Fail(...)`.
- Цикл получает `CycleExitReason.PipelineFailed`.
- `HandleCycleExit(PipelineFailed)` не ставит повтор повторно.
- Следующий цикл ждёт новый barcode (полный старт pre-execution).

Это поведение intentional: повтор не продолжает выполнение с частично невалидными scan-данными.

### Жизненный цикл `_lastSuccessfulContext`

`_lastSuccessfulContext`:
- перезаписывается после каждого успешного `ScanStep`;
- очищается при `ClearStateOnReset()`;
- очищается при `ClearForNokRepeat()`;
- очищается при `ClearForTestCompletion()`.

Если контекст для repeat отсутствует, `ExecuteRepeatPipelineAsync()` завершится ошибкой
`"Нет данных для повтора"` (fail-fast вместо использования устаревших данных).

## Обработка состояний

```csharp
private void HandleCycleExit(CycleExitReason reason)
{
    switch (reason)
    {
        case CycleExitReason.TestCompleted:
            HandleTestCompletedExit();
            break;

        case CycleExitReason.SoftReset:
            // Очистка по AskEnd в ExecuteGridClearAsync()
            HandleSoftResetExit();
            break;

        case CycleExitReason.HardReset:
            // Fallback-очистка, если AskEnd-путь не успел выполнить cleanup
            HandleHardResetExit();
            break;

        case CycleExitReason.RepeatRequested:
            HandleRepeatRequestedExit();
            break;

        case CycleExitReason.NokRepeatRequested:
            HandleNokRepeatRequestedExit();
            break;

        case CycleExitReason.PipelineFailed:
        case CycleExitReason.PipelineCancelled:
            // Ничего — состояние сохраняется
            break;
    }
}
```

## Контракт очистки sequence и `Last*`-контекста

`CycleExitReason` влияет не только на остановку цикла, но и на режим очистки sequence UI:

| Exit path | Helper | Режим очистки sequence | Эффект |
|------|------|------|------|
| `TestCompleted` | `HandleTestCompletedExit()` → `ClearForTestCompletion()` | `SequenceClearMode.CompletedTest` | Фиксирует `Last*`-контекст, snapshot шагов и auto-export, затем возвращает UI к scan-строке. |
| `RepeatRequested` | `HandleRepeatRequestedExit()` → `ClearForRepeat()` | `SequenceClearMode.CompletedTest` | Сохраняет completed-history завершённого прогона перед repeat. |
| `NokRepeatRequested` | `HandleNokRepeatRequestedExit()` → `ClearForNokRepeat()` | `SequenceClearMode.CompletedTest` | Сохраняет completed-history перед NOK repeat с полной подготовкой. |
| `SoftReset` / `HardReset` | `HandleGridClear()` / `HandleHardResetExit()` | `SequenceClearMode.OperationalReset` | Сохраняет snapshot прерванного прогона в `StepHistoryService`, затем очищает sequence UI. Auto-export не запускается даже при включённой галочке автосохранения. |

Важно:

- reset-cleanup проходит через `ClearStateOnReset()`, внутри которого вызывается `BoilerState.Clear()`;
- поэтому `LastSerialNumber` / `LastTestCompletedAt` после reset могут обновиться на последний очищенный контекст котла;
- это нормальное поведение header в result/history/timer вкладках и не означает, что был создан новый completed snapshot;
- logout/full deactivation не являются `CycleExitReason`-веткой и очищают sequence через `SequenceClearMode.ClearOnly`.

### Прерывание completion-flow при reset

Во время `HandleTestCompletionAsync()` ожидание PLC completion-handshake (`Req_Repeat=true`
или `End=false` после записи `End=true`) обязано
прерываться не только PLC reset-токеном, но и отменой текущего cycle CTS.

Причина:

- PLC soft reset отменяет completion через reset-token;
- non-PLC hard reset (например, `PlcConnectionLost -> ErrorCoordinator.Reset()`) отменяет текущий цикл через `_currentCts.Cancel()`;
- completion-flow не должен оставаться в ожидании PLC decision после hard reset, иначе `HandleHardResetExit()` не дойдёт до cleanup sequence UI.
- completion decision-loop читает `Req_Repeat` и `End=false` только через known/unknown контракт `OpcUaSubscription.TryGetValue<bool>(...)`.
- empty/bad cache не трактуется как `false`: completion ждёт реальное PLC-решение или reset/cancel.

### Разрыв диагностики на штатном completion

- После показа `OK/NOK` картинки `TestCompletionCoordinator.HandleTestCompletedAsync()` делает best-effort `IModbusDispatcher.StopAsync()`.
- Этот teardown выполняется только в штатном completion-path и не заменяет существующие reset hooks.
- PLC completion-handshake после картинки (`End/Req_Repeat`) продолжается без активной Modbus-связи с котлом.
- Если PLC выбирает repeat, следующий цикл обязан снова поднять связь через `Coms/Check_Comms`.

### Детерминированный resolver stop-reason

- `_pendingExitReason` хранится атомарно как `int` sentinel, а не nullable enum.
- `_resetSignal` читается через local snapshot на весь цикл, чтобы `SignalReset()` не потерял owner в узком окне reset/cancel.
- `ResolveStopExitReasonOrFallback(...)` обязан использовать единый resolver перед fallback `PipelineCancelled` / `SoftReset`.
- post-AskEnd decision-loop для `Req_Repeat` / `AskEnd=false` использует тот же known/unknown контракт и не должен завершать cleanup по пустому cache.

## Очистка по AskEnd (HandleGridClear)

`OnAskEndReceived` обрабатывается через `HandleGridClear()` → `ExecuteGridClearAsync()`.
Очистка выполняется только один раз на reset-цикл и защищена от stale-сигналов
через sequence-aware reset-окно (`_currentAskEndWindow`):

```csharp
private async Task ExecuteGridClearAsync()
{
    var window = Volatile.Read(ref _currentAskEndWindow);
    var resetSequence = GetResetSequenceSnapshot();
    if (window == null || window.Sequence != resetSequence)
    {
        infra.Logger.LogInformation(
            "AskEndIgnoredAsStale: windowSeq={WindowSequence}, currentSeq={CurrentSequence}",
            window?.Sequence ?? 0,
            resetSequence);
        return;
    }

    RecordAskEndSequence(window.Sequence);
    if (!TryRunResetCleanupOnce())
    {
        CompletePlcReset(window.Sequence);
        return;
    }

    var context = CaptureAndClearState(); // ClearStateOnReset() + ClearAllExceptScan(SequenceClearMode.OperationalReset)
    var allowDialog = ShouldShowInterruptDialog(context)
        && Volatile.Read(ref _interruptDialogAllowedSequence) == window.Sequence;
    if (allowDialog)
    {
        await TryShowInterruptDialogAsync(context.SerialNumber!);
    }
    CompletePlcReset(window.Sequence);
}
```

`TryRunResetCleanupOnce()` общий для AskEnd и HardResetExit, поэтому повторная очистка не выполняется.

## Как сигналы устанавливают состояние

### Soft Reset (PlcResetCoordinator.OnForceStop)

```csharp
private void HandleSoftStop()
{
    BeginPlcReset();
    HandleStopSignal(PreExecutionResolution.SoftStop);
}

private void HandleStopSignal(PreExecutionResolution resolution)
{
    infra.StepTimingService.PauseAllColumnsTiming();
    var exitReason = resolution == PreExecutionResolution.SoftStop
        ? CycleExitReason.SoftReset
        : CycleExitReason.HardReset;
    var stopReason = resolution == PreExecutionResolution.SoftStop
        ? ExecutionStopReason.PlcSoftReset
        : ExecutionStopReason.PlcHardReset;

    state.FlowState.RequestStop(stopReason, stopAsFailure: true);
    SignalReset(exitReason);
    StopChangeoverTimerForReset(GetChangeoverResetMode());
    if (TryCancelActiveOperation(exitReason))
    {
        // Очистка произойдёт в HandleCycleExit
    }
    else
    {
        // Нет активной операции — очищаем сразу
        HandleCycleExit(exitReason);
    }
    SignalResolution(resolution);
}
```

### Hard Reset (ErrorCoordinator.OnReset)

```csharp
private void HandleHardReset()
{
    TryCompletePlcReset();
    var isPending = Interlocked.Exchange(ref coordinators.PlcResetCoordinator.PlcHardResetPending, 0);
    var origin = isPending == 1 ? ResetOriginPlc : ResetOriginNonPlc;
    Volatile.Write(ref _lastHardResetOrigin, origin);
    if (origin == ResetOriginNonPlc)
    {
        BeginResetCycle(ResetOriginNonPlc, ensureAskEndWindow: false);
    }
    HandleStopSignal(PreExecutionResolution.HardReset);
}
```

Старт reset-cycle выполняется только через `BeginResetCycle(origin, ensureAskEndWindow)`:
- PLC-путь: `BeginPlcReset()` → `BeginResetCycle(ResetOriginPlc, true)` (`seq++`, rearm one-shot cleanup, открытие AskEnd-окна).
- non-PLC hard reset: `HandleHardReset()` → `BeginResetCycle(ResetOriginNonPlc, false)` (`seq++`, rearm one-shot cleanup, без AskEnd-окна).

## Сравнение SoftReset vs HardReset

| Аспект | SoftReset | HardReset |
|--------|-----------|-----------|
| Когда очистка | По AskEnd (`ExecuteGridClearAsync`) | В `HandleHardResetExit` как fallback, если AskEnd ещё не очистил |
| One-shot защита | `TryRunResetCleanupOnce()` | Тот же guard, повторной очистки нет |
| Визуально | Синхронно с гридом | UI очищается сразу, данные — один раз по общему guard |

## Добавление нового состояния

### Шаг 1: Добавить в enum

```csharp
// PreExecutionCoordinator.cs
public enum CycleExitReason
{
    PipelineFailed,
    PipelineCancelled,
    TestCompleted,
    SoftReset,
    HardReset,
    NewReason,          // ← Новое состояние
}
```

### Шаг 2: Добавить обработку

```csharp
// PreExecutionCoordinator.MainLoop.cs
private void HandleCycleExit(CycleExitReason reason)
{
    switch (reason)
    {
        // ... существующие case ...

        case CycleExitReason.NewReason:
            HandleNewReasonExit(); // Выделенный handler вместо inline-логики
            break;
    }
}
```

Если новое состояние относится к reset-flow, очистку нужно проводить через общий one-shot guard (`TryRunResetCleanupOnce()`), а не прямым вызовом `ClearStateOnReset()` без guard.

### Шаг 3: Добавить источник сигнала

```csharp
// PreExecutionCoordinator.Subscriptions.cs — подписка на событие
private void SubscribeToStopSignals()
{
    coordinators.PlcResetCoordinator.OnForceStop += HandleSoftStop;
    coordinators.PlcResetCoordinator.OnAskEndReceived += HandleGridClear;
    coordinators.ErrorCoordinator.OnReset += HandleHardReset;
    someService.OnNewSignal += HandleNewSignal;  // ← Новая подписка
}

private void HandleNewSignal()
{
    if (TryCancelActiveOperation(CycleExitReason.NewReason))
    {
        // Очистка отложена
    }
    else
    {
        HandleCycleExit(CycleExitReason.NewReason);
    }
}
```

## Ключевые файлы

| Файл | Содержимое |
|------|------------|
| `PreExecutionCoordinator.cs` | Enum `CycleExitReason`, one-shot guard (`ArmResetCleanupGuard`, `TryRunResetCleanupOnce`) |
| `PreExecutionCoordinator.MainLoop.cs` | `ExecuteCycleAsync`, `HandleCycleExit`, repeat/scan переключение, выходы по reset/test completion |
| `PreExecutionCoordinator.Pipeline.cs` | `ExecutePreExecutionPipelineAsync`, `ExecuteRepeatPipelineAsync`, `ExecuteNokRepeatPipelineAsync` |
| `PreExecutionCoordinator.Pipeline.Helpers.cs` | `InitializeTestRunningAsync` (централизованный старт + запись scan-служебных результатов) |
| `PreExecutionCoordinator.Subscriptions.cs` | `HandleStopSignal`, `HandleGridClear`, stale AskEnd filter |
| `PreExecutionCoordinator.CycleExit.cs` | `HandleSoftResetExit`, `HandleHardResetExit` (fallback cleanup) |

## Гарантии очистки

| Сценарий | Путь очистки |
|----------|--------------|
| **SoftReset** во время ожидания баркода | `HandleGridClear` (по AskEnd) + `TryRunResetCleanupOnce()` |
| **SoftReset** во время pipeline | `HandleGridClear` (по AskEnd) + `TryRunResetCleanupOnce()` |
| **SoftReset** во время теста | `HandleGridClear` (по AskEnd) + `TryRunResetCleanupOnce()` |
| **HardReset** без AskEnd cleanup | `HandleHardResetExit` выполняет fallback cleanup через тот же guard |
| **Дублированный/устаревший AskEnd** | Игнорируется (`_currentAskEndWindow` отсутствует или `window.Sequence != currentSeq`) |
| Нормальное завершение теста | `HandleCycleExit` → `HandleTestCompletedExit` → `ClearForTestCompletion()` → `ClearAllExceptScan(SequenceClearMode.CompletedTest)` |

## Отладка

Для отслеживания состояний добавьте лог:

```csharp
private void HandleCycleExit(CycleExitReason reason)
{
    infra.Logger.LogInformation("HandleCycleExit: {Reason}", reason);
    // ...
}

private void HandleGridClear()
{
    infra.Logger.LogInformation("HandleGridClear: очистка по AskEnd");
    // ...
}
```
