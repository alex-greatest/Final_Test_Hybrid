# TagWaiterGuide.md — Ожидание PLC тегов

## Выбор сервиса

| Сервис | Когда использовать | Примеры |
|--------|-------------------|---------|
| `TagWaiter` | Системные операции (не паузятся) | ErrorCoordinator, PlcResetCoordinator, BlockBoilerAdapterStep |
| `PausableTagWaiter` | Тестовые шаги (паузятся) | ScanStepBase, TestStepContext |

## Архитектура

```
PausableTagWaiter (прокси)
    │
    │  inner.Method(..., pauseToken, ...)
    ▼
TagWaiter (pause-aware)
    │
    │  При паузе: события игнорируются, таймер замораживается
    │  При Resume: перепроверка условий, таймер продолжает
    ▼
OpcUaSubscription
```

## API

```csharp
// Базовые методы
await waiter.WaitForTrueAsync(nodeId, timeout, ct);
await waiter.WaitForFalseAsync(nodeId, timeout, ct);
await waiter.WaitForValueAsync<T>(nodeId, v => v > 10, timeout, ct);
await waiter.WaitForChangeAsync<T>(nodeId, timeout, ct);

// WaitGroup — ожидание нескольких условий
var result = await waiter.WaitAnyAsync(
    waiter.CreateWaitGroup<ErrorResolution>()
        .WaitForTrue(retryTag, () => ErrorResolution.Retry, "Retry")
        .WaitForTrue(skipTag, () => ErrorResolution.Skip, "Skip")
        .WithTimeout(TimeSpan.FromSeconds(60)),
    ct);
// result.Result, result.Name

// AND-логика
builder.WaitForAllTrue([tag1, tag2], () => Result.Both, "Both");
```

## Важно: первичная проверка cache для bool

- В `WaitForValueAsync<T>` первичная проверка выполняется через `subscription.GetValue(nodeId)` (non-generic), а не через `GetValue<T>`.
- Причина: `GetValue<bool>` возвращает `default(false)` при отсутствии значения в cache.
- Для `WaitForFalseAsync` это давало ложное мгновенное завершение после reconnect до первого реального уведомления от PLC.
- Текущая проверка: `if (raw is T current && condition(current))`, поэтому ожидание продолжается, пока не придёт реальное значение нужного типа.

## Pause-Aware поведение

| Событие | Поведение |
|---------|-----------|
| `Pause()` | События игнорируются, таймер замораживается |
| `Resume()` | Перепроверка условий, таймер продолжает с остатка |

**Гарантии:** нет утечек подписок, исключения в обработчиках не ломают Pause/Resume, CancellationToken обрабатывается корректно.

**Ограничение:** микро-пауза (<1ms) между проверкой IsPaused и подпиской может быть пропущена.

## Политика reconnect

- После потери связи используется полный rebuild: создаётся новая `Session`, runtime-подписка пересоздаётся с нуля через `OpcUaSubscription.RecreateForSessionAsync`.
- Runtime monitored items восстанавливаются из runtime-реестра (`_monitoredItems` + callback-слой) с ограниченным retry (`3` попытки, `300 ms`) только для transient OPC ошибок.
- При неуспешном `AddTagAsync` (ошибка `ApplyChangesAsync`) выполняется rollback runtime-состояния (`_monitoredItems`, `_values`, callbacks), чтобы retry подписки не работал по stale-записи.
- Гибрид `SessionReconnectHandler + ручной rebind` не используется, чтобы исключить дубли monitored items.
- UI-индикация на время rebuild: спинер `SubscriptionLoadingOverlay` с текстом `Выполняется подписка...`.
- `TagWaiter` не продолжает прерванный шаг после потери PLC: цикл сбрасывается в исходное состояние; после успешного reconnect новые ожидания снова подписываются на `End/Error`.

## Ключевые файлы

| Файл | Назначение |
|------|------------|
| `Services/OpcUa/TagWaiter.cs` | Базовый сервис с pause-aware перегрузками |
| `Services/OpcUa/PausableTagWaiter.cs` | Прокси, передающий PauseTokenSource |
| `Services/Common/PauseTokenSource.cs` | OnPaused/OnResumed события |
| `Services/OpcUa/WaitGroup/*` | Builder, Condition, Result |
