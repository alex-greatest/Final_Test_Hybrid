# RetrySkipGuide.md — Логика повтора и пропуска шагов

## PLC Теги

| Тег | Адрес | Направление | Назначение |
|-----|-------|-------------|------------|
| **ErrorRetry** | `DB_Station.Test.Req_Repeat` | PLC → PC | Оператор нажал "Повтор" |
| **ErrorSkip** | `DB_Station.Test.End` | PLC → PC | Оператор нажал "Один шаг" |
| **AskRepeat** | `DB_Station.Test.Ask_Repeat` | PC → PLC | PC готов к повтору |
| **Block.Selected** | `DB_VI.Block_X.Selected` | PC → PLC | Какой блок в ошибке |
| **Block.Error** | `DB_VI.Block_X.Error` | PLC → PC | Флаг ошибки блока |

## Retry Flow (Повтор)

```
┌─────────────────────────────────────────────────────────────────┐
│  ЭТАП 1: Ошибка                                                 │
├─────────────────────────────────────────────────────────────────┤
│  PLC: Block.Error = true                                        │
│  PC:  Обнаружил ошибку                                          │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│  ЭТАП 2: Подготовка к диалогу                                   │
├─────────────────────────────────────────────────────────────────┤
│  PC → PLC: Block.Selected = true   (указывает какой блок)       │
│  PC:       Показывает диалог ErrorHandlingDialog                │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│  ЭТАП 3: Ожидание решения оператора                             │
├─────────────────────────────────────────────────────────────────┤
│  PC ждёт: Req_Repeat=true ИЛИ End=true                          │
│  Оператор нажимает "Повтор"                                     │
│  PLC → PC: Req_Repeat = true                                    │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│  ЭТАП 4: Сигнал готовности к повтору                            │
├─────────────────────────────────────────────────────────────────┤
│  PC → PLC: AskRepeat = true   ("готов повторять")               │
│  PLC:      Сбрасывает Block.Error = false                       │
│  PC ждёт:  Block.Error = false (подтверждение)                  │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│  ЭТАП 5: Повтор шага                                            │
├─────────────────────────────────────────────────────────────────┤
│  PC → PLC: Block.Start = true                                   │
│  PLC:      Выполняет блок заново                                │
│  Результат: Block.End=true (успех) или Block.Error=true (снова) │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│  ЭТАП 5.5: Ожидание сброса Req_Repeat                           │
├─────────────────────────────────────────────────────────────────┤
│  PC ждёт: Req_Repeat = false (макс 5 сек)                       │
│  Нужно чтобы следующая ошибка не получила сразу тот же сигнал   │
└─────────────────────────────────────────────────────────────────┘
```

**Шаги:**
1. PLC: `Block.Error = true`
2. PC → PLC: `Block.Selected = true`
3. PC: Показывает диалог
4. Оператор нажимает "Повтор"
5. PLC → PC: `Req_Repeat = true`
6. PC → PLC: `AskRepeat = true`
7. PLC: Сбрасывает `Block.Error = false`
8. PC: Повторяет шаг (`Block.Start = true`)
9. PC ждёт: `Req_Repeat = false` (макс 5 сек)

## Skip Flow (Пропуск)

### Условия срабатывания Skip

| Тип шага | Условие Skip |
|----------|--------------|
| **С блоком** (IHasPlcBlock) | `End=true AND Block.Error=true` |
| **Без блока** | `End=true` |

> **Почему AND-логика для шагов с блоком?**
> Предотвращает случайный Skip когда оператор нажимает "Один шаг" без реальной ошибки блока.

```
┌─────────────────────────────────────────────────────────────────┐
│  ЭТАП 1-2: Ошибка + Диалог (как в Retry)                        │
├─────────────────────────────────────────────────────────────────┤
│  PLC: Block.Error = true                                        │
│  PC:  Block.Selected = true, показывает диалог                  │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│  ЭТАП 3: Оператор выбирает пропуск                              │
├─────────────────────────────────────────────────────────────────┤
│  Оператор нажимает "Один шаг"                                   │
│  PLC → PC: End = true                                           │
│  PC проверяет: Block.Error = true? (для шагов с блоком)         │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│  ЭТАП 4: Пропуск                                                │
├─────────────────────────────────────────────────────────────────┤
│  PC: ClearFailedState()                                         │
│  PC: Переходит к следующему шагу                                │
│  ❌ НЕ отправляет AskRepeat                                     │
│  ❌ НЕ ждёт подтверждения от PLC                                │
│  Block.Error остаётся = true в PLC                              │
└─────────────────────────────────────────────────────────────────┘
```

**Шаги:**
1. PLC: `Block.Error = true`
2. PC → PLC: `Block.Selected = true`
3. PC: Показывает диалог
4. Оператор нажимает "Один шаг"
5. PLC → PC: `End = true`
6. PC: `ClearFailedState()`, переход к следующему шагу
7. НЕ отправляет AskRepeat, Block.Error остаётся true

## Различия Retry vs Skip

| Параметр | Retry | Skip (с блоком) | Skip (без блока) |
|----------|-------|-----------------|------------------|
| **Условие** | `Req_Repeat = true` | `End=true AND Block.Error=true` | `End = true` |
| **PC → PLC** | `AskRepeat = true` | Ничего | Ничего |
| **Ждёт подтверждения** | `Block.Error = false` | Нет | Нет |
| **Шаг выполняется** | Заново | Нет | Нет |
| **Block.Error** | Сбрасывается PLC | Остаётся true | — |
| **Статус шага в UI** | Может стать OK | NOK (Ошибка) | NOK (Ошибка) |

## Ключевые методы

### ErrorCoordinator.Interrupts.cs

```csharp
// Ожидание решения оператора (с blockErrorTag для AND-логики Skip)
public Task<ErrorResolution> WaitForResolutionAsync(CancellationToken ct)
    => WaitForResolutionAsync(null, ct);

public async Task<ErrorResolution> WaitForResolutionAsync(string? blockErrorTag, CancellationToken ct)
{
    return await WaitForOperatorSignalAsync(blockErrorTag, ct);
}

// Ожидание сигналов от PLC
private async Task<ErrorResolution> WaitForOperatorSignalAsync(string? blockErrorTag, CancellationToken ct)
{
    var builder = _resolution.TagWaiter.CreateWaitGroup<ErrorResolution>()
        .WaitForTrue(BaseTags.ErrorRetry, () => ErrorResolution.Retry, "Retry");

    // Skip: AND-логика для шагов с блоком
    if (blockErrorTag != null)
        builder.WaitForAllTrue([BaseTags.ErrorSkip, blockErrorTag], () => ErrorResolution.Skip, "Skip");
    else
        builder.WaitForTrue(BaseTags.ErrorSkip, () => ErrorResolution.Skip, "Skip");

    builder.WithTimeout(ResolutionTimeout);
    return (await _resolution.TagWaiter.WaitAnyAsync(builder, ct)).Result;
}

// Отправка сигнала готовности к повтору
public async Task SendAskRepeatAsync(string? blockErrorTag, CancellationToken ct)
{
    await _resolution.PlcService.WriteAsync(BaseTags.AskRepeat, true, ct);
    await WaitForPlcAcknowledgeAsync(blockErrorTag, ct);  // Ждёт Block.Error = false
}

// Ожидание сброса Req_Repeat (защита от race condition)
public async Task WaitForRetrySignalResetAsync(CancellationToken ct)
{
    // Ждёт Req_Repeat = false (таймаут 5 сек)
    // Нужно чтобы следующая ошибка не получила сразу тот же сигнал
    var currentValue = _resolution.Subscription.GetValue<bool>(BaseTags.ErrorRetry);
    if (currentValue != true) return;  // Уже сброшен

    await _resolution.TagWaiter.WaitForFalseAsync(BaseTags.ErrorRetry,
        timeout: TimeSpan.FromSeconds(5), ct);
}
```

### TestExecutionCoordinator.ErrorHandling.cs

```csharp
// Обработка решения
private async Task ProcessErrorResolution(StepError error, ErrorResolution resolution, CancellationToken ct)
{
    if (resolution == ErrorResolution.Retry)
    {
        await ProcessRetryAsync(error, executor, ct);
    }
    else
    {
        ProcessSkip(executor);
    }
}

// Повтор
private async Task ProcessRetryAsync(StepError error, ColumnExecutor executor, CancellationToken ct)
{
    var blockErrorTag = GetBlockErrorTag(error.FailedStep);
    await _errorCoordinator.SendAskRepeatAsync(blockErrorTag, ct);
    await executor.RetryLastFailedStepAsync(ct);
    if (!executor.HasFailed)
    {
        StateManager.DequeueError();
    }
    await _errorCoordinator.WaitForRetrySignalResetAsync(ct);
}

// Пропуск
private void ProcessSkip(ColumnExecutor executor)
{
    executor.ClearFailedState();
    StateManager.DequeueError();
}
```

## Ключевые файлы

| Файл | Назначение |
|------|------------|
| `ErrorCoordinator.Interrupts.cs` | WaitForResolutionAsync, SendAskRepeatAsync, WaitForRetrySignalResetAsync |
| `TestExecutionCoordinator.ErrorHandling.cs` | ProcessRetryAsync, ProcessSkip |
| `PreExecutionCoordinator.Retry.cs` | RetryStepAsync для PreExecution шагов |
| `BaseTags.cs` | Константы PLC тегов |
| `PlcBlockTagHelper.cs` | Формирование тегов Block.Selected, Block.Error |

## UI Диалог

**Файл:** `Components/Errors/ErrorHandlingDialog.razor`

| Кнопка | PLC сигнал | Действие |
|--------|------------|----------|
| "Повтор" | `Req_Repeat = true` | Retry шага |
| "Один шаг" | `End = true` | Skip шага (NOK) |
| "СТОП" | — | Остановка теста |

## Особенности

### canSkip для PreExecution шагов

Некоторые PreExecution шаги (например `BlockBoilerAdapterStep`) имеют `canSkip: false`:

```csharp
return PreExecutionResult.FailRetryable(
    error,
    canSkip: false,  // Skip отключен для этого шага
    userMessage: error,
    errors: []);
```

При `canSkip: false` даже если оператор нажмёт "Один шаг", пропуск не сработает.

### Race condition при нескольких ошибках

При нескольких ошибках в очереди возможен race condition с сигналом `Req_Repeat`:

```
Проблема:
1. Ошибка 1 (col 2) + Ошибка 2 (col 3) в очереди
2. Оператор нажимает Retry → Req_Repeat = true
3. PC обрабатывает ошибку 1, отправляет AskRepeat
4. PLC начинает сбрасывать Req_Repeat...
5. PC обрабатывает ошибку 2 → CheckCurrentValues() видит Req_Repeat ВСЁ ЕЩЁ true!
6. Ошибка 2 сразу получает Retry без нажатия оператором

Решение:
После retry PC ждёт Req_Repeat = false (WaitForRetrySignalResetAsync)
чтобы следующая ошибка не получила сразу тот же сигнал.
```
