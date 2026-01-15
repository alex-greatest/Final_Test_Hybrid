# TagWaiterGuide.md — Ожидание PLC тегов

## Обзор

Два сервиса для ожидания изменений PLC тегов:

| Сервис | Паузится | Контекст использования |
|--------|----------|------------------------|
| `TagWaiter` | Нет | Системные операции |
| `PausableTagWaiter` | Да | Тестовые шаги |

## Архитектура

```
┌─────────────────────────────────────────────────────────────────┐
│                    PausableTagWaiter (обёртка)                  │
│  await pauseToken.WaitWhilePausedAsync() → inner.Method()       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                         TagWaiter                               │
│  OpcUaSubscription → Subscribe/Unsubscribe → TCS                │
└─────────────────────────────────────────────────────────────────┘
```

**PausableTagWaiter** — декоратор над `TagWaiter`, добавляет ожидание `PauseToken` перед каждой операцией.

## Когда какой использовать

### TagWaiter (без пауз)

Используется для **системных операций**, которые не должны останавливаться при паузе теста:

| Сервис | Файл | Назначение |
|--------|------|------------|
| `ErrorCoordinator` | `ErrorCoordinator.Interrupts.cs` | Ожидание решения оператора (Retry/Skip) |
| `PlcResetCoordinator` | `PlcResetCoordinator.cs` | Сброс по сигналу PLC |
| `BlockBoilerAdapterStep` | `BlockBoilerAdapterStep.cs` | PreExecution шаг (до теста) |

```csharp
// ErrorCoordinator — системная операция, не паузится
public ErrorCoordinatorDependencies(
    TagWaiter tagWaiter,  // НЕ PausableTagWaiter
    ...
)
```

### PausableTagWaiter (с паузой)

Используется для **тестовых шагов**, которые должны останавливаться при паузе:

| Контекст | Файл | Назначение |
|----------|------|------------|
| `TestStepContext` | `TestStepContext.cs` | Контекст для всех тестовых шагов |
| `ScanStepBase` | `ScanStepBase.cs` | Базовый класс scan шагов |
| `TestExecutionCoordinator` | `TestExecutionCoordinator.cs` | Координатор выполнения теста |

```csharp
// Тестовый шаг — паузится
public class ScanStepBase(
    PausableOpcUaTagService opcUa,  // Pausable версия
    ...
)
```

## API

### Базовые методы (оба сервиса)

```csharp
// Ждать конкретное значение
await tagWaiter.WaitForTrueAsync(nodeId, timeout, ct);
await tagWaiter.WaitForFalseAsync(nodeId, timeout, ct);

// Ждать значение по условию
await tagWaiter.WaitForValueAsync<T>(nodeId, v => v > 10, timeout, ct);

// Ждать любое изменение
await tagWaiter.WaitForChangeAsync<T>(nodeId, timeout, ct);
```

### WaitGroup — ожидание нескольких условий

```csharp
// Ждать первый из нескольких сигналов
var result = await tagWaiter.WaitAnyAsync(
    tagWaiter.CreateWaitGroup<ErrorResolution>()
        .WaitForTrue(BaseTags.ErrorRetry, () => ErrorResolution.Retry, "Retry")
        .WaitForTrue(BaseTags.ErrorSkip, () => ErrorResolution.Skip, "Skip")
        .WithTimeout(TimeSpan.FromSeconds(60)),
    ct);

// result.Result — возвращённое значение (Retry или Skip)
// result.Name — имя сработавшего условия ("Retry" или "Skip")
```

### WaitGroup с AND-логикой

```csharp
// Ждать когда ОБА тега станут true
builder.WaitForAllTrue(
    [BaseTags.ErrorSkip, blockErrorTag],  // Оба должны быть true
    () => ErrorResolution.Skip,
    "Skip");
```

## Примеры использования

### ErrorCoordinator — ожидание решения оператора

```csharp
// ErrorCoordinator.Interrupts.cs
private async Task<ErrorResolution> WaitForOperatorSignalAsync(string? blockErrorTag, TimeSpan? timeout, CancellationToken ct)
{
    var builder = _resolution.TagWaiter.CreateWaitGroup<ErrorResolution>()
        .WaitForTrue(BaseTags.ErrorRetry, () => ErrorResolution.Retry, "Retry");

    if (blockErrorTag != null)
    {
        builder.WaitForAllTrue(
            [BaseTags.ErrorSkip, blockErrorTag],
            () => ErrorResolution.Skip,
            "Skip");
    }

    if (timeout.HasValue)
    {
        builder.WithTimeout(timeout.Value);
    }

    var waitResult = await _resolution.TagWaiter.WaitAnyAsync(builder, ct);
    return waitResult.Result;
}
```

### BlockBoilerAdapterStep — PreExecution шаг

```csharp
// BlockBoilerAdapterStep.cs
public class BlockBoilerAdapterStep(
    TagWaiter tagWaiter,  // Обычный, не pausable
    ...
)
{
    public async Task<PreExecutionResult> ExecuteAsync(...)
    {
        var waitResult = await tagWaiter.WaitAnyAsync(
            tagWaiter.CreateWaitGroup<BlockResult>()
                .WaitForTrue(endTag, () => BlockResult.End, "End")
                .WaitForTrue(errorTag, () => BlockResult.Error, "Error")
                .WithTimeout(TimeSpan.FromSeconds(30)),
            ct);
    }
}
```

## DI Регистрация

```csharp
// OpcUaServiceExtensions.cs
services.AddSingleton<TagWaiter>();           // Системные операции
services.AddSingleton<PausableTagWaiter>();   // Тестовые шаги
```

## Ключевые файлы

| Файл | Назначение |
|------|------------|
| `Services/OpcUa/TagWaiter.cs` | Базовый сервис |
| `Services/OpcUa/PausableTagWaiter.cs` | Pausable обёртка |
| `Services/OpcUa/WaitGroup/WaitGroupBuilder.cs` | Builder для multi-tag условий |
| `Services/OpcUa/WaitGroup/TagWaitCondition.cs` | Условие ожидания |
| `Services/OpcUa/WaitGroup/TagWaitResult.cs` | Результат ожидания |

## Правило выбора

> **Если операция должна продолжаться при паузе теста** → `TagWaiter`
>
> **Если операция должна останавливаться при паузе** → `PausableTagWaiter`
