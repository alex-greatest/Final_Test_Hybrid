/c# OPC UA Services - Документация и рефакторинг

## Обзор модуля

Модуль `Services/OpcUa` предоставляет инфраструктуру для подключения к OPC UA серверам промышленного оборудования. OPC UA (Open Platform Communications Unified Architecture) — стандартный протокол для обмена данными в промышленной автоматизации.

---

## Структура файлов

```
Services/OpcUa/
├── IOpcUaConnectionService.cs          # Интерфейс сервиса подключения (24 строки)
├── OpcUaConnectionService.cs           # Core: lifecycle, connect loop (~250 строк)
├── OpcUaConnectionService.Reconnect.cs # Partial: reconnect логика (~210 строк)
│
├── IOpcUaSessionFactory.cs             # Интерфейс фабрики сессий (13 строк)
├── OpcUaSessionFactory.cs              # Создание сессий + конфигурация (87 строк)
│
├── OpcUaSettings.cs                    # POCO настроек (16 строк)
├── OpcUaSettingsValidator.cs           # Валидация настроек (64 строки)
│
├── refactoring.md                      # Эта документация
└── plan.md                             # План рефакторинга
```

**Все файлы < 300 строк** (лимит из `.cursorrules`).

---

## Описание классов

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
    "SessionTimeoutMs": 60000
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
```

---

## Регистрация в DI (Form1.cs)

```csharp
services.Configure<OpcUaSettings>(configuration.GetSection("OpcUa"));
services.AddSingleton<ISessionFactory>(sp => DefaultSessionFactory.Instance);
services.AddSingleton<IOpcUaSessionFactory, OpcUaSessionFactory>();
services.AddSingleton<IOpcUaConnectionService, OpcUaConnectionService>();
```

---

## Пример использования

### Подписка на события

```csharp
public class MyComponent : IDisposable
{
    private readonly IOpcUaConnectionService _opcUa;

    public MyComponent(IOpcUaConnectionService opcUa)
    {
        _opcUa = opcUa;
        _opcUa.ConnectionChanged += OnConnectionChanged;
        _opcUa.SessionRecreated += OnSessionRecreated;
    }

    private void OnConnectionChanged(bool isConnected)
    {
        // Обновить UI индикатор
    }

    private void OnSessionRecreated()
    {
        // Пересоздать подписки на данные
    }

    public void Dispose()
    {
        _opcUa.ConnectionChanged -= OnConnectionChanged;
        _opcUa.SessionRecreated -= OnSessionRecreated;
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

## Будущее развитие (Phase 2)

При добавлении подписок и read/write сервисов — возможная структура:

```
Services/OpcUa/
├── Connection/
│   ├── IOpcUaConnectionService.cs
│   ├── OpcUaConnectionService.cs
│   └── OpcUaConnectionService.Reconnect.cs
│
├── Session/
│   ├── IOpcUaSessionFactory.cs
│   └── OpcUaSessionFactory.cs
│
├── Data/
│   ├── IOpcUaDataService.cs
│   └── OpcUaDataService.cs
│
├── Subscriptions/
│   ├── IOpcUaSubscriptionService.cs
│   └── OpcUaSubscriptionService.cs
│
└── Configuration/
    ├── OpcUaSettings.cs
    └── OpcUaSettingsValidator.cs
```
