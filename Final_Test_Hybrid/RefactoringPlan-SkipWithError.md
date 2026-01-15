# План рефакторинга: Проверка End + Block.Error для Skip

## Цель
Skip должен срабатывать только при комбинации `End=true AND Block.Error=true`

## Текущая логика
```csharp
// ErrorCoordinator.Interrupts.cs:195-196
.WaitForTrue(BaseTags.ErrorRetry, () => ErrorResolution.Retry, "Retry")
.WaitForTrue(BaseTags.ErrorSkip, () => ErrorResolution.Skip, "Skip")  // только End
```

## Проблема
Skip срабатывает просто при `End=true`, без проверки что блок в состоянии ошибки.

---

## Изменения

### 1. Расширить TagWaitCondition для multi-tag

**Файл:** `Services/OpcUa/WaitGroup/TagWaitCondition.cs`

```csharp
public record TagWaitCondition
{
    public required string NodeId { get; init; }
    public IReadOnlyList<string>? AdditionalNodeIds { get; init; }  // NEW: для AND-логики
    public required Func<object?, bool> Condition { get; init; }
    public string? Name { get; init; }
}
```

### 2. Добавить WaitForAllTrue в WaitGroupBuilder<T>

**Файл:** `Services/OpcUa/WaitGroup/WaitGroupBuilder{T}.cs`

```csharp
public WaitGroupBuilder<TResult> WaitForAllTrue(
    IReadOnlyList<string> nodeIds,
    Func<TResult> resultFactory,
    string? name = null)
{
    if (nodeIds.Count == 0) throw new ArgumentException("nodeIds cannot be empty");

    _conditions.Add(new TagWaitCondition
    {
        NodeId = nodeIds[0],
        AdditionalNodeIds = nodeIds.Count > 1 ? nodeIds.Skip(1).ToList() : null,
        Condition = _ => true,  // Проверка в TagWaiter
        Name = name
    });
    _resultCallbacks.Add(_ => resultFactory());
    return this;
}
```

### 3. Обновить TagWaiter для AND-логики

**Файл:** `Services/OpcUa/TagWaiter.cs`

В методе `CreateHandler` добавить проверку всех тегов:

```csharp
private Func<object?, Task> CreateHandler<TResult>(...)
{
    return value =>
    {
        // Проверить основной тег
        if (value is not bool boolValue || !boolValue)
            return Task.CompletedTask;

        // Проверить дополнительные теги (AND-логика)
        if (condition.AdditionalNodeIds != null)
        {
            foreach (var additionalNodeId in condition.AdditionalNodeIds)
            {
                var additionalValue = subscription.GetValue<bool>(additionalNodeId);
                if (additionalValue != true)
                    return Task.CompletedTask;  // Не все теги true
            }
        }

        // Все условия выполнены
        tcs.TrySetResult(new TagWaitResult<TResult> { ... });
        return Task.CompletedTask;
    };
}
```

Также в `CheckCurrentValues` добавить аналогичную проверку.

Также подписаться на все `AdditionalNodeIds` в `SubscribeAllAsync`.

### 4. Добавить параметр blockErrorTag в WaitForResolutionAsync

**Файл:** `Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.Interrupts.cs`

```csharp
// Было:
public async Task<ErrorResolution> WaitForResolutionAsync(CancellationToken ct)

// Станет:
public async Task<ErrorResolution> WaitForResolutionAsync(string? blockErrorTag, CancellationToken ct)
```

### 5. Изменить WaitForOperatorSignalAsync

**Файл:** `Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.Interrupts.cs`

```csharp
private async Task<ErrorResolution> WaitForOperatorSignalAsync(string? blockErrorTag, CancellationToken ct)
{
    var builder = _resolution.TagWaiter.CreateWaitGroup<ErrorResolution>()
        .WaitForTrue(BaseTags.ErrorRetry, () => ErrorResolution.Retry, "Retry");

    // Skip с проверкой Error блока
    if (blockErrorTag != null)
    {
        builder.WaitForAllTrue(
            [BaseTags.ErrorSkip, blockErrorTag],
            () => ErrorResolution.Skip,
            "Skip");
    }
    else
    {
        builder.WaitForTrue(BaseTags.ErrorSkip, () => ErrorResolution.Skip, "Skip");
    }

    builder.WithTimeout(ResolutionTimeout);

    var waitResult = await _resolution.TagWaiter.WaitAnyAsync(builder, ct);
    // ...
}
```

### 6. Обновить вызовы WaitForResolutionAsync

**Файл:** `Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorHandling.cs`
- Получить blockErrorTag из StepError (добавить поле BlockErrorTag в StepError)
- Передать в WaitForResolutionAsync

---

## Файлы для изменения

| # | Файл | Изменения |
|---|------|-----------|
| 1 | `Services/OpcUa/WaitGroup/TagWaitCondition.cs` | Добавить `AdditionalNodeIds` |
| 2 | `Services/OpcUa/WaitGroup/WaitGroupBuilder{T}.cs` | Добавить `WaitForAllTrue` метод |
| 3 | `Services/OpcUa/TagWaiter.cs` | Поддержка AND-логики |
| 4 | `Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.Interrupts.cs` | Параметр blockErrorTag |
| 5 | `Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorHandling.cs` | Передать blockErrorTag |
| 6 | `Models/Steps/StepError.cs` | Добавить BlockErrorTag (опционально) |

---

## Верификация
1. Запустить тест с шагом у которого есть PLC блок
2. Спровоцировать ошибку блока (Block.Error=true)
3. Нажать "Один шаг" (End=true) — должен сработать Skip
4. Сбросить ошибку (Block.Error=false), нажать End — Skip НЕ должен сработать

---

## Статус
- [x] Завершён
