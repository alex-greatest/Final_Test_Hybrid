# BoilerLock Guide

Документ описывает runtime-логику блокировки котла, которая работает поверх ping-опроса диагностики.

## Назначение

Логика нужна для реакции на выбранные ошибки ЭБУ (из `111.txt`) с учётом статуса котла (`регистр 1005`) без остановки ping и системных сервисов.

## Область действия

- Входные данные: `IModbusDispatcher.PingDataUpdated`.
- Используются поля ping:
  - `BoilerStatus` (`1005`, `int16`).
  - `LastErrorId` (`1047`, `uint16`).
- Логика активна только при `ExecutionActivityTracker.IsTestExecutionActive == true`.
- Подъём ECU-ошибки в `ErrorService` выполняется отдельным сервисом `EcuErrorSyncService` в lock-контексте.

## Конфигурация

`appsettings.json`:

```json
{
  "Diagnostic": {
    "WriteVerifyDelayMs": 300,
    "BoilerLock": {
      "Enabled": false,
      "PauseOnStatus1Enabled": false,
      "PlcSignalOnStatus2Enabled": false,
      "ResetFlow": {
        "RequireStandForReset": true,
        "ModeSwitchRetryMax": 2,
        "ResetRetryMax": 3,
        "RetryDelayMs": 250,
        "AttemptCooldownMs": 1000,
        "ErrorSuppressMs": 5000
      }
    }
  }
}
```

| Параметр | Назначение |
|----------|------------|
| `Diagnostic:BoilerLock:Enabled` | Master-флаг всей логики |
| `Diagnostic:BoilerLock:PauseOnStatus1Enabled` | Включает ветку паузы для `1005 == 1` |
| `Diagnostic:BoilerLock:PlcSignalOnStatus2Enabled` | Включает ветку PLC-сигнала для `1005 == 2` |
| `Diagnostic:WriteVerifyDelayMs` | Таймаут ожидания перед попыткой `1153=0` |
| `Diagnostic:BoilerLock:ResetFlow:RequireStandForReset` | Требовать подтверждённый `ModeKey=Stand` перед `1153=0` |
| `Diagnostic:BoilerLock:ResetFlow:ModeSwitchRetryMax` | Максимум retry-попыток перевода в `Stand` за цикл |
| `Diagnostic:BoilerLock:ResetFlow:ResetRetryMax` | Максимум retry-попыток записи `1153=0` за цикл |
| `Diagnostic:BoilerLock:ResetFlow:RetryDelayMs` | Пауза между retry-попытками |
| `Diagnostic:BoilerLock:ResetFlow:AttemptCooldownMs` | Минимальный интервал между циклами попыток |
| `Diagnostic:BoilerLock:ResetFlow:ErrorSuppressMs` | Окно suppress после исчерпания retry |

## Ошибки, которые участвуют в логике

Используются только ID из `Final_Test_Hybrid/111.txt`:

`1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 18, 23, 26`

Если `LastErrorId` не входит в этот whitelist, BoilerLock-логика не выполняется.

## Ветка `1005 == 1` (пауза)

Условия входа:

1. `BoilerLock.Enabled == true`.
2. `BoilerLock.PauseOnStatus1Enabled == true`.
3. Активен test execution (`IsTestExecutionActive == true`).
4. `BoilerStatus == 1`.
5. `LastErrorId` входит в whitelist.

Алгоритм:

1. Ставится interrupt `InterruptReason.BoilerLock` (поведение: pause).
2. Если `ResetFlow.RequireStandForReset=true` и `ModeKey != Stand`, выполняется перевод в `Stand` через `AccessLevelManager` + верификация `ModeKey` чтением.
3. Ждём `Diagnostic.WriteVerifyDelayMs`.
4. Пишем `1153 = 0`.
5. Повторно читаем `1005`.
6. Если `1005 != 1`, снимаем `BoilerLock` через `ForceStop()`.
7. Если `1005 == 1`, пауза остаётся до следующего ping-цикла.
8. При ошибках записи/верификации используются bounded retry + cooldown + suppress (без бесконечного шторма попыток).

Важно:

- Попытка `1153=0` выполняется только если текущий interrupt действительно `BoilerLock`.
- При активном другом interrupt (`TagTimeout`, `AutoModeDisabled`, и т.д.) ветка записи не исполняется.
- В лог пишутся оба адреса: `Doc=1153` и `Modbus=1152` (при `BaseAddressOffset=1`), чтобы не путать диагностику.

## Ветка `1005 == 2` (PLC signal stub)

Условия входа:

1. `BoilerLock.Enabled == true`.
2. `BoilerLock.PlcSignalOnStatus2Enabled == true`.
3. Активен test execution.
4. `BoilerStatus == 2`.
5. `LastErrorId` входит в whitelist.

Действие:

- Отправляется локальный `stub` (лог + TODO) для PLC-сигнала.
- Пауза не ставится.
- Ветка `status=2` сама не поднимает ошибку, но `EcuErrorSyncService` может активировать ECU-ошибку по `1047` при lock-контексте.

## Очистка состояния и защита от вечной паузы

### Recovery-check на каждом ping

Если `CurrentInterrupt == BoilerLock`, но условие паузы больше не выполняется, вызывается `ForceStop()` и interrupt очищается.

Это предотвращает сценарий «условие ушло между опросами, а пауза осталась навсегда».

### Принудительная очистка

Состояние BoilerLock также очищается при:

- `IModbusDispatcher.Disconnecting`,
- `IModbusDispatcher.Stopped`,
- `ErrorCoordinator.OnReset`.

## UI-сообщение

В `MessageService` добавлено правило:

- Приоритет `125`,
- Условие: `CurrentInterrupt == InterruptReason.BoilerLock`,
- Сообщение: `Блокировка котла. Ожидание восстановления`.

Очистка сообщения происходит автоматически после очистки interrupt:

`ForceStop() -> ClearCurrentInterrupt() -> OnInterruptChanged -> пересчёт MessageService`.

## Что не меняется

- Ping keep-alive продолжает работать (не паузится).
- Остальные системные сервисы продолжают работать.
- `ActiveErrorsGrid` продолжает отображать данные из `ErrorService` без изменений UI-компонента.
- PLC reset flow и HardReset flow не объединяются и не переопределяются этой логикой.

## Ключевые точки кода

- `Services/Diagnostic/Services/BoilerLockRuntimeService.cs`
- `Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.cs`
- `Services/Steps/Infrastructure/Execution/ErrorCoordinator/Behaviors/BoilerLockBehavior.cs`
- `Services/Main/Messages/MessageService.cs`
- `appsettings.json` (`Diagnostic:BoilerLock:*`)
