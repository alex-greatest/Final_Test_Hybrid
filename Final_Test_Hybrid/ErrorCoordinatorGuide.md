# ErrorCoordinator Architecture

## Overview

ErrorCoordinator обрабатывает прерывания во время выполнения тестов (потеря соединения с ПЛК, отключение автоматического режима, таймауты тегов). Использует Strategy Pattern для расширяемости.

## Structure

```
Services/Steps/Infrastructure/Execution/ErrorCoordinator/
├── IErrorCoordinator.cs              # Интерфейс координатора
├── ErrorCoordinator.cs               # Основной класс (implements IErrorCoordinator, IInterruptContext)
├── ErrorCoordinator.Interrupts.cs    # Обработка прерываний
├── ErrorCoordinator.Recovery.cs      # Логика сброса и восстановления
├── ErrorCoordinatorDependencies.cs   # Группы зависимостей
└── Behaviors/
    ├── IInterruptBehavior.cs         # Интерфейс стратегии
    ├── IInterruptContext.cs          # Контекст для поведений
    ├── InterruptBehaviorRegistry.cs  # DI-based реестр поведений
    ├── PlcConnectionLostBehavior.cs  # 5 сек задержка → Reset
    ├── AutoModeDisabledBehavior.cs   # Пауза и ожидание
    └── TagTimeoutBehavior.cs         # 5 сек задержка → Reset
```

## Adding New InterruptReason

### Шаг 1: Добавить в enum (ErrorCoordinator.cs)

```csharp
public enum InterruptReason
{
    PlcConnectionLost,
    AutoModeDisabled,
    TagTimeout,
    NewReason  // ← добавить здесь
}
```

### Шаг 2: Создать класс поведения

```csharp
public sealed class NewReasonBehavior : IInterruptBehavior
{
    public InterruptReason Reason => InterruptReason.NewReason;
    public string Message => "Описание прерывания";
    public ErrorDefinition? AssociatedError => null; // или конкретная ошибка

    public Task ExecuteAsync(IInterruptContext context, CancellationToken ct)
    {
        // Показать уведомление оператору
        context.Notifications.ShowWarning(Message, "Детали прерывания");

        // Выбрать действие:
        context.Reset();  // Полный сброс (прерывает выполнение)
        // или
        context.Pause();  // Пауза (можно продолжить после устранения)

        return Task.CompletedTask;
    }
}
```

### Шаг 3: Зарегистрировать в DI (StepsServiceExtensions.cs)

```csharp
services.AddSingleton<IInterruptBehavior, NewReasonBehavior>();
```

> **Важно:** InterruptBehaviorRegistry автоматически найдёт все реализации IInterruptBehavior через DI.

## Dependency Groups

Зависимости разделены на логические группы для улучшения читаемости конструктора:

### ErrorCoordinatorSubscriptions
Сервисы для подписки на события:
- `ConnectionState` — состояние соединения с ПЛК
- `AutoReady` — готовность автоматического режима
- `ActivityTracker` — отслеживание активности тегов

### ErrorResolutionServices
Сервисы для разрешения ошибок:
- `TagWaiter` — ожидание значений тегов
- `PlcService` — взаимодействие с ПЛК
- `ErrorService` — управление ошибками
- `Notifications` — уведомления пользователю

### ErrorCoordinatorState
Состояние координатора:
- `PauseToken` — токен паузы выполнения
- `StateManager` — менеджер состояний
- `StatusReporter` — отчёт о статусе
- `BoilerState` — состояние бойлера

## Events

| Событие | Описание | Подписчики |
|---------|----------|------------|
| `OnReset` | Полный сброс выполнения | TestExecutionCoordinator, ReworkDialogService, PreExecutionCoordinator |
| `OnRecovered` | Восстановление после паузы | — |
| `OnInterruptChanged` | Изменение состояния прерывания | UI компоненты |

### Пример подписки на события

```csharp
// В конструкторе компонента
_errorCoordinator.OnReset += HandleReset;
_errorCoordinator.OnInterruptChanged += HandleInterruptChanged;

// Не забыть отписаться в Dispose
_errorCoordinator.OnReset -= HandleReset;
_errorCoordinator.OnInterruptChanged -= HandleInterruptChanged;
```

## Key Methods

| Метод | Описание |
|-------|----------|
| `HandleInterruptAsync(reason)` | Основная точка входа — делегирует обработку соответствующему behavior |
| `Reset()` | Полный сброс — Resume + вызов OnReset |
| `ForceStop()` | Мягкий сброс — только Resume (без OnReset) |
| `WaitForResolutionAsync()` | Ожидание решения оператора (Retry/Skip/Timeout) |
| `Pause()` | Приостановка выполнения |
| `Resume()` | Возобновление выполнения |

## Behavior Execution Flow

```
HandleInterruptAsync(reason)
    │
    ├── InterruptBehaviorRegistry.GetBehavior(reason)
    │
    ├── behavior.ExecuteAsync(context, ct)
    │   │
    │   ├── context.Notifications.ShowWarning(...)
    │   │
    │   └── context.Reset() или context.Pause()
    │
    └── OnInterruptChanged?.Invoke()
```

## IInterruptContext Interface

Контекст предоставляет поведениям доступ к необходимым операциям:

```csharp
public interface IInterruptContext
{
    INotificationService Notifications { get; }
    void Reset();
    void Pause();
    void Resume();
}
```

## Примеры существующих поведений

### PlcConnectionLostBehavior
- **Задержка:** 5 секунд (даём время на восстановление)
- **Действие:** Reset
- **Ошибка:** PlcConnectionLost

### AutoModeDisabledBehavior
- **Задержка:** нет
- **Действие:** Pause (оператор должен включить авто режим)
- **Ошибка:** AutoModeDisabled

### TagTimeoutBehavior
- **Задержка:** 5 секунд
- **Действие:** Reset
- **Ошибка:** TagTimeout

## Best Practices

1. **Всегда показывать уведомление** — оператор должен знать причину прерывания
2. **Использовать Reset для критических ошибок** — когда продолжение невозможно
3. **Использовать Pause для восстанавливаемых ситуаций** — когда оператор может исправить проблему
4. **Задержка перед Reset** — даёт время на автоматическое восстановление (например, переподключение ПЛК)
5. **AssociatedError** — указывать для интеграции с системой ошибок
