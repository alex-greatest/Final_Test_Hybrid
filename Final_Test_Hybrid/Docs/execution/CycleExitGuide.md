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

    var context = CaptureAndClearState(); // ClearStateOnReset() + ClearAllExceptScan()
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
| Нормальное завершение теста | `HandleCycleExit` → `HandleTestCompletedExit` |

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
