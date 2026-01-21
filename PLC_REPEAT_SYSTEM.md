# Система завершения и повторов тестов (PLC Repeat System)

Документация по обмену сигналами с ПЛК и механизму завершения/повторов тестов.

---

## 1. Обзор системы сигналов ПЛК

### 1.1 Таблица сигналов

| Сигнал | Константа в коде | OPC-UA Node ID | Направление | Описание |
|--------|------------------|----------------|-------------|----------|
| **Req_Reset** | `BaseTags.ReqReset` | `ns=3;s="DB_Station"."Test"."Req_Reset"` | PLC → PC | Запрос сброса от ПЛК |
| **Req_Repeat** | `BaseTags.ErrorRetry` | `ns=3;s="DB_Station"."Test"."Req_Repeat"` | PLC → PC | Запрос повтора (при ошибке или после теста) |
| **End** | `BaseTags.ErrorSkip` | `ns=3;s="DB_Station"."Test"."End"` | PC → PLC | Сигнал завершения теста / пропуска шага |
| **Ask_End** | `BaseTags.AskEnd` | `ns=3;s="DB_Station"."Test"."Ask_End"` | PLC → PC | Подтверждение от ПЛК что сброс обработан |
| **Ask_Repeat** | `BaseTags.AskRepeat` | `ns=3;s="DB_Station"."Test"."Ask_Repeat"` | PC → PLC | PC подтверждает повтор |
| **Ask_Auto** | `BaseTags.TestAskAuto` | `ns=3;s="DB_Station"."Test"."Ask_Auto"` | PC → PLC | Запрос автоматического режима |
| **Reset** | `BaseTags.Reset` | `ns=3;s="DB_Station"."Test"."Reset"` | PC → PLC | PC сигнализирует о сбросе |
| **Fault** | `BaseTags.Fault` | `ns=3;s="DB_Station"."Test"."Fault"` | PC → PLC | Ошибка шага без блока |
| **EndStep** | `BaseTags.TestEndStep` | `ns=3;s="DB_Station"."Test"."EndStep"` | PLC → PC | Подтверждение Skip для шага без блока |

### 1.2 Определения в коде

```csharp
// Final_Test_Hybrid/Models/Plc/Tags/BaseTags.cs
public static class BaseTags
{
    // Error handling tags
    public const string ErrorRetry = "ns=3;s=\"DB_Station\".\"Test\".\"Req_Repeat\"";
    public const string ErrorSkip = "ns=3;s=\"DB_Station\".\"Test\".\"End\"";
    public const string AskRepeat = "ns=3;s=\"DB_Station\".\"Test\".\"Ask_Repeat\"";

    // Reset handling tags
    public const string ReqReset = "ns=3;s=\"DB_Station\".\"Test\".\"Req_Reset\"";
    public const string Reset = "ns=3;s=\"DB_Station\".\"Test\".\"Reset\"";
    public const string AskEnd = "ns=3;s=\"DB_Station\".\"Test\".\"Ask_End\"";
}
```

---

## 2. Сценарии завершения теста

### 2.1 Результаты завершения (CompletionResult)

| Результат | Enum | Описание |
|-----------|------|----------|
| Завершён | `CompletionResult.Finished` | Тест завершён, результат сохранён |
| OK повтор | `CompletionResult.RepeatRequested` | Оператор запросил повтор OK теста |
| NOK повтор | `CompletionResult.NokRepeatRequested` | Оператор запросил повтор NOK теста |
| Отменён | `CompletionResult.Cancelled` | Операция отменена (сброс) |

### 2.2 Причины выхода из цикла (CycleExitReason)

```csharp
public enum CycleExitReason
{
    PipelineFailed,        // Pipeline вернул ошибку
    PipelineCancelled,     // Pipeline отменён (не сброс)
    TestCompleted,         // Тест завершился нормально
    SoftReset,             // Мягкий сброс (wasInScanPhase = true)
    HardReset,             // Жёсткий сброс
    RepeatRequested,       // OK повтор теста
    NokRepeatRequested,    // NOK повтор с подготовкой
}
```

---

## 3. Диаграммы потоков

### 3.1 Нормальное завершение теста

```
┌─────────────────────────────────────────────────────────────────┐
│  TestExecutionCoordinator завершает тест                        │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  PreExecutionCoordinator.HandleTestCompletionAsync()            │
│  ├─ StopTestTimer()                                             │
│  ├─ SetTestResult(1 или 2)                                      │
│  ├─ ShowResultImage()                                           │
│  └─ CreateLinkedTokenSource(ct, _resetCts.Token) // ⚡ Reset    │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  TestCompletionCoordinator.HandleTestCompletedAsync()           │
│  ├─ WriteAsync(End, true)         // Сигнал завершения          │
│  ├─ WaitForFalseAsync(End)        // Ждём PLC сбросит End ⚡    │
│  ├─ Delay(1000ms)                 // Даём PLC время             │
│  └─ GetValue(Req_Repeat)          // Читаем запрос повтора      │
└─────────────────────────────────────────────────────────────────┘
                            │
              ┌─────────────┼─────────────┐
              │             │             │
              ▼             ▼             ▼
┌──────────────────┐ ┌────────────┐ ┌─────────────────────┐
│  Req_Repeat?     │ │  ⚡ Reset  │ │  Req_Repeat = true  │
│  = false         │ │  (OCE)     │ │  HandleRepeatAsync()│
│  HandleFinish    │ │  return    │ │  └─ OK или NOK?     │
│  Async()         │ │  Cancelled │ └─────────────────────┘
│  └─ TrySave      │ └────────────┘           │
│  └─ Finished     │                          │
└──────────────────┘      ┌───────────────────┴───────────┐
                          ▼                               ▼
                ┌─────────────────┐       ┌─────────────────────┐
                │ testResult == 1 │       │ testResult == 2     │
                │ (OK)            │       │ (NOK)               │
                │ WriteAsync(     │       │ HandleNokRepeatAsync│
                │   AskRepeat,    │       │ ├─ TrySaveAsync()   │
                │   true)         │       │ └─ WriteAsync(      │
                │ return Repeat   │       │     AskRepeat, true)│
                │ Requested       │       │ return NokRepeat    │
                └─────────────────┘       │ Requested           │
                                          └─────────────────────┘
```

**⚡ Прерывание Reset:** Ожидание `End=false` связано с `_resetCts` через linked token. При Reset ожидание прерывается, возвращается `CompletionResult.Cancelled` → `CycleExitReason.SoftReset`.

### 3.2 Поток OK повтора (быстрый, переиспользование контекста)

```
┌─────────────────────────────────────────────────────────────────┐
│  CompletionResult.RepeatRequested                               │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  HandleCycleExit(RepeatRequested)                               │
│  ├─ ClearForRepeat()                                            │
│  ├─ _skipNextScan = true                                        │
│  └─ _executeFullPreparation = false                             │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  RunSingleCycleAsync() [следующая итерация]                     │
│  ├─ _skipNextScan == true?                                      │
│  │   └─ Пропускаем WaitForBarcodeAsync()                        │
│  │   └─ barcode = CurrentBarcode                                │
│  └─ ExecuteCycleAsync()                                         │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  ExecuteRepeatPipelineAsync()                                   │
│  ├─ context = _lastSuccessfulContext  // Переиспользуем!        │
│  ├─ ClearForNewTestStart()                                      │
│  ├─ IsHistoryEnabled = true                                     │
│  ├─ ExecuteStartTimer1Async()                                   │
│  ├─ ExecuteBlockBoilerAdapterAsync()                            │
│  └─ StartTestExecution(context)                                 │
└─────────────────────────────────────────────────────────────────┘
```

**Ключевое отличие OK повтора:** Используется `_lastSuccessfulContext` — ScanStep пропускается, данные уже загружены.

### 3.3 Поток NOK повтора (полная подготовка)

```
┌─────────────────────────────────────────────────────────────────┐
│  CompletionResult.NokRepeatRequested                            │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  HandleCycleExit(NokRepeatRequested)                            │
│  ├─ ClearForNokRepeat()                                         │
│  ├─ _skipNextScan = true                                        │
│  └─ _executeFullPreparation = true                              │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  RunSingleCycleAsync() [следующая итерация]                     │
│  ├─ _skipNextScan == true?                                      │
│  │   └─ Пропускаем WaitForBarcodeAsync()                        │
│  │   └─ barcode = CurrentBarcode                                │
│  └─ ExecuteCycleAsync()                                         │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  ExecuteNokRepeatPipelineAsync()                                │
│  └─ while (!ct.IsCancellationRequested)                         │
│     ├─ ExecutePreExecutionPipelineAsync(CurrentBarcode)         │
│     │   └─ ПОЛНЫЙ pipeline включая ScanStep!                    │
│     ├─ result.Status == TestStarted? → return                   │
│     └─ ShowPrepareErrorDialogAsync() → retry loop               │
└─────────────────────────────────────────────────────────────────┘
```

**Ключевое отличие NOK повтора:** Выполняется **полный pipeline** включая ScanStep — данные загружаются заново из MES/БД.

### 3.4 Поток PLC сброса

```
┌─────────────────────────────────────────────────────────────────┐
│  PLC устанавливает Req_Reset = true                             │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  ResetSubscription → OnStateChanged                             │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  PlcResetCoordinator.HandleResetAsync()                         │
│  ├─ InvokeEvent(OnResetStarting) → wasInScanPhase               │
│  ├─ InvokeEvent(OnForceStop)                                    │
│  │   ├─ TestExecutionCoordinator.Stop()                         │
│  │   ├─ PreExecutionCoordinator.HandleSoftStop()                │
│  │   └─ ReworkDialogService.Close()                             │
│  ├─ SendDataToMesAsync()                                        │
│  ├─ WriteAsync(Reset, true)                                     │
│  └─ WaitForTrueAsync(AskEnd)                                    │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  ExecuteSmartReset(wasInScanPhase)                              │
│  ├─ wasInScanPhase == true?                                     │
│  │   └─ ErrorCoordinator.ForceStop()  // Мягкий сброс           │
│  └─ wasInScanPhase == false?                                    │
│      └─ ErrorCoordinator.Reset()      // Жёсткий сброс          │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  InvokeEvent(OnResetCompleted)                                  │
│  └─ ScanModeController.TransitionToReadyInternal()              │
└─────────────────────────────────────────────────────────────────┘
```

### 3.5 Reset во время ожидания End=false

```
┌─────────────────────────────────────────────────────────────────┐
│  Тест завершён → PC записал End = true                          │
│  PC ждёт End = false от PLC                                     │
└─────────────────────────────────────────────────────────────────┘
                            │
              ┌─────────────┴─────────────┐
              ▼                           ▼
┌─────────────────────────┐   ┌─────────────────────────┐
│  PLC сбросил End        │   │  Req_Reset пришёл       │
│  (нормальный путь)      │   │  (прерывание)           │
└─────────────────────────┘   └─────────────────────────┘
              │                           │
              ▼                           ▼
┌─────────────────────────┐   ┌─────────────────────────┐
│  Delay(1000ms)          │   │  _resetCts.Cancel()     │
│  Читаем Req_Repeat      │   │  linked.Token отменён   │
│  → Finish/Repeat        │   │  OCE в TagWaiter        │
└─────────────────────────┘   └─────────────────────────┘
                                          │
                                          ▼
                              ┌─────────────────────────┐
                              │  catch (OCE)            │
                              │  return Cancelled       │
                              └─────────────────────────┘
                                          │
                                          ▼
                              ┌─────────────────────────┐
                              │  HandleCycleExit        │
                              │  (Soft или HardReset)   │
                              │  → Ждём AskEnd          │
                              │  → Разблокировка поля   │
                              └─────────────────────────┘
```

**Ключевой момент:** Linked token (`ct` + `_resetCts.Token`) позволяет прервать бесконечное ожидание `WaitForFalseAsync(End)` при Reset, обеспечивая мгновенную разблокировку поля ввода после AskEnd.

**Для обоих типов сброса:** `OnForceStop` вызывается ВСЕГДА (и для soft, и для hard), что приводит к `_resetCts.Cancel()` ДО PLC коммуникации. Жёсткий сброс дополнительно вызывает `OnReset` → `HandleHardReset()`, но прерывание End уже произошло.

---

## 4. Механизм флагов

### 4.1 Флаги управления повтором

| Флаг | Тип | Описание |
|------|-----|----------|
| `_skipNextScan` | `bool` | Пропустить ожидание сканирования в следующем цикле |
| `_executeFullPreparation` | `bool` | Выполнить полную подготовку (NOK) вместо быстрой (OK) |
| `_lastSuccessfulContext` | `PreExecutionContext?` | Сохранённый контекст для OK повтора |

### 4.2 Логика установки флагов

```csharp
private void HandleCycleExit(CycleExitReason reason)
{
    switch (reason)
    {
        case CycleExitReason.RepeatRequested:
            ClearForRepeat();
            _skipNextScan = true;
            // _executeFullPreparation остаётся false
            break;

        case CycleExitReason.NokRepeatRequested:
            ClearForNokRepeat();
            _skipNextScan = true;
            _executeFullPreparation = true;
            break;
        // ...
    }
}
```

### 4.3 Логика использования флагов

```csharp
private async Task<CycleExitReason> ExecuteCycleAsync(...)
{
    var isRepeat = _skipNextScan;
    var isNokRepeat = _skipNextScan && _executeFullPreparation;
    _skipNextScan = false;
    _executeFullPreparation = false;

    var result = isNokRepeat
        ? await ExecuteNokRepeatPipelineAsync(ct)          // Полная подготовка
        : isRepeat
            ? await ExecuteRepeatPipelineAsync(ct)         // Быстрый повтор
            : await ExecutePreExecutionPipelineAsync(barcode, ct); // Нормальный запуск
    // ...
}
```

### 4.4 Сохранение контекста для OK повтора

```csharp
// В ExecutePreExecutionPipelineAsync после успешного ScanStep:
_lastSuccessfulContext = context;

// В ExecuteRepeatPipelineAsync:
var context = _lastSuccessfulContext;
if (context?.Maps == null)
{
    return PreExecutionResult.Fail("Нет данных для повтора");
}
// Используем сохранённый контекст без повторного ScanStep
```

---

## 5. Очистка состояния

### 5.1 Таблица очистки по сценариям

| Элемент | TestCompleted | SoftReset | HardReset | RepeatRequested | NokRepeatRequested |
|---------|:-------------:|:---------:|:---------:|:---------------:|:------------------:|
| **BoilerState.Clear()** | ✓ | ✓* | ✓ | - | ✓ |
| **PhaseState.Clear()** | - | ✓* | ✓ | - | ✓ |
| **CurrentBarcode** | ✓ | ✓* | ✓ | - | - |
| **StatusReporter.ClearAllExceptScan()** | ✓ | ✓* | ✓ | ✓ | ✓ |
| **StepTimingService.Clear()** | ✓ | ✓ | ✓ | ✓** | ✓** |
| **RecipeProvider.Clear()** | ✓ | ✓ | ✓ | - | ✓ |
| **IsHistoryEnabled = false** | ✓ | ✓ | ✓ | ✓ | ✓ |
| **_lastSuccessfulContext = null** | - | ✓ | ✓ | - | ✓ |
| **ErrorService.ClearHistory()** | - | - | - | ✓*** | ✓*** |
| **TestResultsService.Clear()** | - | - | - | ✓*** | ✓*** |

*\* При SoftReset очистка происходит по сигналу AskEnd (HandleGridClear)*
*\*\* С параметром `preserveScanState: true`*
*\*\*\* Очистка в ClearForNewTestStart() при запуске pipeline*

### 5.2 Методы очистки

#### ClearForTestCompletion

```csharp
private void ClearForTestCompletion()
{
    infra.StatusReporter.ClearAllExceptScan();
    infra.StepTimingService.Clear();
    infra.RecipeProvider.Clear();
    state.BoilerState.Clear();
    ClearBarcode();
    infra.ErrorService.IsHistoryEnabled = false;
}
```

#### ClearStateOnReset

```csharp
private void ClearStateOnReset()
{
    state.BoilerState.Clear();
    state.PhaseState.Clear();
    ClearBarcode();
    infra.ErrorService.IsHistoryEnabled = false;
    infra.StepTimingService.Clear();
    infra.RecipeProvider.Clear();
    _lastSuccessfulContext = null;
}
```

#### ClearForRepeat

```csharp
private void ClearForRepeat()
{
    infra.ErrorService.IsHistoryEnabled = false;
    infra.StatusReporter.ClearAllExceptScan();
    infra.StepTimingService.Clear(preserveScanState: true);
    coordinators.TestCoordinator.ResetForRepeat();
}
```

#### ClearForNokRepeat

```csharp
private void ClearForNokRepeat()
{
    state.BoilerState.Clear();
    state.PhaseState.Clear();
    infra.ErrorService.IsHistoryEnabled = false;
    _lastSuccessfulContext = null;
    infra.StatusReporter.ClearAllExceptScan();
    infra.StepTimingService.Clear(preserveScanState: true);
    infra.RecipeProvider.Clear();
    coordinators.TestCoordinator.ResetForRepeat();
}
```

#### ClearForNewTestStart

```csharp
private void ClearForNewTestStart()
{
    infra.ErrorService.ClearHistory();
    infra.TestResultsService.Clear();
}
```

---

## 6. Ключевые файлы

| Файл | Описание |
|------|----------|
| `Models/Plc/Tags/BaseTags.cs` | Определения OPC-UA тегов |
| `Services/Main/PlcReset/PlcResetCoordinator.cs` | Координатор сброса по сигналу PLC |
| `Services/Main/PlcReset/ResetSubscription.cs` | Подписка на Req_Reset |
| `Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.cs` | Базовый класс координатора завершения |
| `Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.Flow.cs` | Логика завершения и выбора повтора |
| `Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.Repeat.cs` | Обработка NOK повтора |
| `Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.cs` | Базовый класс PreExecution (enum, clear методы) |
| `Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.MainLoop.cs` | Главный цикл, HandleCycleExit |
| `Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Pipeline.cs` | Pipeline'ы (Normal, Repeat, NokRepeat) |
| `Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Retry.cs` | Обработка сигналов сброса, retry loop |

---

## 7. Важные детали реализации

### 7.1 Защита от race condition при сбросе

```csharp
private async Task<CycleExitReason> ExecuteCycleAsync(...)
{
    // Создаём сигнал сброса для защиты от race condition
    var resetSignal = new TaskCompletionSource<CycleExitReason>(...);
    _resetSignal = resetSignal;

    // ... выполнение pipeline ...

    // Ждём завершения теста ИЛИ сигнала сброса
    var completedTask = await Task.WhenAny(testCompletionTcs.Task, resetSignal.Task);

    // Если сработал сброс - возвращаем причину сброса
    if (completedTask == resetSignal.Task)
    {
        return await resetSignal.Task;
    }
    // ...
}
```

### 7.2 Определение типа сброса (wasInScanPhase)

```csharp
// ScanModeController.cs
var wasInScanPhase = IsInScanningPhase;  // _isActivated && !_isResetting
```

- **wasInScanPhase = true** → Мягкий сброс (ForceStop)
- **wasInScanPhase = false** → Жёсткий сброс (Reset)

### 7.3 Прерывание ожидания End при Reset (linked token)

```csharp
// PreExecutionCoordinator.MainLoop.cs - HandleTestCompletionAsync
private async Task<CycleExitReason> HandleTestCompletionAsync(CancellationToken ct)
{
    // ... показ результата ...

    // Связываем с _resetCts чтобы Reset прерывал ожидание End
    var resetCts = _resetCts;
    CancellationTokenSource linked;
    try { linked = CancellationTokenSource.CreateLinkedTokenSource(ct, resetCts.Token); }
    catch (ObjectDisposedException)
    {
        // _resetCts disposed → reset уже завершён
        // Примечание: в узком окне между dispose и SignalReset может вернуться SoftReset
        // вместо HardReset — это допустимо, т.к. состояние очистится по AskEnd
        coordinators.CompletionUiState.HideImage();
        return TryGetStopExitReason(out var exitReason) ? exitReason : CycleExitReason.SoftReset;
    }

    try
    {
        var result = await coordinators.CompletionCoordinator
            .HandleTestCompletedAsync(testResult, linked.Token);
        // ... обработка результата ...
    }
    catch (OperationCanceledException)
    {
        // Reset или внешняя отмена прервали ожидание End
        // При shutdown тоже вернётся SoftReset — это допустимо
        return TryGetStopExitReason(out var exitReason) ? exitReason : CycleExitReason.SoftReset;
    }
    finally
    {
        linked.Dispose();
        coordinators.CompletionUiState.HideImage();
    }
}

// TestCompletionCoordinator.Flow.cs - HandleTestCompletedAsync
public async Task<CompletionResult> HandleTestCompletedAsync(int testResult, CancellationToken ct)
{
    try
    {
        await deps.PlcService.WriteAsync(BaseTags.ErrorSkip, true, ct);
        await deps.TagWaiter.WaitForFalseAsync(BaseTags.ErrorSkip, timeout: null, ct); // ⚡ Прерываемо
        // ... обработка Req_Repeat ...
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Ожидание End прервано");
        return CompletionResult.Cancelled;
    }
    finally
    {
        IsWaitingForCompletion = false;
    }
}
```

**Важно:** Защита от `ObjectDisposedException` при создании linked token — если `_resetCts` уже disposed, значит reset завершён и нужно выходить.

### 7.4 ReworkDialog при NOK повторе

ReworkDialog **НЕ** показывается сразу при NOK повторе. Он показывается в `ScanBarcodeMesStep` только если MES сервер вернёт `RequiresRework = true`.

```csharp
// HandleNokRepeatAsync - БЕЗ ReworkDialog
private async Task<CompletionResult> HandleNokRepeatAsync(CancellationToken ct)
{
    var saved = await TrySaveWithRetryAsync(2, ct);
    if (!saved) return CompletionResult.Cancelled;

    // ReworkDialog будет показан в ScanBarcodeMesStep если MES потребует
    await deps.PlcService.WriteAsync(BaseTags.AskRepeat, true, ct);
    return CompletionResult.NokRepeatRequested;
}
```

---

## 8. Связанная документация

- [PlcResetGuide.md](Final_Test_Hybrid/PlcResetGuide.md) — Детали логики сброса
- [CycleExitGuide.md](Final_Test_Hybrid/CycleExitGuide.md) — Управление состояниями выхода
- [RetrySkipGuide.md](Final_Test_Hybrid/RetrySkipGuide.md) — Повтор и пропуск шагов
- [ErrorCoordinatorGuide.md](Final_Test_Hybrid/ErrorCoordinatorGuide.md) — Координатор прерываний
