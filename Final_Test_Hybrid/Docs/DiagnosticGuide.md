# Diagnostic Service Guide

Руководство по работе с диагностическим сервисом Modbus для связи с ЭБУ котла.

## Архитектура

```
                                    ┌─────────────────────────────────────┐
                                    │         Тестовые шаги               │
                                    │  (через TestStepContext)            │
                                    └──────────────┬──────────────────────┘
                                                   │
                                                   ▼
                              ┌─────────────────────────────────────────────┐
                              │  PausableRegisterReader/Writer              │
                              │  (блокируется при Auto OFF)                 │
                              └──────────────┬──────────────────────────────┘
                                             │
              ┌──────────────────────────────┼──────────────────────────────┐
              │                              │                              │
              ▼                              ▼                              ▼
┌─────────────────────┐      ┌─────────────────────┐      ┌─────────────────────┐
│  PollingService     │      │  RegisterReader/    │      │  Boiler*Service     │
│  (НЕ паузится)      │      │  Writer (базовые)   │      │  (НЕ паузится)      │
└─────────┬───────────┘      └─────────┬───────────┘      └─────────┬───────────┘
          │                            │                            │
          └────────────────────────────┼────────────────────────────┘
                                       │
                                       ▼
                          ┌─────────────────────────┐
                          │   QueuedModbusClient    │
                          └─────────────┬───────────┘
                                        │
                                        ▼
                          ┌─────────────────────────┐
                          │    ModbusDispatcher     │──► Ping Keep-Alive (НЕ паузится)
                          │        (фасад)          │
                          └─────────────┬───────────┘
                                        │
                                        ▼
                          ┌─────────────────────────┐
                          │ ModbusConnectionManager │
                          └─────────────┬───────────┘
                                        │
                                        ▼
                                   [SerialPort]
```

### Внутренняя структура ModbusDispatcher

```
┌─────────────────────────────────────────────────────────────┐
│                  ModbusDispatcher (фасад)                   │
│  - Публичный API (IModbusDispatcher)                        │
│  - Единый _stateLock для состояния                          │
│  - Владеет CTS/Task и соединением                           │
│  - CleanupWorkerStateIfNeeded                               │
└───────────────────────────┬─────────────────────────────────┘
                            │ creates & coordinates
         ┌──────────────────┼──────────────────┐
         │                  │                  │
         ▼                  ▼                  ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ModbusCommandQueue│ │ModbusWorkerLoop │ │ ModbusPingLoop  │
│ (пассивная)     │ │ (логика)        │ │ (генератор)     │
│                 │ │                 │ │                 │
│ - Хранит каналы │ │ - connect       │ │ - Периодический │
│ - Нет политики  │ │ - process cmds  │ │   ping          │
│                 │ │ - wait for cmd  │ │                 │
└─────────────────┘ └─────────────────┘ └─────────────────┘

Internal/CommunicationErrorHelper ← статический helper для ошибок связи
```

**Файловая структура:**
```
Services/Diagnostic/Protocol/CommandQueue/
├── IModbusDispatcher.cs              (публичный интерфейс)
├── ModbusDispatcher.cs               (фасад, ~500 строк)
├── ModbusDispatcherOptions.cs        (настройки)
├── ModbusConnectionManager.cs        (управление COM-портом)
│
└── Internal/                         (internal компоненты)
    ├── CommunicationErrorHelper.cs   (определение ошибок связи)
    ├── ModbusCommandQueue.cs         (пассивная очередь)
    ├── ModbusWorkerLoop.cs           (логика worker)
    └── ModbusPingLoop.cs             (генератор ping)
```

**Ключевые компоненты:**
- `IModbusDispatcher` — диспетчер команд с приоритетной очередью
- `IModbusClient` — клиент для чтения/записи регистров
- `RegisterReader` / `RegisterWriter` — высокоуровневые операции (системные, НЕ паузятся)
- `PausableRegisterReader` / `PausableRegisterWriter` — операции для тестовых шагов (паузятся)
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

**ВАЖНО:** `StartAsync()` вызывается из тестовых шагов, не из Form1.

```csharp
// Form1.cs — только подписка на события, БЕЗ StartAsync
private static void ConfigureDiagnosticEvents(ServiceProvider serviceProvider)
{
    var dispatcher = serviceProvider.GetRequiredService<IModbusDispatcher>();
    var pollingService = serviceProvider.GetRequiredService<PollingService>();
    var plcResetCoordinator = serviceProvider.GetRequiredService<PlcResetCoordinator>();
    var errorCoordinator = serviceProvider.GetRequiredService<IErrorCoordinator>();
    var logger = serviceProvider.GetRequiredService<ILogger<Form1>>();

    dispatcher.Disconnecting += () => pollingService.StopAllTasksAsync();
    plcResetCoordinator.OnForceStop += () => StopDispatcherSafely(dispatcher, logger);
    errorCoordinator.OnReset += () => StopDispatcherSafely(dispatcher, logger);

    // StartAsync() НЕ вызываем — диагностика запускается из тестовых шагов
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

См. `Components/Main/BoilerStatusDisplay.razor` — использует `IsConnected`, `IsReconnecting`, `LastPingData`.

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

1. Завершает каналы команд (TryComplete)
2. Отменяет CancellationToken (ping и worker)
3. **Закрывает COM-порт СРАЗУ** — прерывает in-flight Modbus команду
4. Уведомляет подписчиков Disconnecting (таймаут 2 сек)
5. Ждёт завершения ping + worker параллельно (таймаут 5 сек)
6. **Environment.FailFast** если tasks не завершились
7. Отменяет pending команды, сбрасывает состояние

**Защита от зависания:**

| Сценарий | Поведение |
|----------|-----------|
| Worker завершился < 5 сек | Рестарт разрешён |
| Worker таймаут > 5 сек | **Environment.FailFast** — критический баг |
| Disconnecting handler таймаут > 2 сек | Логируется Warning, продолжаем |
| Текущая команда выполняется | Прерывается через Close() → IOException |

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
private static void StopDispatcherSafely(IModbusDispatcher dispatcher, ILogger logger)
{
    _ = dispatcher.StopAsync().ContinueWith(t =>
    {
        if (t is { IsFaulted: true, Exception: not null })
        {
            logger.LogError(t.Exception.GetBaseException(),
                "Ошибка остановки диспетчера: {Error}",
                t.Exception.GetBaseException().Message);
        }
    }, TaskScheduler.Default);
}
```

После reset пользователь может вручную вызвать `StartAsync()` для восстановления связи.

## Ping Keep-Alive

Диспетчер периодически отправляет ping-команду для:
1. Проверки связи при отсутствии пользовательских команд
2. Чтения полезных данных (ModeKey, BoilerStatus, LastErrorId)

### Данные Ping

| Параметр | Адрес (док) | Modbus | Тип | Описание |
|----------|-------------|--------|-----|----------|
| ModeKey | 1000-1001 | 999-1000 | uint32 | Ключ режима |
| BoilerStatus | 1005 | 1004 | int16 | Статус котла |
| LastErrorId | 1047 | 1046 | uint16 | ID последней ошибки (soft-fail) |

### Реализация PingCommand

**Файл:** `Services/Diagnostic/Protocol/CommandQueue/PingCommand.cs`

#### Алгоритм чтения

1. **Основное чтение (критичное)** — 6 регистров одним запросом:
   - При ошибке — весь ping падает (это корректное поведение)

2. **Soft-fail чтения** — отдельные запросы:
   - LastErrorId — если не читается, возвращает `null`
   - ChTemperature — если не читается, возвращает `null`
   - Не прерывают основной ping

#### Карта регистров

| Параметр | Адрес (док.) | Modbus* | Размер | Тип | Описание |
|----------|--------------|---------|--------|-----|----------|
| ModeKey | 1000-1001 | 999-1000 | 2 рег. | uint32 BE | Ключ режима работы |
| Reserved | 1002-1004 | 1001-1003 | 3 рег. | — | Зарезервировано |
| BoilerStatus | 1005 | 1004 | 1 рег. | int16 | Статус котла |
| LastErrorId | 1047 | 1046 | 1 рег. | uint16? | ID ошибки (soft-fail) |
| ChTemperature | 1006 | 1005 | 1 рег. | int16? | Температура CH (soft-fail) |

*Modbus адрес = Документация - BaseAddressOffset (обычно 1)

#### Формирование ModeKey (Big Endian)

```csharp
var modeKey = ((uint)registers[0] << 16) | registers[1];
// registers[0] = старшие 16 бит
// registers[1] = младшие 16 бит
```

#### Soft-fail паттерн

```csharp
private ushort? ReadLastErrorSoftFail(IModbusMaster master, byte slaveId, CancellationToken ct)
{
    try
    {
        ct.ThrowIfCancellationRequested();
        var errorRegisters = master.ReadHoldingRegisters(slaveId, errorAddress, 1);
        return errorRegisters[0];
    }
    catch (OperationCanceledException) { throw; }  // Отмену пробрасываем
    catch { return null; }  // Остальные ошибки — soft-fail
}
```

**Почему soft-fail:**
- Регистр ошибки может быть недоступен в некоторых версиях прошивки
- Температура CH опциональна
- Основная функция ping (keep-alive) не должна страдать

**Значения ModeKey:**
- `0xD7F8DB56` — стендовый режим
- `0xFA87CD5E` — инженерный режим
- Другое — обычный режим

**Значения BoilerStatus:**
- `-1` — тест
- `0` — включение
- `1-10` — различные режимы работы

**Значения LastErrorId:**
- `null` — не удалось прочитать регистр (soft-fail)
- `0` — ошибок нет
- `1-25` — код ошибки ЭБУ

### Доступ к данным ping

```csharp
var pingData = dispatcher.LastPingData;
if (pingData != null)
{
    var modeKey = pingData.ModeKey;
    var status = pingData.BoilerStatus;
    var errorId = pingData.LastErrorId;
}
```

## Ошибки ЭБУ

### EcuErrorSyncService

Сервис `EcuErrorSyncService` автоматически синхронизирует ошибки ЭБУ с `ErrorService`:

- Подписывается на `PingDataUpdated` события диспетчера
- При изменении `LastErrorId` автоматически взводит/сбрасывает соответствующую ошибку
- При disconnect сбрасывает внутреннее состояние (ошибка остаётся в ErrorService)

### Контракт LastErrorId

| Значение | Поведение |
|----------|-----------|
| `null` | Soft-fail чтения — состояние не меняется |
| `0` | Ошибок нет — сбрасывает предыдущую ошибку если была |
| `1-25` | Код ошибки — взводит соответствующую ошибку, сбрасывает предыдущую |

### Потокобезопасность

`EcuErrorSyncService` использует `lock` для защиты внутреннего состояния:

```csharp
lock (_lock)
{
    if (newErrorId == _currentErrorId) return;
    // ... обработка изменения ошибки
    _currentErrorId = newErrorId;
}
```

### Поведение при disconnect

При разрыве связи (`Disconnecting` event):
1. Сбрасывается внутреннее `_currentErrorId = 0`
2. Ошибка в `ErrorService` **остаётся** как "последняя известная"
3. При восстановлении связи та же ошибка снова поднимется (если ещё активна)

### Список ошибок ЭБУ

Ошибки определены в `ErrorDefinitions.DiagnosticEcu.cs` (ID 1-25):

| ID | Код | Описание | Причина / Что проверить |
|----|-----|----------|-------------------------|
| 1 | E9 | Блокировка при перегреве | Термостат сработал, проверить циркуляцию воды и насос |
| 2 | EA | Блокировка зажигания | Нет искры или газа, проверить электрод и газовый клапан |
| 3 | E2 | Неисправность датчика температуры подающей линии | Обрыв/КЗ датчика NTC, проверить проводку |
| 4 | A7 | Неисправность датчика температуры ГВС | Обрыв/КЗ датчика NTC ГВС |
| 5 | Ad | Неисправность датчика температуры бойлера косвенного нагрева | Обрыв/КЗ датчика бойлера косвенного нагрева |
| 6 | A8 | Неисправен датчик наружной температуры | Обрыв/КЗ уличного датчика (если подключен) |
| 7 | C7 | Не обнаружен тахосигнал с вентилятора | Вентилятор не крутится или неисправен датчик Холла |
| 8 | C6 | Пневматический выключатель не закрывается | Прессостат залип в открытом положении |
| 9 | C4 | Пневматический выключатель закрыт до начала нагрева | Прессостат замкнут когда не должен — проверить трубку |
| 10 | C1 | Вентилятор не может достичь заданных оборотов | Загрязнение, износ подшипников или проблема питания |
| 11 | FA | Неисправность клапанов регулятора давления газа | Клапан не открывается/закрывается, проверить катушки |
| 12 | D7 | Неисправность модулирующей катушки регулятора давления газа | Обрыв катушки модуляции газа |
| 13 | FL | Неисправность датчика контроля пламени | Ионизационный электрод загрязнён или неисправен |
| 14 | CE | Низкое давление воды в системе | Подпитать систему, проверить на утечки |
| 15 | CA | Высокое давление воды в системе | Расширительный бак или предохранительный клапан |
| 16 | P | Не задан тип котла | Требуется настройка параметров котла |
| 17 | 11 | Не задана ступень вентилятора | Требуется калибровка вентилятора |
| 18 | FD | Залипание кнопок | Кнопка на панели управления зажата |
| 19 | LA | Не достигается температура для термической дезинфекции | Проблема с нагревом бойлера (антилегионелла) |
| 20 | PE | Ошибка работы насоса (электропитание) | Проверить питание насоса и предохранители |
| 21 | Pd | Ошибка работы насоса (отсутствие жидкости) | Сухой ход — нет воды в системе |
| 22 | PA | Ошибка работы насоса (блокировка ротора) | Насос заклинило, проверить крыльчатку |
| 23 | F7 | Неисправность катушек клапанов регулятора давления газа | Обрыв катушки газового клапана |
| 24 | A9 | Невозможность нагрева бойлера из режима защиты от замерзания | Бойлер не греется при антифризе |
| 25 | IE | Внутренняя ошибка ЭБУ | Сбой платы управления, может требоваться замена |

Хелпер для получения ошибки по ID:

```csharp
var error = ErrorDefinitions.GetEcuErrorById(errorId);
if (error != null)
{
    _errorService.Raise(error);
}
```

## Pausable vs Non-Pausable (Modbus)

Диагностика **является частью тестовых шагов** — операции чтения/записи регистров выполняются внутри шагов теста.

| Компонент | Поддержка паузы | Причина |
|-----------|-----------------|---------|
| `PausableRegisterReader/Writer` | **Да** | Для тестовых шагов (через `TestStepContext`) |
| `RegisterReader/Writer` | **Нет** | Системные операции (polling, Boiler*Service) |
| Ping keep-alive | **Нет** | Поддержание связи независимо от паузы |

**Ping работает непрерывно потому что:**
- Поддерживает связь с ЭБУ даже при паузе теста
- Предотвращает таймаут соединения
- Обновляет LastPingData (ModeKey, BoilerStatus)

### Специальный контракт `CheckCommsStep`

- Для шага `Coms/Check_Comms` (`CheckCommsStep`) при `AutoReady = false` применяется fail-fast по результату шага: `TestStepResult.Fail(...NoDiagnosticConnection...)` формируется сразу.
- В этом шаге используется wall-clock ожидание (`Task.Delay`), а не pause-aware `context.DelayAsync`, чтобы timeout не «замораживался» на паузе автомата.
- При неуспешном завершении `CheckCommsStep` диспетчер диагностики должен быть остановлен через `StopAsync`, чтобы не оставлять бесконечный reconnect в фоне.
- Показ Retry/Skip-диалога при `AutoReady OFF` может быть отложен до восстановления автомата (`AutoReady = true`); это штатное поведение.
- Повтор шага (`Retry`) имеет смысл только после восстановления `AutoReady`.

### Использование в тестовых шагах

```csharp
public class DiagnosticReadStep : ITestStep
{
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        // Пауза автоматически применится при Auto OFF
        var result = await context.DiagReader.ReadUInt16Async(1005, ct);

        if (!result.Success)
        {
            context.Logger.LogError("Ошибка: {Error}", result.Error);
            return TestStepResult.Fail(result.Error);
        }

        context.Logger.LogInformation("Значение: {Value}", result.Value);
        return TestStepResult.Pass();
    }
}
```

### Поведение паузы

```
TestStep вызывает context.DiagReader.ReadUInt16Async(address, ct)
                            │
                            ▼
        ┌─────────────────────────────────────┐
        │ await pauseToken.WaitWhilePausedAsync(ct) │  ← Блокируется при Auto OFF
        └─────────────────────────────────────┘
                            │
                            ▼ (только после Resume)
        ┌─────────────────────────────────────┐
        │ await inner.ReadUInt16Async(address, ct)  │  ← Реальное чтение
        └─────────────────────────────────────┘
```

| Момент паузы | Поведение |
|--------------|-----------|
| ДО вызова `ReadUInt16Async` | Следующий вызов заблокируется |
| ВО ВРЕМЯ `WaitWhilePausedAsync` | Ждёт Resume, потом читает |
| ПОСЛЕ `WaitWhilePausedAsync` (в Modbus) | Операция завершится (по дизайну) |

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
    "SlaveId": 1,
    "ReadTimeoutMs": 1000,
    "WriteTimeoutMs": 1000,
    "BaseAddressOffset": 1,
    "CommandQueue": {
      "ReconnectDelayMs": 5000,
      "PingIntervalMs": 5000
    }
  }
}
```

### DiagnosticSettings — COM-порт и Modbus RTU

| Параметр | Тип | Default | Описание |
|----------|-----|---------|----------|
| `PortName` | string | "COM1" | Имя COM-порта |
| `BaudRate` | int | 115200 | Скорость (должна совпадать с ЭБУ) |
| `SlaveId` | byte | 1 | Modbus Slave ID |
| `ReadTimeoutMs` | int | 1000 | Таймаут чтения (мс) |
| `WriteTimeoutMs` | int | 1000 | Таймаут записи (мс) |
| `BaseAddressOffset` | ushort | 1 | Смещение: документация → Modbus |

Остальные настройки: DataBits=8, Parity=None, StopBits=One (стандарт Modbus RTU).

### ModbusDispatcherOptions — Command Queue

| Параметр | Default | Описание |
|----------|---------|----------|
| `ReconnectDelayMs` | 5000 | Интервал переподключения (мс) |
| `PingIntervalMs` | 5000 | Интервал ping keep-alive (мс) |

Остальные настройки: HighPriorityQueueCapacity=100, LowPriorityQueueCapacity=10, CommandWaitTimeoutMs=100.

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

### Обработка исключений по слоям

| Слой | Исключение | Результат |
|------|------------|-----------|
| `IModbusClient` | Пробрасывается | — |
| `RegisterReader` | Ловится | `DiagnosticReadResult.Fail(error)` |
| `RegisterWriter` | Ловится | `DiagnosticWriteResult.Fail(error)` |
| `OperationCanceledException` | Пробрасывается на всех слоях | — |

### При потере связи

Диспетчер автоматически переподключается:

1. Команда таймаутит (1 сек) → исключение ловится на уровне слоя:
   - `IModbusClient` → исключение пробрасывается
   - `RegisterReader/Writer` → возвращает `Fail` с `Error = ex.Message`
2. Диспетчер ловит, логирует "Ошибка связи: ... Переподключение..."
3. Закрывает порт, уведомляет подписчиков `Disconnecting`
4. Ждёт 5 сек → пытается переподключиться
5. При успехе первой команды → `Connected` event

Подпишитесь на `Disconnecting` чтобы остановить polling (в Form1.cs уже настроено).

## Потокобезопасность

| Компонент | Потокобезопасность |
|-----------|-------------------|
| `ModbusDispatcher` | Да (единый `_stateLock` для lifecycle) |
| `QueuedModbusClient` | Да (синхронизирован) |
| `ModbusConnectionManager` | Нет (владеет фасад, используется воркером) |
| `RegisterReader` | Да (делегирует в client) |
| `RegisterWriter` | Да (делегирует в client) |
| `PollingService` | Да (синхронизирован) |

**Internal компоненты (не для внешнего использования):**

| Компонент | Потокобезопасность |
|-----------|-------------------|
| `ModbusCommandQueue` | Частично (Channel потокобезопасен) |
| `ModbusWorkerLoop` | Нет (один экземпляр на worker task) |
| `ModbusPingLoop` | Нет (один экземпляр на ping task) |
| `CommunicationErrorHelper` | Да (статический, без состояния) |

## Внутренние защиты

### Инварианты ModbusDispatcher

| Инвариант | Реализация |
|-----------|------------|
| Единственный владелец connect/close | Фасад через `DoConnect`/`DoClose` колбэки |
| Очистка при "воркер умер сам" | `CleanupWorkerStateIfNeeded` в фасаде |
| Порядок Stop | CompleteChannels → Cancel CTS → Close port → CancelAllPendingCommands → wait tasks → cleanup |
| Защита от параллельных Disconnecting | Interlocked gate `_isNotifyingDisconnect` |
| Race protection ping при Stop | `isStopping` + `isPortOpen` проверки |

### Stack Overflow Prevention

NModbus по умолчанию использует внутренний retry при таймаутах, что может вызвать stack overflow.
Решение: `master.Transport.Retries = 0` в `ModbusConnectionManager.Connect()`.

### Exception Propagation

Исключения из команд пробрасываются через `throw;` в `ModbusCommandBase.ExecuteAsync`,
чтобы диспетчер корректно определял разрыв связи и запускал переподключение.
