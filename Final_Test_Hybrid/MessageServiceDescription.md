# MessageService: Система управления сообщениями

## Обзор

Система отображает контекстные сообщения пользователю в зависимости от текущего состояния приложения. Использует **приоритетную модель** — сообщение с наивысшим приоритетом всегда отображается поверх остальных.

```
┌─────────────────────────────────────────────────────────────┐
│                    MessageHelper (UI)                       │
│                         ▲                                   │
│                         │ CurrentMessage                    │
│                         │                                   │
│                  ┌──────┴──────┐                            │
│                  │ MessageService │◄── NotifyChanged()      │
│                  └──────────────┘                           │
│                         ▲                                   │
│     ┌───────────────────┼───────────────────┐               │
│     │                   │                   │               │
│  Priority 120      Priority 110        Priority 100         │
│  ┌─────────┐      ┌─────────────┐      ┌─────────────┐      │
│  │Interrupt│      │  Execution  │      │    Scan     │      │
│  │ Message │      │   Message   │      │   Message   │      │
│  │  State  │      │    State    │      │   Manager   │      │
│  └─────────┘      └─────────────┘      └─────────────┘      │
└─────────────────────────────────────────────────────────────┘
```

## Компоненты

### 1. MessageService

**Файл:** `Services/Main/MessageService.cs`

Центральный сервис управления сообщениями. Хранит список провайдеров с приоритетами.

```csharp
public class MessageService
{
    private readonly Dictionary<object, (int priority, Func<string?> provider)> _providers;

    // Возвращает сообщение от провайдера с наивысшим приоритетом
    public string CurrentMessage { get; }

    // Регистрация провайдера, возвращает ключ для отписки
    public object RegisterProvider(int priority, Func<string?> provider);

    // Отписка провайдера
    public void UnregisterProvider(object key);

    // Уведомление об изменении
    public void NotifyChanged();
}
```

**Таблица приоритетов:**

| Приоритет | Провайдер | Сообщения |
|-----------|-----------|-----------|
| 120 | InterruptMessageState | "Потеря связи с PLC", "Нет автомата" |
| 110 | ExecutionMessageState | "Загрузка рецептов...", "Проверка тегов..." |
| 100 | ScanStepManager | "Отсканируйте серийный номер котла" |
| 50 | OperatorState | "Войдите в систему" |
| 40 | AutoReadySubscription | "Ожидание автомата" |

---

### 2. MessageStateBase

**Файл:** `Services/Main/MessageStateBase.cs`

Базовый класс для thread-safe хранения сообщений.

```csharp
public abstract class MessageStateBase
{
    public void SetMessage(string? message);  // Установить сообщение
    public void Clear();                       // Очистить (SetMessage(null))
    public string? GetMessage();               // Получить текущее
    public event Action? OnChange;             // Событие изменения
}
```

**Наследники:**
- `ExecutionMessageState` — сообщения о загрузке (приоритет 110)
- `InterruptMessageState` — сообщения о прерываниях (приоритет 120)

---

### 3. ExecutionActivityTracker

**Файл:** `Services/Common/ExecutionActivityTracker.cs`

Отслеживает активные фазы выполнения. Нужен для определения, когда реагировать на потерю автомата/связи.

```csharp
public class ExecutionActivityTracker
{
    public bool IsPreExecutionActive { get; }   // Идёт PreExecution (сканирование)
    public bool IsTestExecutionActive { get; }  // Идёт TestExecution (тесты)
    public bool IsAnyActive { get; }            // Любая фаза активна

    public void SetPreExecutionActive(bool active);
    public void SetTestExecutionActive(bool active);
}
```

---

### 4. PauseTokenSource

**Файл:** `Services/Common/PauseTokenSource.cs`

Механизм паузы/возобновления для async операций.

```csharp
public class PauseTokenSource
{
    public bool IsPaused { get; }

    public void Pause();   // Перевести в паузу
    public void Resume();  // Возобновить

    // Ожидание возобновления (вызывается в точках проверки)
    public Task WaitWhilePausedAsync(CancellationToken ct);
}
```

---

## Жизненный цикл сообщений

### Сценарий 1: Успешное сканирование и тест

```
Состояние                              Сообщение
────────────────────────────────────────────────────────────
1. Не залогинен                       "Войдите в систему" (50)
2. Залогинен, нет автомата            "Ожидание автомата" (40)
3. Автомат есть                       "Отсканируйте серийный номер" (100)
4. Сканирование штрихкода
   ├─ Проверка штрихкода              "Проверка штрихкода..." (110)
   ├─ Поиск типа котла                "Поиск типа котла..." (110)
   ├─ Загрузка рецептов               "Загрузка рецептов..." (110)
   ├─ Загрузка последовательности     "Загрузка последовательности..." (110)
   └─ Построение карт                 "Построение карт тестов..." (110)
5. Запуск тестов                      Clear → "Отсканируйте..." (100)
6. Тесты выполняются                  (сообщения от тестов)
7. Тесты завершены                    "Отсканируйте..." (100)
```

### Сценарий 2: Потеря автомата во время выполнения

```
Состояние                              Сообщение
────────────────────────────────────────────────────────────
1. Идёт PreExecution                  "Загрузка рецептов..." (110)
2. AutoReady = false
   └─ TestInterruptCoordinator:
      ├─ _interruptMessage.SetMessage  "Нет автомата" (120) ← перекрывает!
      └─ _pauseToken.Pause()           (ожидание)
3. Ожидание восстановления            "Нет автомата" (120)
4. AutoReady = true
   └─ TestInterruptCoordinator:
      ├─ _interruptMessage.Clear()     null (120)
      └─ _pauseToken.Resume()
5. Продолжение                        "Загрузка рецептов..." (110)
```

---

## Потоки данных

### PreExecutionCoordinator

```
ProcessBarcodeAsync(barcode)
         │
         ▼
┌─────────────────────────────────────────────────────────┐
│  activityTracker.SetPreExecutionActive(true)            │
│                        │                                │
│         ┌──────────────┴──────────────┐                 │
│         ▼                             ▼                 │
│  ExecuteAllStepsAsync()        ResolveTestMaps()        │
│         │                             │                 │
│         │ (для каждого шага)          │                 │
│         ▼                             │                 │
│  pauseToken.WaitWhilePausedAsync()    │ ◄── точка паузы│
│         │                             │                 │
│         ▼                             │                 │
│  step.ExecuteAsync()                  │                 │
│    └─ ScanBarcodeStep                 │                 │
│       └─ BarcodePipeline              │                 │
│          └─ messageState.SetMessage() │                 │
│                        │              │                 │
│         ┌──────────────┴──────────────┘                 │
│         ▼                                               │
│  testCoordinator.StartAsync()                           │
│                        │                                │
│  activityTracker.SetPreExecutionActive(false)           │
└─────────────────────────────────────────────────────────┘
```

### TestInterruptCoordinator

```
┌─────────────────────────────────────────────────────────┐
│              СОБЫТИЯ                                    │
│  ┌──────────────┐    ┌──────────────┐                   │
│  │ Connection   │    │  AutoReady   │                   │
│  │   Changed    │    │   Changed    │                   │
│  └──────┬───────┘    └──────┬───────┘                   │
│         │                   │                           │
│         ▼                   ▼                           │
│  ┌────────────────────────────────────┐                 │
│  │  if (!activityTracker.IsAnyActive) │ ◄── проверка    │
│  │      return;                       │                 │
│  └────────────────┬───────────────────┘                 │
│                   │                                     │
│         ┌─────────┴─────────┐                           │
│         ▼                   ▼                           │
│  ┌─────────────┐     ┌─────────────┐                    │
│  │  Interrupt  │     │   Resume    │                    │
│  │  (Pause/    │     │             │                    │
│  │   Reset)    │     │             │                    │
│  └──────┬──────┘     └──────┬──────┘                    │
│         │                   │                           │
│         │    SemaphoreSlim  │  ◄── синхронизация        │
│         │   _interruptLock  │                           │
│         │                   │                           │
│         ▼                   ▼                           │
│  _interruptMessage     _interruptMessage                │
│    .SetMessage()         .Clear()                       │
│  _pauseToken.Pause()   _pauseToken.Resume()             │
└─────────────────────────────────────────────────────────┘
```

---

## BarcodePipeline

**Файл:** `Services/Steps/Steps/BarcodePipeline.cs`

Fluent-паттерн для последовательного выполнения шагов сканирования с автоматическим обновлением сообщений.

```csharp
var result = await new BarcodePipeline(barcode)
    .Step("Проверка штрихкода...", ValidateBarcode)
    .StepAsync("Поиск типа котла...", FindBoilerTypeAsync)
    .StepAsync("Загрузка рецептов...", LoadRecipesAsync)
    .StepAsync("Загрузка последовательности...", LoadTestSequenceAsync)
    .Step("Построение карт тестов...", BuildTestMaps)
    .ExecuteAsync(messageState);
```

**Как работает:**
1. Каждый `.Step()` добавляет шаг в список
2. `ExecuteAsync()` итерирует по шагам:
   - `messageState.SetMessage(step.Message)` — обновляет UI
   - `step.Action(this)` — выполняет логику
   - Если результат != null — прерывает с ошибкой

---

## Регистрация в DI (Form1.cs)

```csharp
// Сервисы
services.AddSingleton<ExecutionActivityTracker>();
services.AddSingleton<ExecutionMessageState>();
services.AddSingleton<InterruptMessageState>();
services.AddSingleton<PauseTokenSource>();
services.AddSingleton<MessageService>();

// Инициализация провайдеров
private static async void StartMessageService(ServiceProvider sp)
{
    var messageService = sp.GetRequiredService<MessageService>();
    var executionState = sp.GetRequiredService<ExecutionMessageState>();
    var interruptState = sp.GetRequiredService<InterruptMessageState>();

    // Регистрация провайдеров
    messageService.RegisterProvider(110, executionState.GetMessage);
    messageService.RegisterProvider(120, interruptState.GetMessage);

    // Подписка на изменения
    executionState.OnChange += messageService.NotifyChanged;
    interruptState.OnChange += messageService.NotifyChanged;
}
```

---

## Синхронизация Interrupt/Resume

**Проблема:** Race condition при быстром переключении AutoReady.

```
T1: AutoReady=false → HandleInterruptAsync() запускается async
T2: AutoReady=true  → TryResumeFromPauseAsync() вызывается
T3: Resume выполняется ДО Pause → система застревает
```

**Решение:** `SemaphoreSlim _interruptLock`

```csharp
// Interrupt
await _interruptLock.WaitAsync(ct);
try {
    await ProcessInterruptAsync(reason, ct);  // включает Pause()
} finally {
    _interruptLock.Release();
}

// Resume
await _interruptLock.WaitAsync();
try {
    if (!_pauseToken.IsPaused) return;
    ResumeExecution();
} finally {
    _interruptLock.Release();
}
```

Теперь Resume всегда ждёт завершения Interrupt.

---

## Очистка ресурсов

### ScanStepManager.Dispose()

```csharp
public void Dispose()
{
    _sessionManager.ReleaseSession();
    _operatorState.OnChange -= HandleStateChange;
    _autoReady.OnChange -= HandleStateChange;
    _coordinator.OnSequenceCompleted -= HandleSequenceCompleted;
    _messageService.UnregisterProvider(_messageProviderKey);  // ← важно!
}
```

### TestInterruptCoordinator.Dispose()

```csharp
public void Dispose()
{
    _connectionState.ConnectionStateChanged -= HandleConnectionChanged;
    _autoReady.OnChange -= HandleAutoReadyChanged;
    _interruptLock.Dispose();
}
```

---

## Ключевые файлы

| Файл | Назначение |
|------|------------|
| `Services/Main/MessageService.cs` | Центральный сервис сообщений |
| `Services/Main/MessageStateBase.cs` | Базовый класс состояния |
| `Services/Main/ExecutionMessageState.cs` | Сообщения загрузки |
| `Services/Main/InterruptMessageState.cs` | Сообщения прерываний |
| `Services/Common/ExecutionActivityTracker.cs` | Трекер активности |
| `Services/Common/PauseTokenSource.cs` | Механизм паузы |
| `Services/Steps/Steps/BarcodePipeline.cs` | Pipeline сканирования |
| `Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.cs` | Координатор PreExecution |
| `Services/Steps/Infrastructure/Execution/TestInterruptCoordinator.cs` | Обработка прерываний |
| `Services/Steps/Infrastructure/Execution/Scanning/ScanStepManager.cs` | Менеджер сканирования |

---

## См. также

- [ARCHITECTURE.md](ARCHITECTURE.md) — архитектура системы выполнения тестов
- [CLAUDE.md](CLAUDE.md) — общие правила и паттерны проекта
  - Секция "UI Dispatching" — маршрутизация вызовов в Blazor context
  - Секция "Test Step Logging" — логирование в шагах тестирования
