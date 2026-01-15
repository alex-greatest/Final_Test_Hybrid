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
        └─ TestCompleted
```

## Обработка состояний

```csharp
private void HandleCycleExit(CycleExitReason reason)
{
    switch (reason)
    {
        case CycleExitReason.TestCompleted:
            HandleTestCompleted();  // Устанавливаем результат теста
            break;

        case CycleExitReason.SoftReset:
            // Ничего - очистка произойдёт по AskEnd в HandleGridClear
            break;

        case CycleExitReason.HardReset:
            ClearStateOnReset();
            infra.StatusReporter.ClearAllExceptScan();
            break;

        case CycleExitReason.PipelineFailed:
        case CycleExitReason.PipelineCancelled:
            // Ничего — состояние сохраняется
            break;
    }
}
```

## Очистка по AskEnd (HandleGridClear)

При soft reset очистка состояния и грида происходит **одновременно** по сигналу `OnAskEndReceived`:

```csharp
private void HandleGridClear()
{
    ClearStateOnReset();  // Очищает серийный номер, BoilerState, CurrentBarcode
    infra.StatusReporter.ClearAllExceptScan();  // Очищает грид
}
```

**Почему так:** Визуальная синхронизация — серийный номер и грид очищаются одновременно.

## Как сигналы устанавливают состояние

### Soft Reset (PlcResetCoordinator.OnForceStop)

```csharp
private void HandleSoftStop() => HandleStopSignal(PreExecutionResolution.SoftStop);

private void HandleStopSignal(PreExecutionResolution resolution)
{
    var exitReason = resolution == PreExecutionResolution.SoftStop
        ? CycleExitReason.SoftReset
        : CycleExitReason.HardReset;

    if (TryCancelActiveOperation(exitReason))
    {
        // _pendingExitReason установлен, очистка в HandleCycleExit (для HardReset)
        // или в HandleGridClear (для SoftReset)
    }
    else
    {
        HandleCycleExit(exitReason);
    }
}
```

### Hard Reset (ErrorCoordinator.OnReset)

```csharp
private void HandleHardReset()
{
    HandleStopSignal(PreExecutionResolution.HardReset);
    infra.StatusReporter.ClearAllExceptScan();
}
```

## Сравнение SoftReset vs HardReset

| Аспект | SoftReset | HardReset |
|--------|-----------|-----------|
| Когда очистка | По AskEnd (HandleGridClear) | Сразу в HandleCycleExit |
| Что очищается | Всё одновременно | Всё сразу |
| Визуально | Синхронно с гридом | Мгновенно |

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
            // Логика очистки для нового состояния
            ClearStateOnReset();
            // Дополнительные действия если нужно
            break;
    }
}
```

### Шаг 3: Добавить источник сигнала

```csharp
// PreExecutionCoordinator.Retry.cs — подписка на событие
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
| `PreExecutionCoordinator.cs` | Enum `CycleExitReason`, поле `_pendingExitReason` |
| `PreExecutionCoordinator.MainLoop.cs` | `ExecuteCycleAsync`, `HandleCycleExit`, `HandleTestCompleted` |
| `PreExecutionCoordinator.Retry.cs` | `HandleStopSignal`, `TryCancelActiveOperation`, `HandleGridClear` |

## Гарантии очистки

| Сценарий | Путь очистки |
|----------|--------------|
| **SoftReset** во время ожидания баркода | `HandleGridClear` (по AskEnd) |
| **SoftReset** во время pipeline | `HandleGridClear` (по AskEnd) |
| **SoftReset** во время теста | `HandleGridClear` (по AskEnd) |
| **HardReset** в любой момент | `HandleCycleExit` сразу |
| Нормальное завершение теста | `HandleCycleExit` → `HandleTestCompleted` |

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
