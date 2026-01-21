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

## Pause-Aware поведение

| Событие | Поведение |
|---------|-----------|
| `Pause()` | События игнорируются, таймер замораживается |
| `Resume()` | Перепроверка условий, таймер продолжает с остатка |

**Гарантии:** нет утечек подписок, исключения в обработчиках не ломают Pause/Resume, CancellationToken обрабатывается корректно.

**Ограничение:** микро-пауза (<1ms) между проверкой IsPaused и подпиской может быть пропущена.

## Ключевые файлы

| Файл | Назначение |
|------|------------|
| `Services/OpcUa/TagWaiter.cs` | Базовый сервис с pause-aware перегрузками |
| `Services/OpcUa/PausableTagWaiter.cs` | Прокси, передающий PauseTokenSource |
| `Services/Common/PauseTokenSource.cs` | OnPaused/OnResumed события |
| `Services/OpcUa/WaitGroup/*` | Builder, Condition, Result |
