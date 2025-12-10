/c# OPC UA Services - Документация и рефакторинг

## Обзор модуля

Модуль `Services/OpcUa` предоставляет инфраструктуру для подключения к OPC UA серверам промышленного оборудования. OPC UA (Open Platform Communications Unified Architecture) — стандартный протокол для обмена данными в промышленной автоматизации.

---

## Структура файлов

```
Services/OpcUa/
├── IOpcUaConnectionService.cs              # Интерфейс сервиса подключения (24 строки)
├── OpcUaConnectionService.cs               # Core: lifecycle, connect loop (~250 строк)
├── OpcUaConnectionService.Reconnect.cs     # Partial: reconnect логика (~210 строк)
│
├── IOpcUaSubscriptionService.cs            # Интерфейс сервиса подписок (25 строк)
├── OpcUaSubscriptionService.cs             # Core: поля, Subscribe, Dispose (~220 строк)
├── OpcUaSubscriptionService.Callbacks.cs   # Partial: обработка данных (~65 строк)
├── OpcUaSubscriptionService.Recreate.cs    # Partial: пересоздание подписок (~95 строк)
├── OpcUaSubscriptionSettings.cs            # POCO настроек подписок (9 строк)
│
├── IOpcUaSessionFactory.cs                 # Интерфейс фабрики сессий (13 строк)
├── OpcUaSessionFactory.cs                  # Создание сессий + конфигурация (87 строк)
│
├── OpcUaSettings.cs                        # POCO настроек (16 строк)
├── OpcUaSettingsValidator.cs               # Валидация настроек (64 строки)
│
├── refactoring.md                          # Эта документация
└── plan_subscribe.md                       # План сервиса подписок
```

**Все файлы < 300 строк** (лимит из `.cursorrules`).

---

## Описание классов

### OpcUaSubscriptionSettings

**Назначение:** POCO-класс для конфигурации подписок. Загружается из `appsettings.json` через `IOptions<OpcUaSubscriptionSettings>`.

| Свойство | Тип | По умолчанию | Описание |
|----------|-----|--------------|----------|
| `PublishingIntervalMs` | int | 500 | Интервал публикации данных (мс) |
| `SamplingIntervalMs` | int | 250 | Интервал опроса значений (мс) |
| `QueueSize` | uint | 10 | Размер очереди изменений |

---

### OpcUaSettings

**Назначение:** POCO-класс для конфигурации OPC UA клиента. Загружается из `appsettings.json` через `IOptions<OpcUaSettings>`.

| Свойство | Тип | По умолчанию | Описание |
|----------|-----|--------------|----------|
| `EndpointUrl` | string | `opc.tcp://localhost:4840` | Адрес OPC UA сервера |
| `ApplicationName` | string | `OpcUaClient` | Имя приложения для идентификации на сервере |
| `ReconnectIntervalMs` | int | 5000 | Интервал попыток переподключения (мс) |
| `SessionTimeoutMs` | int | 60000 | Таймаут сессии (мс) |
| `MaxStringLength` | int | 1048576 | Макс. длина строки (1 МБ) |
| `MaxByteStringLength` | int | 1048576 | Макс. длина бинарных данных (1 МБ) |
| `MaxArrayLength` | int | 65535 | Макс. количество элементов массива |
| `MaxMessageSize` | int | 4194304 | Макс. размер сообщения (4 МБ) |
| `MaxBufferSize` | int | 65535 | Размер буфера (64 КБ) |
| `ChannelLifetimeMs` | int | 300000 | Время жизни канала (5 мин) |
| `SecurityTokenLifetimeMs` | int | 3600000 | Время жизни токена безопасности (1 час) |

**Пример конфигурации (appsettings.json):**
```json
{
  "OpcUa": {
    "EndpointUrl": "opc.tcp://192.168.1.100:4840",
    "ApplicationName": "TestHybridClient",
    "ReconnectIntervalMs": 5000,
    "SessionTimeoutMs": 60000,
    "Subscription": {
      "PublishingIntervalMs": 500,
      "SamplingIntervalMs": 250,
      "QueueSize": 10
    }
  }
}
```

---

### OpcUaSettingsValidator

**Назначение:** Статический класс для валидации настроек OPC UA.

```csharp
public static class OpcUaSettingsValidator
{
    public static void Validate(OpcUaSettings settings, ILogger? logger = null);
}
```

**Проверки:**
- `EndpointUrl` не пустой
- `EndpointUrl` начинается с `opc.tcp://`
- Предупреждения для слишком малых интервалов

---

### IOpcUaSessionFactory / OpcUaSessionFactory

**Назначение:** Фабрика для создания настроенных OPC UA сессий. Инкапсулирует создание `ApplicationConfiguration` и `ConfiguredEndpoint`.

```csharp
public interface IOpcUaSessionFactory
{
    Task<ISession> CreateAsync(OpcUaSettings settings, CancellationToken cancellationToken = default);
}
```

**Ответственность:**
- Создание `ApplicationConfiguration` с настройками безопасности и транспорта
- Создание `ConfiguredEndpoint` с таймаутами
- Делегирование создания сессии в `ISessionFactory` (OPC UA SDK)

---

### IOpcUaConnectionService

**Назначение:** Контракт сервиса для управления подключением к OPC UA серверу.

```csharp
public interface IOpcUaConnectionService : IAsyncDisposable
{
    bool IsConnected { get; }
    event Action<bool>? ConnectionChanged;
    event Action? SessionRecreated;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();

    Task ExecuteWithSessionAsync(Func<ISession, Task> action, CancellationToken ct = default);
    Task<T> ExecuteWithSessionAsync<T>(Func<ISession, Task<T>> action, CancellationToken ct = default);
}
```

---

### IOpcUaSubscriptionService / OpcUaSubscriptionService

**Назначение:** Thread-safe сервис подписок на изменения значений OPC UA nodes.

```csharp
public interface IOpcUaSubscriptionService : IAsyncDisposable
{
    bool IsInitialized { get; }
    IDisposable Subscribe(string nodeId, Action<DataValue> onValueChanged);
    IDisposable Subscribe(IEnumerable<string> nodeIds, Action<string, DataValue> onValueChanged);
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
```

**Особенности:**
- **Отложенная инициализация:** `Subscribe()` только добавляет callback в словарь, `InitializeAsync()` создаёт OPC подписки
- **Batch initialization:** Один round-trip для всех тегов (эффективно для 500+ тегов)
- Callback вызывается в потоке OPC UA SDK (не UI thread)
- Возвращает `IDisposable` для отписки
- Автоматически пересоздаёт подписки при `SessionRecreated`
- Thread-safe через `SemaphoreSlim` + `ReaderWriterLockSlim`

**Разбит на три файла:**
- `OpcUaSubscriptionService.cs` — core: поля, Subscribe, Dispose
- `OpcUaSubscriptionService.Callbacks.cs` — обработка OnDataChange
- `OpcUaSubscriptionService.Recreate.cs` — пересоздание при reconnect

---

### OpcUaConnectionService (partial class)

**Назначение:** Реализация сервиса подключения с автоматическим переподключением.

Разбит на два файла:
- `OpcUaConnectionService.cs` — lifecycle, connect loop, public API
- `OpcUaConnectionService.Reconnect.cs` — reconnect логика, события

**Почему partial, а не композиция:**
Reconnect логика тесно связана с внутренним состоянием (`_session`, `_sessionLock`, `_isReconnecting`). Композиция потребовала бы либо выставить состояние публично, либо передавать много callback'ов. Partial class — компромисс между разделением кода и инкапсуляцией.

**Жизненный цикл:**
```
StartAsync() → ConnectLoop → [Connected]
                    ↓              ↓
              [Disconnected] ← KeepAlive Error
                    ↓
            SessionReconnectHandler
                    ↓
              [Reconnected] или [SessionRecreated]
```

---

## Граф зависимостей

```
┌─────────────────────────────────────────────────────────┐
│                      DI Container                        │
└─────────────────────────────────────────────────────────┘
                            │
         ┌──────────────────┼──────────────────┐
         ▼                  ▼                  ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ OpcUaSettings   │ │ ILoggerFactory  │ │ ISessionFactory │
│ (IOptions)      │ │                 │ │ (OPC UA SDK)    │
└─────────────────┘ └─────────────────┘ └─────────────────┘
         │                  │                  │
         └──────────────────┼──────────────────┘
                            ▼
                ┌───────────────────────┐
                │  IOpcUaSessionFactory │
                │  (OpcUaSessionFactory)│
                └───────────────────────┘
                            │
                            ▼
              ┌───────────────────────────┐
              │  IOpcUaConnectionService  │
              │ (OpcUaConnectionService)  │
              │    └── .Reconnect.cs      │
              └───────────────────────────┘
                            │
         ┌──────────────────┴──────────────────┐
         ▼                                     ▼
┌─────────────────────┐          ┌─────────────────────────────┐
│ OpcUaSubscription   │          │  IOpcUaSubscriptionService  │
│ Settings (IOptions) │──────────│ (OpcUaSubscriptionService)  │
└─────────────────────┘          │    ├── .Callbacks.cs        │
                                 │    └── .Recreate.cs         │
                                 └─────────────────────────────┘
```

---

## Регистрация в DI (Form1.cs)

```csharp
services.Configure<OpcUaSettings>(configuration.GetSection("OpcUa"));
services.Configure<OpcUaSubscriptionSettings>(configuration.GetSection("OpcUa:Subscription"));
services.AddSingleton<ISessionFactory>(sp => DefaultSessionFactory.Instance);
services.AddSingleton<IOpcUaSessionFactory, OpcUaSessionFactory>();
services.AddSingleton<IOpcUaConnectionService, OpcUaConnectionService>();
services.AddSingleton<IOpcUaSubscriptionService, OpcUaSubscriptionService>();
```

---

## Пример использования

### Подписка на данные (Blazor компонент)

```csharp
@inject IOpcUaSubscriptionService OpcUa
@implements IDisposable

<div>Temperature: @_temperature °C</div>

@code {
    private double _temperature;
    private IDisposable? _subscription;

    protected override void OnInitialized()
    {
        _subscription = OpcUa.Subscribe("ns=2;s=Temperature", value =>
        {
            _temperature = Convert.ToDouble(value.Value);
            InvokeAsync(StateHasChanged);  // Маршалинг на UI thread
        });
    }

    public void Dispose() => _subscription?.Dispose();
}
```

### Подписка на несколько nodes

```csharp
_subscription = OpcUa.Subscribe(
    new[] { "ns=2;s=Temp1", "ns=2;s=Temp2", "ns=2;s=Pressure" },
    (nodeId, value) =>
    {
        _values[nodeId] = value.Value;
        InvokeAsync(StateHasChanged);
    });
```

### Подписка на события соединения

```csharp
public class MyComponent : IDisposable
{
    private readonly IOpcUaConnectionService _opcUa;

    public MyComponent(IOpcUaConnectionService opcUa)
    {
        _opcUa = opcUa;
        _opcUa.ConnectionChanged += OnConnectionChanged;
    }

    private void OnConnectionChanged(bool isConnected)
    {
        // Обновить UI индикатор
    }

    public void Dispose()
    {
        _opcUa.ConnectionChanged -= OnConnectionChanged;
    }
}
```

### Чтение данных

```csharp
public async Task<object?> ReadValueAsync(string nodeId)
{
    return await _opcUa.ExecuteWithSessionAsync(async session =>
    {
        var node = new NodeId(nodeId);
        var value = await session.ReadValueAsync(node);
        return value.Value;
    });
}
```

### Запись данных

```csharp
public async Task WriteValueAsync(string nodeId, object value)
{
    await _opcUa.ExecuteWithSessionAsync(async session =>
    {
        var writeValue = new WriteValue
        {
            NodeId = new NodeId(nodeId),
            AttributeId = Attributes.Value,
            Value = new DataValue(new Variant(value))
        };

        var response = await session.WriteAsync(
            null,
            new WriteValueCollection { writeValue },
            CancellationToken.None);

        if (StatusCode.IsBad(response.Results[0]))
        {
            throw new Exception($"Write failed: {response.Results[0]}");
        }
    });
}
```

---

## История рефакторинга

### Phase 2: Subscription Service (декабрь 2024)

**Задача:** Реализовать thread-safe сервис подписок для HMI панели.

**Созданные файлы:**

| Файл | Строк | Описание |
|------|-------|----------|
| `OpcUaSubscriptionSettings.cs` | 9 | POCO настроек подписок |
| `IOpcUaSubscriptionService.cs` | 25 | Интерфейс сервиса |
| `OpcUaSubscriptionService.cs` | ~220 | Core: поля, Subscribe, Dispose |
| `OpcUaSubscriptionService.Callbacks.cs` | 65 | Обработка OnDataChange |
| `OpcUaSubscriptionService.Recreate.cs` | 90 | Пересоздание при reconnect |

**Архитектурные решения:**

| Решение | Выбор | Причина |
|---------|-------|---------|
| Data API | Callback per node | Каждый компонент подписывается на свои теги |
| Auto-recreate | Да | Прозрачность для UI компонентов |
| Grouping | Один Subscription | Меньше overhead, проще управление |
| Locks | Два раздельных | `_stateLock` + `_callbackLock` для избежания deadlock |

**Thread-Safety:**

```csharp
// Lock ordering (для предотвращения deadlock):
1. _connectionService._sessionLock (через ExecuteWithSessionAsync)
2. _stateLock (SemaphoreSlim)
3. _callbackLock (ReaderWriterLockSlim, write)

// OnDataChange использует только read lock:
_callbackLock (read) → Invoke callbacks (БЕЗ локов)
```

**Ключевые паттерны:**

1. **Copy-on-read для callbacks** — snapshot создаётся при изменении, OnDataChange читает immutable copy
2. **IDisposable для отписки** — `CallbackEntry.Dispose()` удаляет callback из списка
3. **CompositeDisposable** — для multi-node подписок
4. **Атомарный dispose** — `Interlocked.Exchange(ref _disposeState, 1)`

**Изменённые файлы:**
- `Form1.cs` — DI регистрация `IOpcUaSubscriptionService`
- `appsettings.json` — секция `OpcUa:Subscription`

---

### Phase 2.1: Thread-Safety Hardening (декабрь 2024)

**Цель:** Исправить race conditions, утечки ресурсов, улучшить thread-safety.

**Найдено и исправлено 7 критических проблем:**

| # | Проблема | Решение |
|---|----------|---------|
| 1 | `GetAwaiter().GetResult()` блокировал поток | Отложенная инициализация `InitializeAsync()` |
| 2 | Утечка подписок при Recreate | Добавлен `session.RemoveSubscription()` |
| 3 | Race Subscribe + DisposeAsync | Повторная проверка `IsDisposed` после lock'а |
| 4 | Race IsDisposed + OnDataChange | `Task.Delay(50)` перед dispose lock'ов |
| 5 | Race в StartAsync | `StartAsync` теперь под `_sessionLock` |
| 6 | `_isReconnecting` не атомарный | `Interlocked.CompareExchange` |
| 7 | Утечка PeriodicTimer | Try-catch с cleanup при ошибке |

**Новый API:**
```csharp
public interface IOpcUaSubscriptionService : IAsyncDisposable
{
    bool IsInitialized { get; }
    IDisposable Subscribe(string nodeId, Action<DataValue> onValueChanged);
    Task InitializeAsync(CancellationToken ct = default);  // НОВЫЙ
}
```

**Паттерн batch initialization:**
```csharp
// До: 500 round-trips к серверу
foreach (var tag in tags)
    opcUa.Subscribe(tag, callback);  // Каждый вызов → ApplyChanges()

// После: 1 round-trip
foreach (var tag in tags)
    opcUa.Subscribe(tag, callback);  // Только добавляет в словарь
await opcUa.InitializeAsync();       // Один ApplyChanges() для всех
```

**Изменённые файлы:**
- `IOpcUaSubscriptionService.cs` — добавлен `InitializeAsync()`, `IsInitialized`
- `OpcUaSubscriptionService.cs` — отложенная инициализация, проверки IsDisposed
- `OpcUaSubscriptionService.Recreate.cs` — `session.RemoveSubscription()`, проверка disposed
- `OpcUaConnectionService.cs` — `StartAsync` под lock'ом, защита timer'а
- `OpcUaConnectionService.Reconnect.cs` — атомарный `_isReconnecting`

---

### Phase 2.1.1: Early Exit Optimizations (декабрь 2024)

**Цель:** Оптимизация раннего выхода при dispose для уменьшения блокировок.

**Изменения:**

| Файл | Метод | Оптимизация |
|------|-------|-------------|
| `OpcUaSubscriptionService.cs` | `RemoveCallback` | Двойная проверка `IsDisposed` (до и после lock) |
| `OpcUaConnectionService.cs` | `ExecuteWithSessionAsync` | Двойная проверка `ObjectDisposedException.ThrowIf` |
| `OpcUaConnectionService.cs` | `ExecuteWithSessionAsync<T>` | Двойная проверка `ObjectDisposedException.ThrowIf` |

**Паттерн:**

```csharp
// До взятия lock — ранний выход без блокировки
if (IsDisposed) return;

lock.Wait();
try
{
    // После взятия lock — повторная проверка (dispose мог произойти пока ждали)
    if (IsDisposed) return;
    // ... работа ...
}
finally
{
    lock.Release();
}
```

**Преимущества:**
- Избегаем ожидания lock при shutdown
- Явное исключение вместо неявного `ObjectDisposedException` от disposed lock
- Консистентность с остальным кодом (Subscribe, InitializeAsync уже используют этот паттерн)

---

### Phase 2.1.2: Lazy Subscription Creation Fix (декабрь 2024)

**Цель:** Исправить edge case когда `Subscribe()` вызывается после пустого `InitializeAsync()`.

**Проблема:**

```csharp
// Сценарий:
await opcUa.InitializeAsync();  // _subscriptions.Count == 0 → _opcSubscription = null
opcUa.Subscribe(nodeId, cb);    // IsInitialized == true → CreateMonitoredItemForEntry()
                                 // → _opcSubscription is null → ранний return
                                 // Callback добавлен но MonitoredItem НЕ создан!
```

**Решение:** Убран ранний выход в `CreateOpcSubscriptionAsync`. Subscription без MonitoredItems допустима в OPC UA — items добавляются позже через `ApplyChanges()`.

**Изменённые файлы:**
- `OpcUaSubscriptionService.cs` — удалён `if (_subscriptions.Count == 0) return;`

**До:**
```csharp
private async Task CreateOpcSubscriptionAsync(...)
{
    if (_subscriptions.Count == 0)  // Пропускаем создание
    {
        return;
    }
    await _connectionService.ExecuteWithSessionAsync(...);
}
```

**После:**
```csharp
private async Task CreateOpcSubscriptionAsync(...)
{
    await _connectionService.ExecuteWithSessionAsync(...);  // Всегда создаём
}
```

---

### Phase 1: Разбиение на файлы (декабрь 2024)

**Проблема:** `OpcUaConnectionService.cs` содержал 462 строки, превышая лимит 300 строк.

**Решение:** Разбиение по Single Responsibility:

| Извлечённый код | Новый файл | Строк |
|-----------------|------------|-------|
| `CreateApplicationConfigurationAsync()` | `OpcUaSessionFactory.cs` | 95 |
| `ValidateSettings()` | `OpcUaSettingsValidator.cs` | 53 |
| Reconnect методы (11 шт.) | `OpcUaConnectionService.Reconnect.cs` | 189 |

**Результат:** Основной файл сокращён с 462 до 223 строк.

### Phase 0.1: Hardening (декабрь 2024)

#### 1. Атомарный DisposeAsync

**Проблема:** Двойной вызов `DisposeAsync` мог вызвать `ObjectDisposedException` от `_sessionLock.Dispose()`.

**Решение:** `Interlocked.Exchange(ref _disposeState, 1)` — атомарная проверка и установка.

```csharp
private int _disposeState;
private bool IsDisposed => Volatile.Read(ref _disposeState) != 0;

public async ValueTask DisposeAsync()
{
    if (Interlocked.Exchange(ref _disposeState, 1) != 0)
    {
        return;
    }
    ...
}
```

#### 2. Защита от повторного StartAsync

**Проблема:** Повторный вызов `StartAsync` перезаписывал `_connectTimer`, вызывая утечку.

**Решение:** Проверка `_connectLoopTask is not null` + выброс исключений.

```csharp
public Task StartAsync(CancellationToken cancellationToken = default)
{
    if (IsDisposed)
    {
        throw new ObjectDisposedException(nameof(OpcUaConnectionService));
    }
    if (_connectLoopTask is not null)
    {
        throw new InvalidOperationException("Service is already started");
    }
    ...
}
```

#### 3. ContinueWith в HandleKeepAliveError

**Проблема:** Fire-and-forget в `HandleKeepAliveError` терял исключения.

**Решение:** Добавлен `ContinueWith` с логированием.

```csharp
_ = StartReconnectHandlerAsync(session).ContinueWith(
    t => _logger.LogError(t.Exception, "Error starting reconnect handler"),
    CancellationToken.None,
    TaskContinuationOptions.OnlyOnFaulted,
    TaskScheduler.Default);
```

### Phase 0: Исправление concurrency issues (декабрь 2024)

#### 1. Race Condition: флаг `_isReconnecting`

**Проблема:** Флаг устанавливался без синхронизации.

**Решение:** Установка флага под `_sessionLock`.

#### 2. Thread-safe доступ к Session

**Проблема:** Публичное свойство `Session` позволяло доступ во время переподключения.

**Решение:** Приватное поле `_session` + методы `ExecuteWithSessionAsync`.

#### 3. Fire-and-forget исключения

**Проблема:** Исключения в async callback'ах терялись.

**Решение:** `ContinueWith` с логированием ошибок в `OnReconnectComplete`.

#### 4. Блокирующие события

**Проблема:** Подписчики могли заблокировать reconnect flow.

**Решение:** Асинхронный вызов через `Task.Run`.

---

## Известные ограничения

1. **SessionReconnectHandler deprecated API** — используется конструктор без `ITelemetryContext`. При обновлении библиотеки OPC UA потребуется адаптация.

2. **Только Anonymous authentication** — для production может потребоваться добавить поддержку сертификатов и username/password.

3. **AutoAcceptUntrustedCertificates = true** — в production следует настроить proper certificate validation.

---

## Следующие шаги

### Phase 2.2: PlcDataStore

**Цель:** Централизованное хранилище данных PLC для логики и UI.

**Проблема:**
- UI компоненты (табы) mount/unmount → частые subscribe/unsubscribe к PLC
- Несколько сервисов логики читают одни и те же теги
- 500+ тегов — накладно пересоздавать при переключении табов

**Решение:** Singleton `PlcDataStore`:
- Подписывается на все теги один раз при старте
- Хранит последние значения в `ConcurrentDictionary`
- Раздаёт события `OnValueChanged` для UI
- Сервисы логики читают значения напрямую через `Get<T>()`

**Структура:**

```
Services/OpcUa/
└── PlcDataStore.cs   # ~50 строк
```

**API:**

```csharp
public class PlcDataStore
{
    // Инициализация — вызывается когда теги известны (из сервера/БД)
    public async Task InitializeAsync(IOpcUaSubscriptionService opcUa, IEnumerable<string> tags)
    {
        foreach (var tag in tags)
            opcUa.Subscribe(tag, value => OnValueChanged(tag, value));
        await opcUa.InitializeAsync();  // Один round-trip для всех тегов
    }

    // Читать значение
    public T? Get<T>(string nodeId);

    // Подписаться на конкретный тег (фильтрованно)
    public IDisposable Subscribe(string nodeId, Action<object?> callback);
    public IDisposable Subscribe(IEnumerable<string> nodeIds, Action<string, object?> callback);
}
```

**Почему фильтрованная подписка:**

| Вариант | При update тега |
|---------|-----------------|
| Общий `event OnValueChanged` | 500 подписчиков проверяют `if (nodeId == ...)` |
| `Subscribe(nodeId, callback)` | Вызываются только 2-3 подписчика этого тега |

Фильтрованный вариант быстрее и чище в использовании.

**Использование:**

```csharp
// Логика — читает + реагирует
public class TemperatureMonitor
{
    private readonly PlcDataStore _plc;

    public TemperatureMonitor(PlcDataStore plc)
    {
        _plc = plc;
        _plc.Subscribe(PlcTags.Temperature, OnTemperatureChanged);
    }

    private void OnTemperatureChanged(object? value)
    {
        var temp = (double)value!;
        if (temp > 100) { /* аларм */ }
    }

    public bool IsOverheated() => _plc.Get<double>(PlcTags.Temperature) > 100;
}

// UI — подписка на конкретный тег
@inject PlcDataStore Plc
@implements IDisposable

@code {
    private double _temp;
    private IDisposable? _sub;

    protected override void OnInitialized()
    {
        _temp = Plc.Get<double>(PlcTags.Temperature);
        _sub = Plc.Subscribe(PlcTags.Temperature, v =>
        {
            _temp = (double)v!;
            InvokeAsync(StateHasChanged);
        });
    }

    public void Dispose() => _sub?.Dispose();
}
```

**Источники тегов:**
- Хардкод (`PlcTags.cs`)
- JSON с сервера
- CSV / БД

---

## Будущее развитие (Phase 3)

При добавлении read/write сервиса — возможная структура:

```
Services/OpcUa/
├── IOpcUaDataService.cs           # Интерфейс для Read/Write
├── OpcUaDataService.cs            # Реализация Read/Write операций
```

**Возможный API:**

```csharp
public interface IOpcUaDataService
{
    Task<DataValue> ReadAsync(string nodeId, CancellationToken ct = default);
    Task<DataValue[]> ReadAsync(IEnumerable<string> nodeIds, CancellationToken ct = default);
    Task WriteAsync(string nodeId, object value, CancellationToken ct = default);
}
```
