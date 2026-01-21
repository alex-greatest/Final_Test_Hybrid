# Diagnostic Service Guide

Руководство по работе с диагностическим сервисом Modbus для связи с ЭБУ котла.

## Архитектура

```
[UI/Services] ──► [RegisterReader/Writer] ──► [QueuedModbusClient] ──► [ModbusDispatcher] ──► [SerialPort]
                                                      │                        │
[PollingService] ──► [PollingTask] ─────────────────────┘                        │
  (Low priority)                                                          State events
                                                                    (Connected, Disconnecting)
                                                                              │
                                                                    [Ping Keep-Alive Task]
                                                                      (Low priority)
```

**Ключевые компоненты:**
- `IModbusDispatcher` — диспетчер команд с приоритетной очередью
- `IModbusClient` — клиент для чтения/записи регистров
- `RegisterReader` / `RegisterWriter` — высокоуровневые операции с типизацией
- `PollingService` / `PollingTask` — периодический опрос регистров
- `PingCommand` — keep-alive команда, читает ModeKey + BoilerStatus

## Установка связи

**ВАЖНО:** Связь требует явного вызова `StartAsync()` перед любыми операциями.

### Обязательный старт

```csharp
public class MyService(IModbusDispatcher dispatcher)
{
    public async Task InitializeAsync()
    {
        // Необходимо вызвать перед любым чтением/записью
        await dispatcher.StartAsync();
    }
}
```

**Без вызова `StartAsync()`:**
- `RegisterReader` вернёт `DiagnosticReadResult.Fail` с ошибкой
- `RegisterWriter` вернёт `DiagnosticWriteResult.Fail` с ошибкой
- `IModbusClient` напрямую выбросит `InvalidOperationException`

### Инициализация в приложении (Form1.cs)

```csharp
private static async void ConfigureDiagnosticEvents(ServiceProvider serviceProvider)
{
    var dispatcher = serviceProvider.GetRequiredService<IModbusDispatcher>();

    // Запускаем диспетчер при старте приложения
    await dispatcher.StartAsync();
}
```

## Состояние связи (IsConnected)

### Новая логика IsConnected

После `StartAsync()` состояние определяется так:

| Состояние | IsConnected | IsReconnecting | Описание |
|-----------|-------------|----------------|----------|
| Порт открыт | `false` | `false` | Порт открыт, но устройство не подтвердило связь |
| Первая успешная команда | `true` | `false` | Устройство ответило — связь подтверждена |
| Таймаут команды | `false` | `true` | Ошибка связи — переподключение |
| После StopAsync | `false` | `false` | Диспетчер остановлен |

**Важно:** `IsConnected = true` устанавливается только после первой **успешной** команды (ping или пользовательской).

### Свойства

```csharp
public class MyService(IModbusDispatcher dispatcher)
{
    public void CheckStatus()
    {
        // Запущен ли диспетчер
        bool isStarted = dispatcher.IsStarted;

        // Подтверждена ли связь с устройством
        bool isConnected = dispatcher.IsConnected;

        // Идёт ли переподключение
        bool isReconnecting = dispatcher.IsReconnecting;

        // Последние данные ping (ModeKey, BoilerStatus)
        var pingData = dispatcher.LastPingData;
    }
}
```

### События

```csharp
public class MyService(IModbusDispatcher dispatcher)
{
    public void SubscribeToEvents()
    {
        // Соединение подтверждено (после первой успешной команды)
        dispatcher.Connected += OnConnected;

        // Соединение разрывается (вызывается ДО отключения)
        dispatcher.Disconnecting += OnDisconnectingAsync;
    }

    private void OnConnected()
    {
        Console.WriteLine("Связь с ЭБУ подтверждена");
    }

    private Task OnDisconnectingAsync()
    {
        Console.WriteLine("Отключение...");
        return Task.CompletedTask;
    }
}
```

**Безопасность событий:**

Оба события вызываются внутри try/catch — исключения в обработчиках **не прерывают** работу диспетчера:

```csharp
// Внутренняя реализация
private void NotifyConnectedSafely()
{
    try { Connected?.Invoke(); }
    catch (Exception ex) { _logger.LogError(ex, "..."); }
}
```

### Использование в UI (Blazor)

```csharp
@inject IModbusDispatcher Dispatcher

<div class="status">
    @if (Dispatcher.IsConnected)
    {
        <span class="connected">Подключено</span>
        @if (Dispatcher.LastPingData != null)
        {
            <span>ModeKey: @Dispatcher.LastPingData.ModeKey.ToString("X8")</span>
            <span>Status: @Dispatcher.LastPingData.BoilerStatus</span>
        }
    }
    else if (Dispatcher.IsReconnecting)
    {
        <span class="reconnecting">Переподключение...</span>
    }
    else
    {
        <span class="disconnected">Отключено</span>
    }
</div>
```

## Разрыв связи и рестарт

### Остановка диспетчера

```csharp
public class MyService(IModbusDispatcher dispatcher)
{
    public async Task StopConnectionAsync()
    {
        await dispatcher.StopAsync();
        // Соединение закрыто, все pending команды отменены
    }
}
```

**Поведение `StopAsync()`:**

1. Завершает каналы команд (новые команды не принимаются)
2. Отменяет ping task
3. Отменяет worker task
4. **Немедленно закрывает COM-порт** — прерывает текущую Modbus команду через IOException
5. Ожидает завершения worker (таймаут 5 сек)
6. Отменяет все pending команды в очереди
7. Сбрасывает состояние (`IsConnected = false`, `LastPingData = null`)

**Защита от зависания:**

| Сценарий | Поведение |
|----------|-----------|
| Worker завершился нормально | Рестарт разрешён |
| Worker таймаут (>5 сек) | **Рестарт заблокирован**, логируется CRITICAL |
| Текущая команда выполняется | Прерывается через `Close()` → IOException |

### Рестарт после StopAsync

**Поддерживается!** После `StopAsync()` можно снова вызвать `StartAsync()`:

```csharp
public class MyService(IModbusDispatcher dispatcher)
{
    public async Task RestartAsync()
    {
        await dispatcher.StopAsync();
        // ... пауза или другие действия ...
        await dispatcher.StartAsync(); // Работает!
    }
}
```

При рестарте:
- Создаются новые каналы команд (очередь пуста)
- Начинается попытка подключения
- Запускается ping keep-alive

### Интеграция с PLC Reset

Диагностический сервис автоматически останавливается при сбросе PLC:

| Событие | Источник | Действие |
|---------|----------|----------|
| Soft Reset | `PlcResetCoordinator.OnForceStop` | `dispatcher.StopAsync()` |
| Hard Reset | `ErrorCoordinator.OnReset` | `dispatcher.StopAsync()` |

**Реализация в Form1.cs:**

```csharp
// Безопасная остановка с обработкой ошибок
plcResetCoordinator.OnForceStop += () => StopDispatcherSafely(dispatcher);
errorCoordinator.OnReset += () => StopDispatcherSafely(dispatcher);

private static void StopDispatcherSafely(IModbusDispatcher dispatcher)
{
    _ = dispatcher.StopAsync().ContinueWith(t =>
    {
        if (t.IsFaulted)
            Debug.WriteLine($"Ошибка: {t.Exception?.GetBaseException().Message}");
    }, TaskScheduler.Default);
}
```

После reset пользователь может вручную вызвать `StartAsync()` для восстановления связи.

## Ping Keep-Alive

Диспетчер периодически отправляет ping-команду для:
1. Проверки связи при отсутствии пользовательских команд
2. Чтения полезных данных (ModeKey, BoilerStatus)

### Данные Ping

| Параметр | Адрес (док) | Modbus | Тип | Описание |
|----------|-------------|--------|-----|----------|
| ModeKey | 1000-1001 | 999-1000 | uint32 | Ключ режима |
| BoilerStatus | 1005 | 1004 | int16 | Статус котла |

**Значения ModeKey:**
- `0xD7F8DB56` — стендовый режим
- `0xFA87CD5E` — инженерный режим
- Другое — обычный режим

**Значения BoilerStatus:**
- `-1` — тест
- `0` — включение
- `1-10` — различные режимы работы

### Доступ к данным ping

```csharp
var pingData = dispatcher.LastPingData;
if (pingData != null)
{
    var modeKey = pingData.ModeKey;
    var status = pingData.BoilerStatus;
}
```

## Чтение регистров

### Через RegisterReader (рекомендуется)

```csharp
public class MyService(RegisterReader reader)
{
    public async Task ReadValuesAsync(CancellationToken ct)
    {
        // Чтение одного регистра
        var result = await reader.ReadUInt16Async(0x1000, ct: ct);
        if (result.Success)
        {
            ushort value = result.Value;
        }
        else
        {
            // result.Error содержит сообщение об ошибке
            // Включая "Диспетчер не запущен" если не вызван StartAsync()
        }

        // Чтение с приоритетом (Low для фоновых задач)
        var lowPriorityResult = await reader.ReadUInt16Async(
            0x1000,
            priority: CommandPriority.Low,
            ct: ct);
    }
}
```

### Через IModbusClient (низкоуровневый)

```csharp
public class MyService(IModbusClient client)
{
    public async Task ReadRawAsync(CancellationToken ct)
    {
        // ВАЖНО: выбросит InvalidOperationException если dispatcher не запущен
        ushort[] registers = await client.ReadHoldingRegistersAsync(
            address: 0x1000,
            count: 5,
            priority: CommandPriority.High,
            ct: ct);
    }
}
```

## Запись регистров

### Через RegisterWriter (рекомендуется)

```csharp
public class MyService(RegisterWriter writer)
{
    public async Task WriteValuesAsync(CancellationToken ct)
    {
        // Запись одного регистра
        var result = await writer.WriteUInt16Async(0x2000, 42, ct);
        if (!result.Success)
        {
            Console.WriteLine($"Ошибка: {result.Error}");
        }
    }
}
```

## Периодический опрос (Polling)

### Запуск задачи опроса

```csharp
public class MyService(PollingService pollingService)
{
    public void StartPolling()
    {
        // Определяем адреса для опроса
        var addresses = new ushort[] { 0x1000, 0x1001, 0x1002 };

        // Запускаем опрос
        pollingService.StartPolling(
            name: "StatusPolling",
            addresses: addresses,
            interval: TimeSpan.FromSeconds(1),
            callback: OnDataReceived);
    }

    private Task OnDataReceived(Dictionary<ushort, object> data)
    {
        foreach (var (address, value) in data)
        {
            Console.WriteLine($"[0x{address:X4}] = {value}");
        }
        return Task.CompletedTask;
    }
}
```

### Остановка задачи опроса

```csharp
public class MyService(PollingService pollingService)
{
    public async Task StopPollingAsync()
    {
        // Остановить конкретную задачу
        await pollingService.StopPollingAsync("StatusPolling");

        // Или остановить все задачи
        await pollingService.StopAllTasksAsync();
    }
}
```

## Приоритеты команд

| Приоритет | Использование |
|-----------|---------------|
| `CommandPriority.High` | One-off операции (чтение/запись по запросу пользователя) |
| `CommandPriority.Low` | Фоновый polling и ping keep-alive |

High-priority команды всегда выполняются раньше Low-priority.

## Настройки (appsettings.json)

```json
{
  "Diagnostic": {
    "PortName": "COM3",
    "BaudRate": 115200,
    "DataBits": 8,
    "Parity": "None",
    "StopBits": "One",
    "SlaveId": 1,
    "ReadTimeoutMs": 1000,
    "WriteTimeoutMs": 1000,
    "BaseAddressOffset": 1,
    "CommandQueue": {
      "HighPriorityQueueCapacity": 100,
      "LowPriorityQueueCapacity": 10,
      "ReconnectDelayMs": 5000,
      "CommandWaitTimeoutMs": 100,
      "PingIntervalMs": 5000
    }
  }
}
```

### DiagnosticSettings — COM-порт и Modbus RTU

| Параметр | Тип | Default | Описание |
|----------|-----|---------|----------|
| `PortName` | string | "COM1" | Имя COM-порта (USB-RS485 адаптер) |
| `BaudRate` | int | 115200 | Скорость передачи (бод). Должна совпадать с ЭБУ |
| `DataBits` | int | 8 | Биты данных (стандарт для Modbus RTU) |
| `Parity` | Parity | None | Контроль чётности: None/Odd/Even/Mark/Space |
| `StopBits` | StopBits | One | Стоповые биты: One/Two/OnePointFive |
| `SlaveId` | byte | 1 | Modbus Slave ID устройства (адрес ведомого) |
| `ReadTimeoutMs` | int | 1000 | Таймаут ответа на чтение (мс) |
| `WriteTimeoutMs` | int | 1000 | Таймаут ответа на запись (мс) |
| `BaseAddressOffset` | ushort | 1 | Смещение адресов: документация (1005) → Modbus (1004) |

### ModbusDispatcherOptions — Command Queue

| Параметр | Тип | Default | Описание |
|----------|-----|---------|----------|
| `HighPriorityQueueCapacity` | int | 100 | Размер очереди для one-off команд (UI, read/write по запросу) |
| `LowPriorityQueueCapacity` | int | 10 | Размер очереди для polling и ping |
| `ReconnectDelayMs` | int | 5000 | Фиксированный интервал переподключения (5 сек) |
| `CommandWaitTimeoutMs` | int | 100 | Как долго воркер ждёт команду перед проверкой состояния |
| `PingIntervalMs` | int | 5000 | Интервал ping keep-alive (5 сек) |

## DI регистрация

Сервисы регистрируются автоматически через `AddDiagnosticServices()`:

```csharp
services.AddDiagnosticServices(configuration);
```

Это регистрирует:
- `IModbusDispatcher` → `ModbusDispatcher` (singleton)
- `IModbusClient` → `QueuedModbusClient` (singleton)
- `RegisterReader` (singleton)
- `RegisterWriter` (singleton)
- `PollingService` (singleton)

## Обработка ошибок

### При чтении/записи

```csharp
var result = await reader.ReadUInt16Async(0x1000);

if (!result.Success)
{
    // result.Error содержит сообщение об ошибке
    _logger.LogError("Ошибка чтения: {Error}", result.Error);
}
```

### При потере связи

Диспетчер автоматически переподключается с фиксированным интервалом 5 секунд. Подпишитесь на `Disconnecting` чтобы остановить polling:

```csharp
// В Form1.cs уже настроено:
dispatcher.Disconnecting += () => pollingService.StopAllTasksAsync();
```

## Потокобезопасность

| Компонент | Потокобезопасность |
|-----------|-------------------|
| `ModbusDispatcher` | Да (синхронизирован) |
| `QueuedModbusClient` | Да (синхронизирован) |
| `ModbusConnectionManager` | Нет (используется только воркером) |
| `RegisterReader` | Да (делегирует в client) |
| `RegisterWriter` | Да (делегирует в client) |
| `PollingService` | Да (синхронизирован) |
