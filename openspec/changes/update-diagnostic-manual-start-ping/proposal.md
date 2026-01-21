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
- **Ping keep-alive** — периодическая low-priority команда для обнаружения потери связи
- **Restart support** — пересоздаём каналы при `StartAsync()`, разрешаем рестарт после `StopAsync()`
- **Queue clearing** — при `StopAsync()` отменяем все pending команды
- **PLC Reset integration** — подписка на `OnForceStop` и `OnReset` → вызов `StopAsync()`

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
Any timeout → IsConnected = false → reconnect loop
    ↓
Ping continues periodically (keep-alive)
```

## Impact

- Affected specs: `diagnostic-service` (новая спека)
- Affected code:
  - `Services/Diagnostic/Protocol/QueuedModbusClient.cs` — убрать автостарт
  - `Services/Diagnostic/Protocol/CommandQueue/ModbusDispatcher.cs` — IsConnected logic, restart
  - `Services/Diagnostic/Polling/PollingService.cs` — добавить ping task
  - `Form1.cs` или dedicated service — подписка на PLC reset события
  - `Docs/DiagnosticGuide.md` — обновить документацию

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

## Notes

- Ping адрес: `1055 - BaseAddressOffset` (Firmware Major)
- Ping интервал: настраиваемый (например, 5 секунд)
- Каналы пересоздаются при каждом `StartAsync()` — гарантированно пустая очередь
- `IsConnected = true` только после успешной команды, не после открытия порта
