# CancellationGuide.md

Подробное руководство по системе прерывания и отмены в Final_Test_Hybrid.

> **См. также:** [CLAUDE.md](../../CLAUDE.md), [StepsGuide.md](StepsGuide.md), [PlcResetGuide.md](../runtime/PlcResetGuide.md)

---

## Обзор архитектуры

Система прерывания состоит из нескольких связанных компонентов:

```
┌─────────────────────────────────────────────────────────────────┐
│  CancellationTokenSource                                         │
│  └── Основной токен отмены для теста                            │
├─────────────────────────────────────────────────────────────────┤
│  PauseTokenSource                                                │
│  └── Пауза без отмены (Auto OFF)                                │
├─────────────────────────────────────────────────────────────────┤
│  AsyncManualResetEvent (_continueGate)                           │
│  └── Блокировка при ошибке шага (HasFailed=true)                │
├─────────────────────────────────────────────────────────────────┤
│  ExecutionFlowState                                              │
│  └── Флаги прерывания (StopRequested, StopAsFailure, StopReason)│
├─────────────────────────────────────────────────────────────────┤
│  ExecutionStateManager                                           │
│  └── Глобальное состояние выполнения                            │
└─────────────────────────────────────────────────────────────────┘
```

---

## CancellationToken Flow

Поток CancellationToken от создания до шага:

```
PreExecutionCoordinator                    TestExecutionCoordinator
        │                                            │
   _currentCts.Token                            _cts.Token
        │                                            │
        ▼                                            ▼
PreExecutionStep.ExecuteAsync(ctx, ct)    ColumnExecutor.ExecuteMapAsync(map, ct)
                                                     │
                                                     ▼
                                          ITestStep.ExecuteAsync(context, ct)
                                                     │
                                          ┌──────────┴──────────┐
                                          ▼                     ▼
                                   context.OpcUa.*        context.TagWaiter.*
                                   context.DelayAsync()   context.DiagReader.*
```

**Кто создаёт CTS:**

| Координатор | CTS | Lifecycle |
|-------------|-----|-----------|
| `ScanModeController` | `_loopCts` | Создаётся при активации scan mode, отменяется при logout/disable scan mode |
| `PreExecutionCoordinator` | `_currentCts` | Создаётся на каждый цикл (после скана), отменяется при soft/hard reset |
| `TestExecutionCoordinator` | `_cts` | Создаётся для каждого запуска тестов, отменяется при stop/reset/timeout |

**Кто вызывает Cancel:**

| Событие | Что отменяется |
|---------|----------------|
| `PlcResetCoordinator.OnForceStop` | `_currentCts` (цикл), `_cts` (тест) |
| `ErrorCoordinator.OnReset` | `_currentCts` (цикл), `_cts` (тест) |
| Logout / disable scan mode | `_loopCts` (и как следствие `_currentCts`) |

---

## Компоненты системы

### CancellationTokenSource

Основной механизм отмены операций. Создаётся для каждого теста.

| Источник | CTS | Что отменяет |
|----------|-----|--------------|
| `TestExecutionCoordinator` | `_cts` | Текущий запуск тестов (maps) |
| `PreExecutionCoordinator` | `_currentCts` | Текущий цикл pre-exec/pipeline |
| `ScanModeController` | `_loopCts` | Главный цикл scan mode |

### ExecutionFlowState

Хранит флаги прерывания для координации между компонентами.

| Поле | Назначение |
|------|------------|
| `StopRequested` | Запрошена остановка |
| `StopAsFailure` | Остановка как ошибка (NOK) |
| `StopReason` | Причина остановки (для логов) |

**Важно:** `StopReason` используется только для логирования, `StopAsFailure` OR-ится при множественных вызовах.

**Важно (защита от ложного успеха):** `ExecutionFlowState` общий для цикла и может быть очищен (например, в начале нового цикла).
Поэтому `TestExecutionCoordinator` держит локальную «защёлку» (`StopReason/StopAsFailure`) на время прогона и использует её в `Complete()`.

### PauseTokenSource

Приостановка без отмены — используется при выключении Auto режима.

```csharp
// В шагах используется через context:
await context.PauseToken.WaitWhilePausedAsync(ct);
await context.DelayAsync(TimeSpan.FromSeconds(5), ct);  // Pause-aware delay
```

**Важно:** `Pausable*` сервисы автоматически приостанавливаются.

### AsyncManualResetEvent (_continueGate)

Блокирует выполнение колонки при ошибке шага до Retry/Skip.

| Метод | Когда |
|-------|-------|
| `Reset()` | При ошибке шага — блокирует gate |
| `Set()` | После Retry/Skip — открывает gate |

### ExecutionStateManager

Глобальное состояние выполнения (`Idle`, `Processing`, `Running`, `PausedOnError`, `Completed`, `Failed`).

---

## Soft Reset vs Hard Reset

| Тип | Условие | Метод | Поведение |
|-----|---------|-------|-----------|
| **Soft** | `wasInScanPhase=true` | `ForceStop()` | Resume → OnForceStop |
| **Hard** | `wasInScanPhase=false` | `Reset()` | Resume → OnReset → новый reset-cycle (`_resetSequence`) и one-shot очистка (`OnAskEndReceived` или `HandleHardResetExit`) |

**Soft Reset:** тест в фазе сканирования, минимальная очистка.
**Hard Reset:** тест не начался или уже завершён, полная очистка через защищённый one-shot путь.

Дополнительно для текущей реализации:
- AskEnd обрабатывается только для актуального `ResetAskEndWindow(seq)`; stale AskEnd игнорируется.
- Диалог причины прерывания разрешён не более одного раза на reset-серию (series-latch).
- Любой новый reset немедленно закрывает активный диалог причины (`CancelActiveDialog`).

---

## Accepted Patterns

> **Эти паттерны выглядят как потенциальные баги, но являются корректным поведением.**

### 1. Gate ждёт только при HasFailed=true

```csharp
// ColumnExecutor.ExecuteMapAsync
if (ct.IsCancellationRequested) break;  // ← Проверка ДО WaitAsync
await _continueGate.WaitAsync(ct);
```

**Почему OK:** При soft stop используется `ct.IsCancellationRequested → break` → `ClearStatusIfNotFailed()`.
Исключение от `_continueGate.WaitAsync(ct)` бросается только при `HasFailed=true`, и тогда `ClearStatusIfNotFailed()` всё равно ничего не делает.

### 2. Task.WhenAll без linked CTS

```csharp
// TestExecutionCoordinator
await Task.WhenAll(executorTasks);  // Без linked CTS
```

**Почему OK:** Все исключения из `step.ExecuteAsync()` ловятся внутри `ExecuteStepCoreAsync`.
Единственный риск — исключения ВНЕ try-блока (исправлено для `StartNewStep`).

### 3. Шаги без timeout защиты

**Почему OK:** Это документированный контракт (см. [StepsGuide.md](StepsGuide.md) Часть 7).
Шаги ОБЯЗАНЫ уважать `CancellationToken`. Timeout не поможет если шаг его игнорирует —
система не может безопасно прервать выполняющийся код без кооперации шага.

### 4. DequeueError до фактического retry

```csharp
// ErrorCoordinator
var error = errorQueue.Dequeue();  // До retry
```

**Почему OK:** При Stop/Reset вызывается `SetMaps() → Reset()`, который очищает всё.
Ошибка не теряется — она или обрабатывается, или очищается вместе с состоянием.

### 5. Двойной Stop (OnForceStop + OnReset)

**Почему OK:**
- `StopReason` используется только для логов
- `StopAsFailure` OR-ится корректно (`|=`)
- Второй вызов `Stop()` игнорируется через проверку `IsCancellationRequested`

### 6. CTS.Dispose() без строгой синхронизации

**Почему OK:** `CancellationTokenSource.Dispose()` идемпотентен.
Double dispose безопасен. Теоретический race с `CancelExecution` маловероятен.

### 7. Проверка step ПОСЛЕ семафора

```csharp
// ColumnExecutor.RetryLastFailedStepAsync
await _retrySemaphore.WaitAsync(ct);
var step = _state.FailedStep;  // ← Проверка ПОСЛЕ семафора
if (step == null) return;
```

**Почему OK:** Это защита от TOCTOU (Time-Of-Check-Time-Of-Use).
`_state.FailedStep` может измениться пока ждём семафор.
Проверка ПОСЛЕ захвата семафора — правильно.

### 8. Диалоги без CancellationToken

**Почему OK:** Диалоги закрываются через события:
- `PrepareErrorDialog.razor` подписан на `OnReset` и `OnForceStop`
- При этих событиях вызывает `DialogService.Close(false)` автоматически

### 9. Сброс Start только при успехе (без finally)

**Подробно:** [StepsGuide.md](StepsGuide.md#часть-55-паттерн-сброса-start-тега)

При ошибке/retry/skip координатор сбрасывает Start через `ResetBlockStartAsync`.

---

## Anti-Patterns (НЕ делать)

### Игнорирование CancellationToken

```csharp
// ❌ ЗАПРЕЩЕНО
while (true)
{
    await Task.Delay(100);  // Без ct!
}

// ✅ ПРАВИЛЬНО
while (!ct.IsCancellationRequested)
{
    await Task.Delay(100, ct);
}
```

### Блокирующие вызовы

```csharp
// ❌ ЗАПРЕЩЕНО
var result = task.Result;
task.Wait();
Thread.Sleep(1000);

// ✅ ПРАВИЛЬНО
var result = await task;
await Task.Delay(1000, ct);
```

### Отмена без проверки контекста

```csharp
// ❌ ОПАСНО — может отменить не то
cts.Cancel();

// ✅ ПРАВИЛЬНО — проверить контекст
if (shouldCancel)
{
    cts.Cancel();
}
```

### Бесконечное ожидание без timeout

```csharp
// ❌ ОПАСНО
await tagWaiter.WaitForTrueAsync(tag);  // Навсегда?

// ✅ ПРАВИЛЬНО
await tagWaiter.WaitForTrueAsync(tag, timeout: TimeSpan.FromSeconds(30), ct);
```

---

## Checklist для новых шагов

При создании или ревью шагов проверять. См. также [StepsGuide.md](StepsGuide.md#часть-7-чек-листы).

### CancellationToken

| Требование | Проверка |
|------------|----------|
| Циклы | `ct.ThrowIfCancellationRequested()` в каждом `while`/`for` |
| Задержки | `context.DelayAsync()` или `Task.Delay(..., ct)` |
| I/O операции | Передают `ct` |
| Блокировки | Нет `.Result`, `.Wait()`, `Thread.Sleep()` |

### События и Cleanup

| Требование | Проверка |
|------------|----------|
| Event handlers | Защищены try-catch (если критичны) |
| Состояние | Атомарно или под lock |
| Ресурсы | `finally` или `using` |
| Retry | Нет утечек памяти при повторном входе |

---

## Примеры правильной обработки

### Простой шаг с ожиданием

```csharp
public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
{
    // 1. Запись в PLC
    var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
    if (writeResult.Error != null)
    {
        return TestStepResult.Fail(writeResult.Error);
    }

    // 2. Ожидание с ct
    var waitResult = await context.TagWaiter.WaitAnyAsync(
        context.TagWaiter.CreateWaitGroup<Result>()
            .WaitForTrue(EndTag, () => Result.Success, "End")
            .WaitForTrue(ErrorTag, () => Result.Error, "Error"),
        ct);

    // 3. Обработка результата
    return waitResult.Result switch
    {
        Result.Success => TestStepResult.Pass(),
        Result.Error => TestStepResult.Fail("Ошибка"),
        _ => TestStepResult.Fail("Неизвестный результат")
    };
}
```

### Шаг с циклом

```csharp
public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
{
    for (int i = 0; i < 10; i++)
    {
        ct.ThrowIfCancellationRequested();  // ← Обязательно!

        var result = await DoWorkAsync(context, ct);
        if (!result.Success)
        {
            return TestStepResult.Fail(result.Error);
        }

        await context.DelayAsync(TimeSpan.FromSeconds(1), ct);  // Pause-aware
    }

    return TestStepResult.Pass();
}
```

### Шаг с cleanup

```csharp
public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
{
    try
    {
        await context.OpcUa.WriteAsync(StartTag, true, ct);
        // ... работа ...
        return TestStepResult.Pass();
    }
    catch (OperationCanceledException)
    {
        // Отмена — cleanup и пробросить
        await TryResetStartTagAsync(context);
        throw;
    }
    finally
    {
        // Гарантированный cleanup
        await TryResetStartTagAsync(context);
    }
}

private async Task TryResetStartTagAsync(TestStepContext context)
{
    try
    {
        // Без ct — должен выполниться даже при отмене
        await context.OpcUa.WriteAsync(StartTag, false, CancellationToken.None);
    }
    catch
    {
        // Игнорируем ошибки cleanup
    }
}
```

---

## Диагностика проблем

### Тест не останавливается

1. Проверить шаг на игнорирование `ct`
2. Проверить бесконечные циклы без `ct.ThrowIfCancellationRequested()`
3. Проверить `Task.Delay()` без `ct`

### Колонка "зависла"

1. Проверить `_continueGate` — возможно `HasFailed=true` и ждёт Retry/Skip
2. Проверить логи на исключения в `StartNewStep` или событиях

### Ошибка теряется

1. Проверить что вызывается `SetErrorState` при ошибке
2. Проверить что не происходит `Reset()` до обработки ошибки

---

## Ссылки

- [StepsGuide.md](StepsGuide.md) — создание шагов, контракт CancellationToken
- [PlcResetGuide.md](../runtime/PlcResetGuide.md) — Soft/Hard Reset от PLC
- [RetrySkipGuide.md](RetrySkipGuide.md) — механизм повтора и пропуска
- [StateManagementGuide.md](StateManagementGuide.md) — управление состоянием
