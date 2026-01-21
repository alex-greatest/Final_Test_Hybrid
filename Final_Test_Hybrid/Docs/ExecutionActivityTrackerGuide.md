# ExecutionActivityTrackerGuide.md — Отслеживание фаз выполнения

> Подробнее о системе выполнения: [CLAUDE.md → Step Execution System](CLAUDE.md#step-execution-system--полный-flow)

## Обзор

`ExecutionActivityTracker` — singleton-сервис для отслеживания активных фаз выполнения с потокобезопасной реализацией.

| Свойство | Описание |
|----------|----------|
| `IsPreExecutionActive` | Фаза подготовки (ScanStep, BlockBoilerAdapter) |
| `IsTestExecutionActive` | Фаза выполнения (4 executor'а параллельно) |
| `IsAnyActive` | Любая фаза активна (OR логика) |

**Основной потребитель:** `ErrorCoordinator` использует `IsAnyActive` как guard clause для interrupt'ов.

## Архитектура

```
┌────────────────────────────────────────────────────────────────┐
│ PreExecutionCoordinator.RunSingleCycleAsync()                  │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│  SetPreExecutionActive(true)  ← Начало фазы подготовки        │
│  │                                                            │
│  ├─ WaitForBarcodeAsync()    ← Ожидание сканирования          │
│  │                                                            │
│  ├─ ExecutePreExecutionPipelineAsync()                         │
│  │  ├─ ScanStep            ← 10 шагов подготовки             │
│  │  └─ BlockBoilerAdapterStep  ← Блокировка адаптера PLC     │
│  │                                                            │
│  └─ StartTestExecution()                                       │
│     │                                                         │
│     └─ TestExecutionCoordinator.StartAsync()                   │
│        │                                                      │
│        ├─ BeginExecution()                                    │
│        │  └─ SetTestExecutionActive(true)  ← Начало теста    │
│        │                                                      │
│        ├─ RunAllMaps()  ← 4 executor'а параллельно           │
│        │                                                      │
│        └─ Complete()                                          │
│           └─ SetTestExecutionActive(false)  ← Конец теста    │
│                                                              │
│  SetPreExecutionActive(false)  ← Конец фазы подготовки       │
│                                                              │
└────────────────────────────────────────────────────────────────┘
```

## Жизненный цикл

| Событие | Метод | Файл |
|---------|-------|------|
| Начало подготовки | `SetPreExecutionActive(true)` | `PreExecutionCoordinator.MainLoop.cs:44` |
| Начало теста | `SetTestExecutionActive(true)` | `TestExecutionCoordinator.Execution.cs:83` |
| Конец теста | `SetTestExecutionActive(false)` | `TestExecutionCoordinator.Execution.cs:134` |
| Конец подготовки | `SetPreExecutionActive(false)` | `PreExecutionCoordinator.MainLoop.cs:55` (finally) |

**Важно:**
- `IsTestExecutionActive` может быть `true` пока `IsPreExecutionActive` всё ещё `true` (асинхронный fire-and-forget)
- `SetPreExecutionActive(false)` вызывается в `finally` блоке — гарантирует очистку

## Использование в ErrorCoordinator

`ErrorCoordinator` использует `IsAnyActive` как **guard clause** — interrupt'ы генерируются только во время активного выполнения.

### Потеря соединения с PLC

```csharp
private void HandleConnectionChanged(bool isConnected)
{
    var isActive = _subscriptions.ActivityTracker.IsAnyActive;
    if (_disposed || isConnected || !isActive) { return; }  // Guard
    FireAndForgetInterrupt(InterruptReason.PlcConnectionLost);
}
```

### Отключение автоматического режима

```csharp
private void HandleAutoReadyChanged()
{
    var isReady = _subscriptions.AutoReady.IsReady;
    var isActive = _subscriptions.ActivityTracker.IsAnyActive;

    if (isReady)
    {
        FireAndForgetResume();
        return;
    }

    if (isActive)  // Guard — только если тест активен
    {
        FireAndForgetInterrupt(InterruptReason.AutoModeDisabled);
    }
}
```

### Сценарии

| Сценарий | IsAnyActive | Результат |
|----------|-------------|-----------|
| Потеря соединения **во время теста** | `true` | Interrupt генерируется |
| Потеря соединения **в режиме ожидания** | `false` | Ничего не происходит |
| Отключение Auto **во время теста** | `true` | Interrupt генерируется |

## Потокобезопасность

```csharp
public sealed class ExecutionActivityTracker
{
    private readonly Lock _lock = new();
    private bool _isPreExecutionActive;
    private bool _isTestExecutionActive;

    public event Action? OnChanged;

    public bool IsAnyActive
    {
        get { lock (_lock) { return _isPreExecutionActive || _isTestExecutionActive; } }
    }

    public void SetTestExecutionActive(bool active)
    {
        lock (_lock)
        {
            if (_isTestExecutionActive == active) { return; }  // Защита от дублей
            _isTestExecutionActive = active;
        }
        OnChanged?.Invoke();  // Event вызывается ВНЕ lock'а
    }
}
```

**Почему event вне lock'а:**
- Предотвращение deadlock'ов — подписчики могут захватывать свои lock'и
- Паттерн "capture-check-notify" — проверка в lock'е, уведомление вне

## API

### Свойства

| Свойство | Тип | Описание |
|----------|-----|----------|
| `IsPreExecutionActive` | `bool` | Фаза подготовки активна |
| `IsTestExecutionActive` | `bool` | Фаза выполнения активна |
| `IsAnyActive` | `bool` | `IsPreExecutionActive \|\| IsTestExecutionActive` |

### Методы

| Метод | Описание |
|-------|----------|
| `SetPreExecutionActive(bool)` | Установить состояние фазы подготовки |
| `SetTestExecutionActive(bool)` | Установить состояние фазы выполнения |
| `Clear()` | Очистить оба флага |

### Событие

| Событие | Когда |
|---------|-------|
| `OnChanged` | При любом изменении состояния |

## DI Регистрация

```csharp
// StepsServiceExtensions.cs
services.AddSingleton<ExecutionActivityTracker>();
```

### Граф зависимостей

```
ExecutionActivityTracker (Singleton)
├─ PreExecutionState → PreExecutionCoordinator
├─ ErrorCoordinatorSubscriptions → ErrorCoordinator
└─ TestExecutionCoordinator (прямое внедрение)
```

## Ключевые файлы

| Файл | Содержимое |
|------|------------|
| `Services/Common/ExecutionActivityTracker.cs` | Основной класс |
| `Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.MainLoop.cs` | `SetPreExecutionActive` вызовы |
| `Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionDependencies.cs` | `PreExecutionState` с `ActivityTracker` |
| `Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.cs` | Прямое внедрение |
| `Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.Execution.cs` | `SetTestExecutionActive` вызовы |
| `Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.cs` | `IsAnyActive` потребитель |
| `Services/DependencyInjection/StepsServiceExtensions.cs` | DI регистрация |
