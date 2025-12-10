# План: OPC UA Subscription Service

## Цель
Реализовать thread-safe сервис подписок для HMI панели с плавным обновлением UI.

---

## Архитектурные решения

| Решение | Выбор | Причина |
|---------|-------|---------|
| Data API | Callback per node | Каждый компонент подписывается на свои теги |
| Auto-recreate | Да | Прозрачность для UI компонентов |
| Grouping | Один Subscription | Меньше overhead, проще управление |
| Locks | Два раздельных | `_stateLock` + `_callbackLock` для избежания deadlock |

---

## Структура файлов

```
Services/OpcUa/
├── IOpcUaSubscriptionService.cs           # Интерфейс (~30 строк)
├── OpcUaSubscriptionService.cs            # Core: поля, Subscribe (~200 строк)
├── OpcUaSubscriptionService.Callbacks.cs  # Partial: callback invocation (~100 строк)
├── OpcUaSubscriptionService.Recreate.cs   # Partial: session recreation (~80 строк)
├── OpcUaSubscriptionSettings.cs           # POCO настроек (~20 строк)
│
├── IOpcUaConnectionService.cs             # Без изменений
├── OpcUaConnectionService.cs              # Без изменений
└── ...
```

---

## API сервиса

```csharp
public interface IOpcUaSubscriptionService : IAsyncDisposable
{
    /// <summary>
    /// Подписаться на изменения значения node.
    /// Callback вызывается в потоке OPC UA SDK (не UI thread).
    /// </summary>
    /// <returns>IDisposable для отписки</returns>
    IDisposable Subscribe(string nodeId, Action<DataValue> onValueChanged);

    /// <summary>
    /// Подписаться на несколько nodes одним вызовом.
    /// </summary>
    IDisposable Subscribe(IEnumerable<string> nodeIds, Action<string, DataValue> onValueChanged);
}
```

---

## Пример использования в Blazor компоненте

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

---

## Внутренние модели

```csharp
// Запись для каждого nodeId
private sealed class SubscriptionEntry
{
    public required string NodeId { get; init; }
    public required MonitoredItem MonitoredItem { get; init; }
    public List<CallbackEntry> Callbacks { get; } = new();
}

// IDisposable токен для отписки
private sealed class CallbackEntry : IDisposable
{
    public required Guid Id { get; init; }
    public required Action<DataValue> Callback { get; init; }
    public required string NodeId { get; init; }
    public required OpcUaSubscriptionService Owner { get; init; }
    private int _disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Owner.RemoveCallback(NodeId, Id);
    }
}
```

---

## OpcUaSubscriptionSettings.cs

```csharp
public sealed class OpcUaSubscriptionSettings
{
    public int PublishingIntervalMs { get; init; } = 500;
    public int SamplingIntervalMs { get; init; } = 250;
    public uint QueueSize { get; init; } = 10;
}
```

---

## Регистрация в DI (Form1.cs)

```csharp
services.Configure<OpcUaSubscriptionSettings>(_config.GetSection("OpcUa:Subscription"));
services.AddSingleton<IOpcUaSubscriptionService, OpcUaSubscriptionService>();
```

---

## Конфигурация (appsettings.json)

```json
{
  "OpcUa": {
    "EndpointUrl": "opc.tcp://...",
    "Subscription": {
      "PublishingIntervalMs": 500,
      "SamplingIntervalMs": 250,
      "QueueSize": 10
    }
  }
}
```

---

## Порядок реализации

1. **OpcUaSubscriptionSettings.cs** — POCO настроек
2. **IOpcUaSubscriptionService.cs** — интерфейс
3. **OpcUaSubscriptionService.cs** — core:
   - Поля и конструктор
   - `Subscribe()` / `RemoveCallback()`
   - `CreateSubscriptionEntry()` / `EnsureSubscription()`
   - `UpdateCallbackSnapshot()`
   - `DisposeAsync()`
4. **OpcUaSubscriptionService.Callbacks.cs** — partial:
   - `OnDataChange()` — callback от OPC UA SDK
   - `InvokeCallbacksSafe()` — безопасный вызов callbacks
5. **OpcUaSubscriptionService.Recreate.cs** — partial:
   - `OnSessionRecreated()` — handler события
   - `RecreateAllSubscriptionsAsync()` — пересоздание
6. **Form1.cs** — регистрация в DI
7. **appsettings.json** — секция Subscription
8. **refactoring.md** — документация

---

## Thread-Safety: Поля и синхронизация

```csharp
public sealed partial class OpcUaSubscriptionService : IOpcUaSubscriptionService
{
    // === IMMUTABLE ===
    private readonly IOpcUaConnectionService _connectionService;
    private readonly ILogger<OpcUaSubscriptionService> _logger;
    private readonly OpcUaSubscriptionSettings _settings;

    // === PROTECTED BY _stateLock ===
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly Dictionary<string, SubscriptionEntry> _subscriptions = new();
    private Subscription? _opcSubscription;

    // === PROTECTED BY _callbackLock ===
    private readonly ReaderWriterLockSlim _callbackLock = new();
    private Dictionary<string, List<CallbackEntry>> _callbackSnapshot = new();

    // === ATOMIC ===
    private int _disposeState;
    private volatile bool _isRecreating;
}
```

---

## Порядок захвата локов (Lock Ordering)

**Правило:** Всегда в этом порядке для предотвращения deadlock:

```
1. _connectionService._sessionLock (через ExecuteWithSessionAsync)
           ↓
2. _stateLock
           ↓
3. _callbackLock (write)
```

**OnDataChange (OPC UA thread):**
```
_callbackLock (read only)
           ↓
Invoke callbacks (БЕЗ локов!)
```

---

## Race Conditions и решения

### 1. Concurrent Subscribe на один nodeId

```
Thread A: Subscribe("temp", cb1) — проверяет exists
Thread B: Subscribe("temp", cb2) — тоже проверяет, видит false
Оба создают MonitoredItem!
```

**Решение:** `_stateLock` сериализует все Subscribe операции.

---

### 2. Unsubscribe во время OnDataChange

```
OPC UA thread: вызывает callback для "temp"
UI thread: Unsubscribe("temp") удаляет callback
Callback вызван на disposed компоненте!
```

**Решение:** Copy-on-read pattern:

```csharp
private void OnDataChange(MonitoredItem item, ...)
{
    List<CallbackEntry> callbacksCopy;

    _callbackLock.EnterReadLock();
    try
    {
        callbacksCopy = _callbackSnapshot[nodeId]; // immutable snapshot
    }
    finally
    {
        _callbackLock.ExitReadLock();
    }

    // Вызов callbacks ВНЕ лока
    foreach (var entry in callbacksCopy)
    {
        InvokeSafe(entry);
    }
}
```

---

### 3. Subscribe во время SessionRecreated

```
Thread A: Создаёт MonitoredItem
Reconnect: Сессия пересоздана, старый Subscription невалиден
MonitoredItem создан на старой сессии!
```

**Решение:** `ExecuteWithSessionAsync` держит session lock. Пока Subscribe работает, SessionRecreated ждёт. И наоборот.

---

### 4. Dispose во время OnDataChange

```
Service disposing
OPC UA SDK всё ещё вызывает callbacks
NullReferenceException!
```

**Решение:** Многоуровневая защита:

```csharp
private void OnDataChange(...)
{
    // Layer 1: Quick check
    if (IsDisposed) return;

    try
    {
        OnDataChangeCore(...);
    }
    catch (ObjectDisposedException)
    {
        // Expected during shutdown
    }
}
```

---

## Dispose Sequence

```csharp
public async ValueTask DisposeAsync()
{
    // 1. Atomic dispose check
    if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;

    // 2. Отписка от событий
    _connectionService.SessionRecreated -= OnSessionRecreated;

    // 3. Захват _stateLock (блокирует новые Subscribe)
    await _stateLock.WaitAsync();
    try
    {
        // 4. Очистка callback snapshot
        _callbackLock.EnterWriteLock();
        try { _callbackSnapshot = new(); }
        finally { _callbackLock.ExitWriteLock(); }

        // 5. Dispose OPC UA Subscription
        await DisposeOpcSubscription();

        // 6. Clear state
        _subscriptions.Clear();
    }
    finally
    {
        _stateLock.Release();
    }

    // 7. Dispose локов
    _stateLock.Dispose();
    _callbackLock.Dispose();
}
```

---

## Обработка reconnect (включая длительный разрыв)

**Сценарий: сеть упала на 10+ минут**

```
Сеть упала → KeepAlive Error → SessionReconnectHandler пытается...
                    ↓
         [Длительный разрыв - сессия на сервере истекла]
                    ↓
        Сеть восстановилась
                    ↓
    SessionReconnectHandler создаёт НОВУЮ сессию
                    ↓
    TransferSubscriptions → FAIL (сервер не поддерживает или сессия мертва)
                    ↓
    SessionRecreated event → OPC UA подписки потеряны
                    ↓
    НАШ СЕРВИС пересоздаёт всё
```

**Логика восстановления:**

```
SessionRecreated event (от OpcUaConnectionService)
       ↓
OnSessionRecreated() [fire-and-forget с ContinueWith]
       ↓
await _stateLock.WaitAsync() [блокирует новые Subscribe]
       ↓
_isRecreating = true
       ↓
Dispose старый _opcSubscription (если есть)
       ↓
ExecuteWithSessionAsync(newSession =>
{
    // 1. Создать НОВЫЙ Subscription на новой сессии
    _opcSubscription = new Subscription(newSession.DefaultSubscription);
    newSession.AddSubscription(_opcSubscription);
    _opcSubscription.Create();

    // 2. Восстановить ВСЕ MonitoredItems из _subscriptions
    foreach (var entry in _subscriptions.Values)
    {
        var item = new MonitoredItem { StartNodeId = entry.NodeId, ... };
        item.Notification += OnDataChange;
        _opcSubscription.AddItem(item);
        entry.MonitoredItem = item;  // обновить ссылку
    }

    _opcSubscription.ApplyChanges();
})
       ↓
_isRecreating = false
       ↓
_stateLock.Release()
```

**Гарантии:**
- `_subscriptions` dictionary хранит nodeId + callbacks — **НЕ теряется**
- OPC UA объекты (Subscription, MonitoredItem) пересоздаются
- UI компоненты **не знают о разрыве** — их IDisposable токены валидны
- Данные начнут приходить автоматически после пересоздания

---

## Файлы для изменения

| Файл | Действие |
|------|----------|
| `Services/OpcUa/IOpcUaSubscriptionService.cs` | CREATE |
| `Services/OpcUa/OpcUaSubscriptionService.cs` | CREATE |
| `Services/OpcUa/OpcUaSubscriptionService.Callbacks.cs` | CREATE |
| `Services/OpcUa/OpcUaSubscriptionService.Recreate.cs` | CREATE |
| `Services/OpcUa/OpcUaSubscriptionSettings.cs` | CREATE |
| `Form1.cs` | EDIT — добавить регистрацию |
| `appsettings.json` | EDIT — добавить секцию Subscription |
| `Services/OpcUa/refactoring.md` | EDIT — документация |
