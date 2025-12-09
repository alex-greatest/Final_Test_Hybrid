# OPC UA Services - Документация и рефакторинг

## Обзор модуля

Модуль `Services/OpcUa` предоставляет инфраструктуру для подключения к OPC UA серверам промышленного оборудования. OPC UA (Open Platform Communications Unified Architecture) — стандартный протокол для обмена данными в промышленной автоматизации.

---

## Структура файлов

```
Services/OpcUa/
├── IOpcUaConnectionService.cs   # Интерфейс сервиса подключения
├── OpcUaConnectionService.cs    # Реализация с управлением сессией
├── OpcUaSettings.cs             # Конфигурация подключения
└── refactoring.md               # Эта документация
```

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

### IOpcUaConnectionService

**Назначение:** Контракт сервиса для управления подключением к OPC UA серверу.

```csharp
public interface IOpcUaConnectionService : IAsyncDisposable
{
    // Состояние подключения
    bool IsConnected { get; }

    // События
    event Action<bool>? ConnectionChanged;    // Изменение состояния подключения
    event Action? SessionRecreated;           // Сессия пересоздана (подписки потеряны)

    // Управление жизненным циклом
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();

    // Thread-safe доступ к сессии
    Task ExecuteWithSessionAsync(Func<ISession, Task> action, CancellationToken ct = default);
    Task<T> ExecuteWithSessionAsync<T>(Func<ISession, Task<T>> action, CancellationToken ct = default);
}
```

---

### OpcUaConnectionService

**Назначение:** Реализация сервиса подключения с автоматическим переподключением.

**Ключевые возможности:**
- Автоматическое подключение при старте
- Периодические попытки переподключения при разрыве связи
- Обработка KeepAlive для детектирования потери соединения
- Thread-safe операции с сессией
- Корректное освобождение ресурсов

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

## Проведённый рефакторинг (декабрь 2024)

### Исправленные проблемы

#### 1. Race Condition: флаг `_isReconnecting`

**Проблема:** Флаг устанавливался без синхронизации, что могло привести к параллельным попыткам подключения.

**Было:**
```csharp
private void StartReconnectHandler(ISession session)
{
    _isReconnecting = true;  // БЕЗ lock
    _reconnectHandler = new SessionReconnectHandler();
    _reconnectHandler.BeginReconnect(...);
}
```

**Стало:**
```csharp
private async Task StartReconnectHandlerAsync(ISession session)
{
    await _sessionLock.WaitAsync(_disposeCts.Token).ConfigureAwait(false);
    try
    {
        if (_isReconnecting || _isDisposed) return;
        _isReconnecting = true;
        _reconnectHandler = new SessionReconnectHandler();
        _reconnectHandler.BeginReconnect(...);
    }
    finally
    {
        _sessionLock.Release();
    }
}
```

**Сценарий гонки (предотвращён):**
1. Thread A: KeepAlive error → вызывает `StartReconnectHandler`
2. Thread B: `TryConnectIfNotConnectedAsync` проверяет `_isReconnecting` (ещё false)
3. Thread A: устанавливает `_isReconnecting = true`
4. Thread B: создаёт новую сессию параллельно с reconnect handler
5. **Результат:** две активные сессии, утечка ресурсов

---

#### 2. Thread-safe доступ к Session

**Проблема:** Публичное свойство `Session` позволяло внешнему коду получить сессию во время переподключения.

**Было:**
```csharp
public ISession? Session { get; private set; }

// Внешний код:
var value = await opcUaService.Session!.ReadValueAsync(nodeId);  // Опасно!
```

**Стало:**
```csharp
private ISession? _session;  // Приватное поле

// Безопасный доступ через метод:
public async Task<T> ExecuteWithSessionAsync<T>(Func<ISession, Task<T>> action, CancellationToken ct)
{
    await _sessionLock.WaitAsync(ct).ConfigureAwait(false);
    try
    {
        EnsureConnected();
        return await action(_session!).ConfigureAwait(false);
    }
    finally
    {
        _sessionLock.Release();
    }
}

// Использование:
var value = await opcUaService.ExecuteWithSessionAsync(
    session => session.ReadValueAsync(nodeId));
```

---

#### 3. Fire-and-forget исключения

**Проблема:** Исключения в асинхронных callback'ах терялись.

**Было:**
```csharp
private void OnReconnectComplete(object? sender, EventArgs e)
{
    _ = HandleReconnectCompleteAsync();  // Исключения не логируются
}
```

**Стало:**
```csharp
private void OnReconnectComplete(object? sender, EventArgs e)
{
    if (_isDisposed) return;

    _ = HandleReconnectCompleteAsync().ContinueWith(
        t => _logger.LogError(t.Exception, "Unhandled exception in reconnect handler"),
        CancellationToken.None,
        TaskContinuationOptions.OnlyOnFaulted,
        TaskScheduler.Default);
}
```

---

#### 4. Блокирующие события

**Проблема:** Подписчики событий могли заблокировать reconnect flow.

**Было:**
```csharp
private void RaiseConnectionChanged(bool isConnected)
{
    try
    {
        ConnectionChanged?.Invoke(isConnected);  // Синхронный вызов
    }
    catch (Exception ex) { ... }
}
```

**Стало:**
```csharp
private void RaiseConnectionChangedAsync(bool isConnected)
{
    var handler = ConnectionChanged;
    if (handler is null) return;

    Task.Run(() =>
    {
        try
        {
            handler.Invoke(isConnected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ConnectionChanged handler");
        }
    });
}
```

---

#### 5. Упрощение `TryConnectIfNotConnectedAsync`

**Было:**
```csharp
private async Task TryConnectIfNotConnectedAsync()
{
    if (IsConnected || _isReconnecting)  // Проверка IsConnected вне lock
    {
        return;
    }
    await TryConnectAsync().ConfigureAwait(false);
}
```

**Стало:**
```csharp
private async Task TryConnectIfNotConnectedAsync()
{
    if (_isReconnecting)  // volatile достаточно для быстрого выхода
    {
        return;
    }
    await TryConnectAsync().ConfigureAwait(false);
    // IsConnected проверяется внутри TryConnectCoreAsync под lock
}
```

---

## Пример использования

### Регистрация в DI (Form1.cs)

```csharp
services.Configure<OpcUaSettings>(configuration.GetSection("OpcUa"));
services.AddSingleton<ISessionFactory, DefaultSessionFactory>();
services.AddSingleton<IOpcUaConnectionService, OpcUaConnectionService>();
```

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

## Известные ограничения

1. **SessionReconnectHandler deprecated API** — используется конструктор без `ITelemetryContext`. При обновлении библиотеки OPC UA потребуется адаптация.

2. **Только Anonymous authentication** — для production может потребоваться добавить поддержку сертификатов и username/password.

3. **AutoAcceptUntrustedCertificates = true** — в production следует настроить proper certificate validation.

---

## Связанные файлы

- `appsettings.json` — секция `OpcUa` с настройками подключения
- `Form1.cs` — регистрация сервисов в DI (закомментировано, подготовлено к активации)
