# Change: Manual Start/Stop + Ping Keep-Alive + PLC Reset Integration

## Why

Текущая реализация имеет проблемы:
1. **Автостарт** — диспетчер запускается автоматически при первом read/write, нет контроля
2. **Ложное подключение** — `IsConnected=true` сразу после открытия порта, хотя котёл может не отвечать
3. **Нет рестарта** — после `StopAsync()` диспетчер нельзя перезапустить
4. **Нет интеграции с PLC Reset** — при сбросе PLC диагностика должна отключаться
5. **Нет keep-alive** — если нет команд, не знаем что связь потеряна

## What Changes

- **BREAKING**: Убрать автостарт из `QueuedModbusClient` — требовать явный `StartAsync()`
- **IsConnected logic** — `true` только после первой успешной команды (ping или user)
- **Ping keep-alive** — периодическая low-priority команда, читает ModeKey + BoilerStatus → `DiagnosticPingData`
- **LastPingData property** — dispatcher экспортирует последние данные ping для UI
- **Restart support** — пересоздаём каналы при `StartAsync()`, разрешаем рестарт после `StopAsync()`
- **Queue clearing** — при `StopAsync()` отменяем все pending команды
- **PLC Reset integration** — подписка на `OnForceStop` и `OnReset` → вызов `StopAsync()`
- **Simplified reconnect** — фиксированный интервал 5 сек вместо exponential backoff

## Connection State Model

```
StartAsync()
    ↓
Port opens → IsConnected = false
    ↓
Ping (low priority) → queue
    ↓
User command (high priority)? → executes first
    ↓
First successful command → IsConnected = true
    ↓
Any timeout → IsConnected = false → reconnect (5 sec interval)
    ↓
Ping continues periodically (keep-alive)
```

## Impact

- Affected specs: `diagnostic-service` (новая спека)
- Affected code:
  - `Services/Diagnostic/Protocol/QueuedModbusClient.cs` — убрать автостарт, добавить ThrowIfNotStarted
  - `Services/Diagnostic/Protocol/CommandQueue/ModbusDispatcher.cs` — IsConnected logic, restart, ping keep-alive task
  - `Services/Diagnostic/Protocol/CommandQueue/ModbusDispatcherOptions.cs` — добавить `PingIntervalMs`
  - `Services/Diagnostic/RegisterReader.cs` — обернуть InvalidOperationException в DiagnosticReadResult.Fail
  - `Form1.cs` — явный вызов `StartAsync()` при инициализации, подписка на PLC reset события
  - `Docs/diagnostics/DiagnosticGuide.md` — обновить документацию

## Integration with PLC Reset

```
PlcResetCoordinator.HandleResetAsync()
    ↓
PlcResetCoordinator.OnForceStop (soft reset)
ErrorCoordinator.OnReset (hard reset)
    ↓
DiagnosticService subscriber:
    await dispatcher.StopAsync()  // Disconnect + clear queues
```

| PLC Reset Type | Event Source | Diagnostic Action |
|----------------|--------------|-------------------|
| Soft (ForceStop) | `PlcResetCoordinator.OnForceStop` | `StopAsync()` |
| Hard (Reset) | `ErrorCoordinator.OnReset` | `StopAsync()` |

После reset пользователь может вручную вызвать `StartAsync()` для восстановления связи.

## Ping Data Model

Ping читает полезные данные для UI (не просто проверка связи):

| Параметр | Адрес (док) | Modbus | Тип | Описание |
|----------|-------------|--------|-----|----------|
| ModeKey | 1000-1001 | 999-1000 | uint32 | Ключ режима (стенд/инженерный/обычный) |
| BoilerStatus | 1005 | 1004 | int16 | Статус котла (-1..10) |

```csharp
/// <summary>
/// Данные ping-опроса. Расширяемая структура для будущих параметров.
/// </summary>
public record DiagnosticPingData
{
    /// <summary>Ключ режима: стенд (0xD7F8DB56), инженерный (0xFA87CD5E), обычный (иное).</summary>
    public uint ModeKey { get; init; }

    /// <summary>Статус котла: -1 тест, 0 включение, 1-10 различные режимы.</summary>
    public short BoilerStatus { get; init; }

    // Будущие поля добавляются здесь
}
```

## Notes

- Ping читает 6 регистров: 999-1004 (ModeKey + reserved + BoilerStatus)
- Ping интервал: настраиваемый (например, 5 секунд)
- Каналы пересоздаются при каждом `StartAsync()` — гарантированно пустая очередь
- `IsConnected = true` только после успешной команды, не после открытия порта
