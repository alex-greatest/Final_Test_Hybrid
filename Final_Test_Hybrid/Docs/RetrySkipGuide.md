# RetrySkipGuide.md — Логика повтора и пропуска шагов

## PLC Теги

| Тег | Адрес | Направление | Назначение |
|-----|-------|-------------|------------|
| **ErrorRetry** | `DB_Station.Test.Req_Repeat` | PLC → PC | Оператор нажал "Повтор" |
| **ErrorSkip** | `DB_Station.Test.End` | PLC → PC | Оператор нажал "Один шаг" |
| **AskRepeat** | `DB_Station.Test.Ask_Repeat` | PC → PLC | PC готов к повтору |
| **Fault** | `DB_Station.Test.Fault` | PC → PLC | Ошибка шага без блока |
| **Test_End_Step** | `DB_Station.Test.Test_End_Step` | PLC → PC | PLC сигнал завершения (для шагов без блока) |
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
| **С блоком** (IHasPlcBlockPath) | `End=true AND Block.Error=true` |
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
| **Fault сброс** | `Fault = false` (для не-PLC) | — | `Fault = false` |
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
        await ProcessSkipAsync(error, executor, ct);
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

    await ResetFaultIfNoBlockAsync(error.FailedStep);  // Сброс Fault для шагов без блока
    await _errorCoordinator.WaitForRetrySignalResetAsync(ct);
}

// Пропуск
private async Task ProcessSkipAsync(StepError error, ColumnExecutor executor, CancellationToken ct)
{
    await ResetBlockStartAsync(error.FailedStep);
    await ResetFaultIfNoBlockAsync(error.FailedStep);  // Сброс Fault для шагов без блока

    await WaitForSkipSignalsResetAsync(error.FailedStep, ct);

    StateManager.MarkErrorSkipped();
    executor.ClearFailedState();
    StateManager.DequeueError();
}

// Сброс Fault для шагов БЕЗ блока (PLC сбросит Test_End_Step)
private async Task ResetFaultIfNoBlockAsync(ITestStep? step)
{
    if (step is IHasPlcBlockPath)
        return;

    await _plcService.WriteAsync(BaseTags.Fault, false);
}
```

## Ключевые файлы

| Файл | Назначение |
|------|------------|
| `ErrorCoordinator.Interrupts.cs` | WaitForResolutionAsync, SendAskRepeatAsync, WaitForRetrySignalResetAsync |
| `TestExecutionCoordinator.ErrorHandling.cs` | ProcessRetryAsync, ProcessSkipAsync, ResetFaultIfNoBlockAsync |
| `PreExecutionCoordinator.Retry.cs` | RetryStepAsync для PreExecution шагов |
| `BaseTags.cs` | Константы PLC тегов (Fault, Test_End_Step) |
| `PlcBlockTagHelper.cs` | Формирование тегов Block.Selected, Block.Error |

## UI Диалог

**Файл:** `Components/Errors/ErrorHandlingDialog.razor`

| Кнопка | PLC сигнал | Действие |
|--------|------------|----------|
| "Повтор" | `Req_Repeat = true` | Retry шага |
| "Один шаг" | `End = true` | Skip шага (NOK) |
| "СТОП" | — | Остановка теста |

### Закрытие панели ошибки (FloatingErrorPanel)

Панель ошибки закрывается **после подтверждения PLC**, а не сразу при нажатии кнопки:

```
1. Оператор нажимает "Повтор"
2. PLC → PC: Req_Repeat = true
3. PC → PLC: AskRepeat = true
4. PLC: Сбрасывает Block.Error = false
5. PC ждёт: Block.Error = false  ← подтверждение получено
6. UI: Панель закрывается       ← OnRetryStarted событие
7. PC: Выполняет retry шага
```

**Реализация:**
- `TestExecutionCoordinator.OnRetryStarted` — событие после `SendAskRepeatAsync`
- `BoilerInfo.razor` подписан на событие → вызывает `CloseErrorPanel()`

**Почему не сразу при нажатии:**
- Если запись `AskRepeat` в PLC не удалась — панель остаётся открытой
- Пользователь видит что система ждёт подтверждения от PLC

**Почему не после завершения retry:**
- Retry может выполняться долго
- Пользователь должен видеть прогресс в гриде, а не заблокированную панель

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

### Retry: PLC vs не-PLC шаги

При Retry поведение зависит от типа шага:

| Этап | PLC шаг (IHasPlcBlockPath) | Не-PLC шаг |
|------|------------------------|------------|
| `GetBlockErrorTag()` | `DB_VI.Block_X.Error` | `null` |
| `WaitForPlcAcknowledgeAsync` | **Ждёт** Block.Error=false | Пропускает (return) |
| `RetryLastFailedStepAsync` | После сброса Block.Error | Сразу |
| `ResetFaultIfNoBlockAsync` | Пропускает (return) | **Сбрасывает** Fault=false |
| `WaitForRetrySignalResetAsync` | Ждёт Req_Repeat=false | Ждёт Req_Repeat=false |

**Для PLC шагов — двойная защита:**
1. **Block.Error=false** — PLC готов к новому запуску блока
2. **Req_Repeat=false** — следующая ошибка не получит сразу тот же сигнал

**Для не-PLC шагов — тройная защита:**
1. **Fault=false** → PLC сбрасывает Test_End_Step=false
2. **Test_End_Step=false** — следующая ошибка не получит сразу Skip
3. **Req_Repeat=false** — следующая ошибка не получит сразу Retry

### Сброс Fault для шагов без блока

Для шагов **без PLC блока** используется тег `Fault` вместо `Block.Error`:

```
Проблема (до исправления):
1. PC ставит Fault = true при ошибке шага без блока
2. Оператор нажимает Skip → PLC: Test_End_Step = true
3. PC НЕ сбрасывал Fault = false
4. PLC ждёт сброс Fault чтобы сбросить Test_End_Step
5. На следующей ошибке CheckCurrentValues видит Test_End_Step = true → сразу Skip!

Решение:
При Skip и Retry для шагов без блока PC сбрасывает Fault = false
→ PLC автоматически сбрасывает Test_End_Step = false
```

| Этап | PLC шаг (IHasPlcBlockPath) | Не-PLC шаг |
|------|------------------------|------------|
| Ошибка | PC: `Block.Selected = true` | PC: `Fault = true` |
| Skip/Retry | — | PC: `Fault = false` |
| PLC реакция | Сам сбрасывает `Block.Error` | Сбрасывает `Test_End_Step = false` |
| Ожидание сброса | `Block.End = false` | `Test_End_Step = false` |
