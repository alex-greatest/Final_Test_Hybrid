# Diagnostic Service Guide

Руководство по работе с диагностическим сервисом Modbus для связи с ЭБУ котла.

## Архитектура

```
[UI/Services] ──► [RegisterReader/Writer] ──► [QueuedModbusClient] ──► [ModbusDispatcher] ──► [SerialPort]
                                                      │                        │
[PollingService] ──► [PollingTask] ─────────────────────┘                        │
  (Low priority)                                                          State events
                                                                    (Connected, Disconnecting)
```

**Ключевые компоненты:**
- `IModbusDispatcher` — диспетчер команд с приоритетной очередью
- `IModbusClient` — клиент для чтения/записи регистров
- `RegisterReader` / `RegisterWriter` — высокоуровневые операции с типизацией
- `PollingService` / `PollingTask` — периодический опрос регистров

## Установка связи

Связь устанавливается **автоматически** при первом обращении к `IModbusClient`:

```csharp
public class MyService(RegisterReader reader)
{
    public async Task DoSomethingAsync()
    {
        // Диспетчер автоматически стартует и подключается
        var result = await reader.ReadUInt16Async(0x1000);

        if (result.Success)
        {
            Console.WriteLine($"Значение: {result.Value}");
        }
    }
}
```

### Ручной старт (опционально)

```csharp
public class MyService(IModbusDispatcher dispatcher)
{
    public async Task StartConnectionAsync()
    {
        await dispatcher.StartAsync();
        // Диспетчер начнёт подключение
    }
}
```

## Разрыв связи

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

**Важно:** После `StopAsync()` диспетчер нельзя перезапустить — он предназначен для однократного использования в течение жизни приложения.

## Индикация состояния связи

### Свойства

```csharp
public class MyService(IModbusDispatcher dispatcher)
{
    public void CheckStatus()
    {
        // Запущен ли диспетчер
        bool isStarted = dispatcher.IsStarted;

        // Есть ли активное соединение
        bool isConnected = dispatcher.IsConnected;

        // Идёт ли переподключение
        bool isReconnecting = dispatcher.IsReconnecting;
    }
}
```

### События

```csharp
public class MyService(IModbusDispatcher dispatcher)
{
    public void SubscribeToEvents()
    {
        // Соединение установлено
        dispatcher.Connected += OnConnected;

        // Соединение разрывается (вызывается ДО отключения)
        dispatcher.Disconnecting += OnDisconnectingAsync;
    }

    private void OnConnected()
    {
        Console.WriteLine("Подключено к ЭБУ");
    }

    private Task OnDisconnectingAsync()
    {
        Console.WriteLine("Отключение...");
        return Task.CompletedTask;
    }
}
```

### Использование в UI (Blazor)

```csharp
@inject IModbusDispatcher Dispatcher

<div class="status">
    @if (Dispatcher.IsConnected)
    {
        <span class="connected">Подключено</span>
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

        // Чтение нескольких регистров
        var multiResult = await reader.ReadUInt16ArrayAsync(0x1000, count: 10, ct: ct);
        if (multiResult.Success)
        {
            ushort[] values = multiResult.Value;
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

        // Запись нескольких регистров
        var multiResult = await writer.WriteUInt16ArrayAsync(
            0x2000,
            new ushort[] { 1, 2, 3 },
            ct);
    }
}
```

### Через IModbusClient (низкоуровневый)

```csharp
public class MyService(IModbusClient client)
{
    public async Task WriteRawAsync(CancellationToken ct)
    {
        // Запись одного регистра
        await client.WriteSingleRegisterAsync(0x2000, 42, ct: ct);

        // Запись нескольких регистров
        await client.WriteMultipleRegistersAsync(
            0x2000,
            new ushort[] { 1, 2, 3 },
            ct: ct);
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

### Проверка статуса опроса

```csharp
public class MyService(PollingService pollingService)
{
    public void CheckPollingStatus()
    {
        bool isRunning = pollingService.IsPollingRunning("StatusPolling");
    }
}
```

## Приоритеты команд

| Приоритет | Использование |
|-----------|---------------|
| `CommandPriority.High` | One-off операции (чтение/запись по запросу пользователя) |
| `CommandPriority.Low` | Фоновый polling |

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
      "InitialReconnectDelayMs": 1000,
      "MaxReconnectDelayMs": 30000,
      "ReconnectBackoffMultiplier": 2.0,
      "CommandWaitTimeoutMs": 100
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
| `LowPriorityQueueCapacity` | int | 10 | Размер очереди для polling (фоновый опрос) |
| `InitialReconnectDelayMs` | int | 1000 | Начальная задержка при потере связи |
| `MaxReconnectDelayMs` | int | 30000 | Максимальная задержка (exponential backoff ceiling) |
| `ReconnectBackoffMultiplier` | double | 2.0 | Множитель: 1с → 2с → 4с → 8с → ... → 30с |
| `CommandWaitTimeoutMs` | int | 100 | Как долго воркер ждёт команду перед проверкой состояния |

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

Диспетчер автоматически переподключается с экспоненциальным backoff. Подпишитесь на `Disconnecting` чтобы остановить polling:

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
