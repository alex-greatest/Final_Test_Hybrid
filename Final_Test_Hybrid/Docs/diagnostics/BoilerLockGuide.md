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
- Ping cadence теперь profile-based:
  - active execution: `5000 мс`;
  - idle: `10000 мс`.
- Safety-контракт не меняется: `BoilerLock` продолжает питаться только валидным ping, а stale ping очищается при reconnect.

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
| `Diagnostic:BoilerLock:PlcSignalOnStatus2Enabled` | Включает ветку pause-only для `1005 == 2` (имя сохранено для совместимости) |
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
- `BoilerLock` не имеет собственного pause-token и делит ownership паузы с `ErrorCoordinator.CurrentInterrupt`.
- `AutoReady OFF` не должен перехватывать ownership уже активной `BoilerLock`-паузы: `AutoModeDisabled` не затирает interrupt, пока `BoilerLock` удерживает общий `PauseToken`.

## Ветка `1005 == 2` (BlockA pause-only)

Условия входа:

1. `BoilerLock.Enabled == true`.
2. `BoilerLock.PlcSignalOnStatus2Enabled == true`.
3. Активен test execution.
4. `BoilerStatus == 2`.
5. `LastErrorId` входит в whitelist.

Действие:

1. Поднимается отдельный interrupt `InterruptReason.BoilerBlockA`.
2. Execution ставится на паузу через общий `PauseToken`.
3. Автоматических действий с котлом нет:
   - без перевода в `Stand`;
   - без записи `1153=0`;
   - без auto-resume по изменению `1005`.
4. Ветка `status=2` сама не поднимает ошибку через `AssociatedError`, но `EcuErrorSyncService` может активировать ECU-ошибку по `1047` при lock-контексте.

Важно:

- Если уже активен другой interrupt (`PlcConnectionLost`, `TagTimeout`, `AutoModeDisabled`, `BoilerLock`), `status=2` не перехватывает ownership паузы.
- `AutoReady OFF` не перехватывает ownership уже активной `BoilerBlockA`-паузы.
- После входа в `BoilerBlockA` простой уход `1005` из значения `2` тест не продолжает.
- Снятие паузы допускается только через `ForceStop()` или `Reset()`.

## Очистка состояния и защита от вечной паузы

### Recovery-check на каждом ping

Если `CurrentInterrupt == BoilerLock`, но условие паузы больше не выполняется, вызывается `ForceStop()` и interrupt очищается.

Это предотвращает сценарий «условие ушло между опросами, а пауза осталась навсегда».

Ограничение текущего ownership:

- recovery-check срабатывает только пока `CurrentInterrupt == BoilerLock`;
- если ownership уже перехвачен другим interrupt, `BoilerLockRuntimeService` не снимает чужой interrupt и ждёт следующий ping-цикл для повторной оценки условия.
- Ветка `BoilerBlockA` в recovery-check не участвует: pause-only interrupt намеренно живёт до reset.

### Принудительная очистка

Состояние BoilerLock также очищается при:

- `IModbusDispatcher.Disconnecting`,
- `IModbusDispatcher.Stopped`,
- `ErrorCoordinator.OnReset`.

Для `BoilerBlockA` это означает:

- reset/runtime cleanup очищает внутренний latch ветки `status=2`;
- основной pause снимается существующими `ForceStop()` / `Reset()` через общий `PauseToken`.

## UI-сообщение

В `MessageService` добавлено правило:

- Приоритет `125`,
- Условие: `CurrentInterrupt == InterruptReason.BoilerLock`,
- Сообщение: `Блокировка котла. Ожидание восстановления`.

Очистка сообщения происходит автоматически после очистки interrupt:

`ForceStop() -> ClearCurrentInterrupt() -> OnInterruptChanged -> пересчёт MessageService`.

Для `BoilerBlockA`:

- Условие: `CurrentInterrupt == InterruptReason.BoilerBlockA`,
- Сообщение: `Блокировка А. Остановите тест`,
- Приоритет ниже `Нет связи с PLC` и `Нет автомата`,
- Очистка сообщения происходит сразу после `ForceStop()` или `Reset()`.

## Что не меняется

- Ping keep-alive продолжает работать (не паузится).
- Active ping cadence сохраняет остановку по `BoilerLock` в пределах одного активного ping-цикла.
- Остальные системные сервисы продолжают работать.
- `ActiveErrorsGrid` продолжает отображать данные из `ErrorService` без изменений UI-компонента.
- PLC reset flow и HardReset flow не объединяются и не переопределяются этой логикой.

## Ключевые точки кода

- `Services/Diagnostic/Services/BoilerLockRuntimeService.cs`
- `Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.cs`
- `Services/Steps/Infrastructure/Execution/ErrorCoordinator/Behaviors/BoilerLockBehavior.cs`
- `Services/Steps/Infrastructure/Execution/ErrorCoordinator/Behaviors/BoilerBlockABehavior.cs`
- `Services/Main/Messages/MessageService.cs`
- `appsettings.json` (`Diagnostic:BoilerLock:*`)
