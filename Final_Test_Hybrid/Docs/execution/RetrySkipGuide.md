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
    PC:       Переход к ожиданию сброса Req_Repeat
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
| Retry упал (exception) | Fail-safe `OpenGate()` + `HardReset` | Избегаем дедлока колонки, выполнение переводится в reset-flow |

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
// и как fail-safe при аварийном exception в фоне retry
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
| **Ждёт PLC** | `Req_Repeat = false` | Сброс сигналов Skip (60 сек) |
| **Шаг выполняется** | Заново | Нет |
| **Gate** | `OpenGate()` после успеха | `Set()` сразу |
| **Статус UI** | OK или NOK | NOK |

## Фоновый Retry (tracked task)

Диалог следующей ошибки появляется сразу (~100мс):

```
[00:00] Col 0 + Col 1: ошибки → в очередь
[00:01] Диалог Col 0
[00:10] Оператор: "Повтор"
[00:11] SendAskRepeatAsync → AskRepeat=true
[00:12] InvokeRetryStartedSafely → панель закрывается
[00:13] WaitForRetrySignalResetAsync → Req_Repeat=false
[00:14] DequeueError
[00:15] ExecuteRetryInBackgroundAsync (фоновой, tracked)
[00:16] while → HasPendingErrors = true
[00:17] Диалог Col 1 ← СРАЗУ!
```

## Settlement между картами (после Retry/Skip)

`TestExecutionCoordinator` ждёт settlement карты **без таймаута остановки**.

- Условия settlement:
  - `!StateManager.HasPendingErrors`
  - `(_errorDrainTask == null || _errorDrainTask.IsCompleted)`
  - `!_retryState.IsActive`
  - `!HasPendingRetries()`
  - `!executor.HasFailed` для всех колонок
- Если settlement задерживается, координатор пишет диагностический snapshot раз в 2 минуты (без `Stop()`/`Cancel()`).
- Это сохраняет прежнее поведение map-pipeline для штатных сценариев и убирает ложный аварийный выход при долгом ожидании завершения шага (например, ожидание `End/Error`).

## Ключевые методы

### ProcessRetryAsync

```csharp
private async Task ProcessRetryAsync(StepError error, ColumnExecutor executor, CancellationToken ct)
{
    try
    {
        await _errorCoordinator.SendAskRepeatAsync(ct);
    }
    catch (Exception ex)
    {
        await HandleAskRepeatFailureAsync(error, ex, ct);
        return;
    }

    TryPublishEvent(new ExecutionEvent(ExecutionEventKind.RetryStarted));

    try
    {
        await _errorCoordinator.WaitForRetrySignalResetAsync(ct);
    }
    catch (TimeoutException)  // Req_Repeat не сброшен за 60 сек
    {
        await HandleTagTimeoutAsync("Req_Repeat не сброшен", ct);
        return;
    }

    _retryState.MarkStarted();
    try
    {
        await PublishEventCritical(new ExecutionEvent(
            ExecutionEventKind.RetryRequested,
            StepError: error,
            ColumnExecutor: executor));
    }
    catch (Exception ex)
    {
        _retryState.MarkCompleted();
        await HandleRetryPublishFailureAsync(error, ex);
        return;
    }

    StateManager.DequeueError();
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
        await HandleRetryFailureWithHardResetAsync(error, ex);
        executor.OpenGate(); // fail-safe: не оставлять колонку в вечном wait
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

При ошибке `SendAskRepeatAsync` выполняется fail-fast через `HandleAskRepeatFailureAsync`:
- `RequestStopAsFailure(...)`
- `HandleInterruptAsync(PlcConnectionLost|TagTimeout)`
- `CancelAsync()`

### Retry снова упал

Для не-`OperationCanceledException` в `ExecuteRetryInBackgroundAsync`:
- логируется критическая ошибка;
- вызывается `HardReset` (`_errorCoordinator.Reset()`);
- дополнительно выполняется fail-safe `executor.OpenGate()`, чтобы не допустить зависания колонки при любых последующих сбоях.

### Fault для non-PLC шагов

`ResetFaultIfNoBlockAsync` сбрасывает `Fault=false` только для шагов без PLC-блока.
При нескольких non-PLC ошибках возможен кратковременный сброс Fault — самовосстанавливается при обработке следующей ошибки.
Запись `Fault=true/false` выполняется с ограниченным retry (до 3 попыток, пауза 250 мс).
Если все попытки записи Fault неуспешны, выполняется fail-fast в `HardReset` (`_errorCoordinator.Reset()` + отмена текущего прогона).

### Таймаут Req_Repeat (60 сек)

Если PLC не сбросит сигнал за 60 секунд → `HandleTagTimeoutAsync()` → жёсткий стоп теста.
Это защита от залипших сигналов, которые могут вызвать автоматический Retry/Skip для другой колонки.

### `CheckCommsStep` при `AutoReady OFF`

- Шаг `Coms/Check_Comms` является `INonSkippable`, поэтому Skip для него недоступен.
- При `AutoReady = false` шаг завершается fail-fast с `NoDiagnosticConnection` и не уходит в бесконечное ожидание связи.
- После неуспеха шаг останавливает `IModbusDispatcher`, чтобы в фоне не продолжался reconnect-loop.
- Ошибка шага фиксируется в `ColumnExecutor` до `pauseToken.WaitWhilePausedAsync`, поэтому error-flow и попытка записи `Fault=true` стартуют сразу; диалог может появиться позже (после восстановления автомата).
- `Retry` для этого шага имеет смысл только после восстановления автомата (`AutoReady = true`).

### Cancel во время фонового Retry

При отмене теста во время фонового Retry: если `executor.HasFailed=false`, открываем gate.
Это предотвращает зависание колонки (gate закрыт, HasFailed=false, нет ошибки в очереди).

### Race Condition при Skip

Порядок важен: `DequeueError()` ПЕРЕД `ClearFailedState()`.
Иначе возможен сценарий: gate открылся → новая ошибка → EnqueueError отклонена (дубликат) → ошибка потеряна.
