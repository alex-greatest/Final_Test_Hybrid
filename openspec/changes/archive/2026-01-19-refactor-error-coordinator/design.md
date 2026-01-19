# Design: Рефакторинг ErrorCoordinator

## Context
ErrorCoordinator — центральный координатор прерываний в системе тестирования. Обрабатывает:
- Потерю связи с PLC
- Отключение автоматического режима
- Таймауты тегов
- Ожидание решения оператора (Retry/Skip)

Текущая реализация: ~350 строк, 4 файла, over-engineered синхронизация.

## Goals
- Уменьшить объём кода на 30-40%
- Один файл вместо четырёх (кроме dependencies)
- Понятная логика без избыточных абстракций
- Сохранить thread-safety без over-engineering

## Non-Goals
- Изменение behavior-стратегий (PlcConnectionLostBehavior и т.д.)
- Изменение event-контракта (OnReset, OnRecovered, OnInterruptChanged)
- Изменение общей архитектуры Strategy Pattern

## Decisions

### 1. Синхронизация: один SemaphoreSlim вместо трёх примитивов

**Текущее состояние:**
```csharp
private readonly SemaphoreSlim _operationLock = new(1, 1);
private int _isHandlingInterrupt;
private int _activeOperations;
```

**Проблема:** `_isHandlingInterrupt` защищает от параллельной обработки прерываний, `_operationLock` защищает критические секции, `_activeOperations` нужен только для graceful shutdown в `WaitForPendingOperationsAsync`.

**Решение:** Один `SemaphoreSlim(1, 1)` для эксклюзивного доступа к обработке прерываний.

```csharp
private readonly SemaphoreSlim _interruptLock = new(1, 1);
```

**Rationale:**
- `_isHandlingInterrupt` и `_operationLock` выполняют одну функцию — предотвращение параллельной обработки
- `_activeOperations` over-engineered — 5 секунд ожидания в dispose избыточны, достаточно отмены через `_disposeCts`

### 2. Структура файлов: консолидация в один файл

**Текущее состояние:**
```
ErrorCoordinator.cs           (153 строки) - конструктор, события, disposal
ErrorCoordinator.Interrupts.cs (281 строка) - обработка прерываний
ErrorCoordinator.Recovery.cs   (112 строк) - Reset, Resume
```

**Решение:** Объединить в один `ErrorCoordinator.cs` (~200-250 строк после рефакторинга).

**Rationale:**
- Partial classes оправданы при >400 строк на логический блок
- После упрощения синхронизации код сократится
- Один файл проще для навигации и понимания

### 3. WaitForResolution API: Options record вместо перегрузок

**Текущее состояние:**
```csharp
Task<ErrorResolution> WaitForResolutionAsync(CancellationToken ct);
Task<ErrorResolution> WaitForResolutionAsync(string? blockEndTag, string? blockErrorTag, CancellationToken ct, TimeSpan? timeout = null);
Task<ErrorResolution> WaitForResolutionAsync(string? blockEndTag, string? blockErrorTag, bool enableSkip, CancellationToken ct, TimeSpan? timeout = null);
```

**Решение:**
```csharp
public record WaitForResolutionOptions(
    string? BlockEndTag = null,
    string? BlockErrorTag = null,
    bool EnableSkip = true,
    TimeSpan? Timeout = null);

Task<ErrorResolution> WaitForResolutionAsync(
    WaitForResolutionOptions? options = null,
    CancellationToken ct = default);
```

**Rationale:**
- Один метод вместо трёх
- Именованные параметры через record — self-documenting
- Легко расширять (добавить поле в record без breaking change)

### 4. Dependencies: убрать ErrorCoordinatorState

**Текущее состояние:**
```csharp
ErrorCoordinatorState(PauseTokenSource, ExecutionStateManager, StepStatusReporter, BoilerState)
```

**Проблема:** `ExecutionStateManager`, `StepStatusReporter`, `BoilerState` — не используются напрямую в ErrorCoordinator.

**Решение:**
- Оставить только `PauseTokenSource` напрямую в конструкторе
- Удалить `ErrorCoordinatorState` класс

**Rationale:**
- Меньше уровней абстракции = проще понять
- `ErrorCoordinatorSubscriptions` и `ErrorResolutionServices` оставить — они группируют связанные сервисы

## Risks / Trade-offs

| Риск | Вероятность | Митигация |
|------|-------------|-----------|
| Race condition при упрощении синхронизации | Низкая | Один SemaphoreSlim обеспечивает те же гарантии |
| Breaking API WaitForResolution | Средняя | Обновить 3-5 вызовов в кодовой базе |
| Потеря graceful shutdown | Низкая | `_disposeCts.CancelAsync()` достаточен |

## Migration Plan

1. Создать `WaitForResolutionOptions` record
2. Обновить `IErrorCoordinator` интерфейс
3. Рефакторинг `ErrorCoordinator` — объединение файлов, упрощение синхронизации
4. Обновить вызывающий код (`ColumnExecutor`, etc.)
5. Удалить старые файлы (`.Interrupts.cs`, `.Recovery.cs`)
6. Обновить `ErrorCoordinatorGuide.md`

## Open Questions

Нет — scope определён, решения приняты.
