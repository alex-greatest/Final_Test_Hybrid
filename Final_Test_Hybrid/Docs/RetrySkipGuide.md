# RetrySkipGuide.md — Логика повтора и пропуска шагов

## PLC Теги

| Тег | Адрес | Направление | Назначение |
|-----|-------|-------------|------------|
| **ErrorRetry** | `DB_Station.Test.Req_Repeat` | PLC → PC | Оператор нажал "Повтор" |
| **AskRepeat** | `DB_Station.Test.Ask_Repeat` | PC → PLC | PC готов к повтору |
| **Fault** | `DB_Station.Test.Fault` | PC → PLC | Ошибка шага без блока |
| **EndStep** | `DB_Station.Test.EndStep` | PLC → PC | PLC подтверждает Skip для шага без блока |
| **Block.Selected** | `DB_VI.Block_X.Selected` | PC → PLC | Какой блок в ошибке |
| **Block.Error** | `DB_VI.Block_X.Error` | PLC → PC | Флаг ошибки блока |
| **Block.End** | `DB_VI.Block_X.End` | PLC → PC | Флаг завершения блока (в т.ч. для Skip) |

> **Примечание:** тег `DB_Station.Test.End` (`BaseTags.ErrorSkip`) используется в completion-flow (завершение теста) и в механизме Skip шага не участвует.

## Retry Flow (Повтор)

```
[1] Ошибка
    PLC: Block.Error = true
    PC:  SetErrorState() → gate.Reset()
                ↓
[2] Подготовка диалога
    PC → PLC: Block.Selected = true
    PC:       Показывает диалог
                ↓
[3] Ожидание оператора
    PC ждёт: Req_Repeat=true ИЛИ Skip-сигнал
    Оператор: "Повтор"
    PLC → PC: Req_Repeat = true
                ↓
[4] Сигнал готовности
    PC → PLC: AskRepeat = true
    PLC:      Block.Error = false
    PC ждёт:  Block.Error = false
                ↓
[5] Фоновый retry (tracked task)
    PC: InvokeRetryStartedSafely() → панель закрывается
    PC: WaitForRetrySignalResetAsync() → Req_Repeat = false
    PC: DequeueError() → освобождает очередь
    PC: ExecuteRetryInBackgroundAsync() → фоновой retry (tracked)
                ↓
[6] Следующая ошибка (если есть)
    while цикл продолжается → диалог для следующей ошибки
    (не ждёт завершения retry!)
```

## Skip Flow (Пропуск)

| Тип шага | Условие Skip |
|----------|--------------|
| **С блоком** | `End=true AND Block.Error=true` |
| **Без блока** | `EndStep=true` |

```
[1-2] Ошибка + Диалог (как в Retry)
                ↓
[3] Оператор: "Один шаг"
    PLC → PC: EndStep = true (для шагов без блока)
    PC: проверяет Block.Error = true (для шагов с блоком)
                ↓
[4] Ожидание сброса сигналов (защита от stale)
    PC ждёт: Block.Error=false И Block.End=false (для шагов С блоком)
             ИЛИ EndStep=false (для шагов БЕЗ блока)
    Таймаут: 60 сек → жёсткий стоп теста
                ↓
[5] Пропуск (порядок важен!)
    PC: ResetBlockStartAsync()
    PC: ResetFaultIfNoBlockAsync()
    PC: MarkErrorSkipped()
    PC: DequeueError()       ← СНАЧАЛА удаляем из очереди
    PC: ClearFailedState()   ← ПОТОМ открываем gate
    ❌ НЕ отправляет AskRepeat
```

## Gate Mechanism (ColumnExecutor)

Gate контролирует выполнение шагов в колонке:

| Событие | Gate | Эффект |
|---------|------|--------|
| Старт теста | `Set()` | Шаги выполняются |
| Ошибка шага | `Reset()` | Шаги ждут |
| Skip | `Set()` | Следующий шаг запускается |
| Retry успешен | `OpenGate()` | Следующий шаг запускается |
| Retry упал | — | Gate закрыт, новая ошибка в очереди |

```csharp
// ExecuteMapAsync ждёт gate перед каждым шагом
foreach (var step in steps)
{
    await _continueGate.WaitAsync(ct);
    await ExecuteStep(step, ct);
}

// SetErrorState закрывает gate
_continueGate.Reset();

// ClearFailedState (Skip) открывает gate
_continueGate.Set();

// OpenGate() вызывается из координатора после успешного retry
public void OpenGate() => _continueGate.Set();
```

## Retry Serialization (SemaphoreSlim)

Предотвращает конкурентные retry на одной колонке:

```csharp
private readonly SemaphoreSlim _retrySemaphore = new(1, 1);

public async Task RetryLastFailedStepAsync(CancellationToken ct)
{
    var acquired = false;
    try
    {
        await _retrySemaphore.WaitAsync(ct);
        acquired = true;
        // ... retry logic
    }
    finally
    {
        if (acquired) _retrySemaphore.Release();
    }
}
```

## Различия Retry vs Skip

| Параметр | Retry | Skip |
|----------|-------|------|
| **Условие** | `Req_Repeat = true` | `End=true (AND Block.Error)` |
| **AskRepeat** | Да | Нет |
| **Ждёт PLC** | `Block.Error = false` + `Req_Repeat = false` | Сброс сигналов Skip (60 сек) |
| **Шаг выполняется** | Заново | Нет |
| **Gate** | `OpenGate()` после успеха | `Set()` сразу |
| **Статус UI** | OK или NOK | NOK |

## Фоновый Retry (tracked task)

Диалог следующей ошибки появляется сразу (~100мс):

```
[00:00] Col 0 + Col 1: ошибки → в очередь
[00:01] Диалог Col 0
[00:10] Оператор: "Повтор"
[00:11] SendAskRepeatAsync → Block.Error=false
[00:12] InvokeRetryStartedSafely → панель закрывается
[00:13] WaitForRetrySignalResetAsync → Req_Repeat=false
[00:14] DequeueError
[00:15] ExecuteRetryInBackgroundAsync (фоновой, tracked)
[00:16] while → HasPendingErrors = true
[00:17] Диалог Col 1 ← СРАЗУ!
```

## Ключевые методы

### ProcessRetryAsync

```csharp
private async Task ProcessRetryAsync(StepError error, ColumnExecutor executor, CancellationToken ct)
{
    try
    {
        await _errorCoordinator.SendAskRepeatAsync(blockErrorTag, ct);
    }
    catch (TimeoutException)  // Block.Error не сброшен за 60 сек
    {
        await HandleTagTimeoutAsync("Block.Error не сброшен", ct);
        return;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка SendAskRepeatAsync");
        return;  // Диалог покажется снова
    }

    InvokeRetryStartedSafely();

    try
    {
        await _errorCoordinator.WaitForRetrySignalResetAsync(ct);
    }
    catch (TimeoutException)  // Req_Repeat не сброшен за 60 сек
    {
        await HandleTagTimeoutAsync("Req_Repeat не сброшен", ct);
        return;
    }

    StateManager.DequeueError();
    await PublishEventCritical(new ExecutionEvent(
        ExecutionEventKind.RetryRequested,
        StepError: error,
        ColumnExecutor: executor));
}
```

### ExecuteRetryInBackgroundAsync

```csharp
private async Task ExecuteRetryInBackgroundAsync(StepError error, ColumnExecutor executor, CancellationToken ct)
{
    try
    {
        await executor.RetryLastFailedStepAsync(ct);
        await ResetFaultIfNoBlockAsync(error.FailedStep, ct);
        if (!executor.HasFailed)
        {
            executor.OpenGate();
        }
    }
    catch (OperationCanceledException)
    {
        // Защита от зависания колонки при Cancel
        if (!executor.HasFailed)
        {
            executor.OpenGate();
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка Retry в фоне");
    }
}
```

## Ключевые файлы

| Файл | Назначение |
|------|------------|
| `ColumnExecutor.cs` | Gate, SemaphoreSlim, RetryLastFailedStepAsync |
| `TestExecutionCoordinator.ErrorResolution.cs` | ProcessRetryAsync, ExecuteRetryInBackgroundAsync |
| `ErrorCoordinator.Resolution.cs` | WaitForResolutionAsync, SendAskRepeatAsync, WaitForRetrySignalResetAsync |
| `AsyncManualResetEvent.cs` | Gate implementation |

## Edge Cases

### Ошибка SendAskRepeatAsync

При ошибке связи с PLC → return → диалог показывается снова.

### Retry снова упал

SetErrorState() → gate.Reset() → новая ошибка в очередь → новый диалог.

### Fault для non-PLC шагов

`ResetFaultIfNoBlockAsync` сбрасывает `Fault=false` только для шагов без PLC-блока.
При нескольких non-PLC ошибках возможен кратковременный сброс Fault — самовосстанавливается при обработке следующей ошибки.

### Таймаут Block.Error/Req_Repeat (60 сек)

Если PLC не сбросит сигнал за 60 секунд → `HandleTagTimeoutAsync()` → жёсткий стоп теста.
Это защита от залипших сигналов, которые могут вызвать автоматический Retry/Skip для другой колонки.

### Cancel во время фонового Retry

При отмене теста во время фонового Retry: если `executor.HasFailed=false`, открываем gate.
Это предотвращает зависание колонки (gate закрыт, HasFailed=false, нет ошибки в очереди).

### Race Condition при Skip

Порядок важен: `DequeueError()` ПЕРЕД `ClearFailedState()`.
Иначе возможен сценарий: gate открылся → новая ошибка → EnqueueError отклонена (дубликат) → ошибка потеряна.
