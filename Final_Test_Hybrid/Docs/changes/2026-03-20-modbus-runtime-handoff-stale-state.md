# 2026-03-20 modbus runtime handoff stale state

## Failure mode

- При открытой ручной панели `Тест связи` шаг `Coms/Check_Comms` мог принять старый `LastPingData` как подтверждение живой связи и пройти без нового ping уже в ownership runtime.
- При fail-path `NoDiagnosticConnection` shared dispatcher мог остаться запущенным после reuse/shared-session и продолжать reconnect/ping в фоне.
- При неожиданной остановке worker'а `EcuErrorSyncService` не очищал ECU-состояние по `Stopped`, поэтому активная ECU-ошибка могла залипнуть до следующего валидного ping/disconnect.

## Root cause

- `CheckCommsStep` смотрел только на факт наличия `dispatcher.LastPingData`, не различая свежий runtime ping и stale данные ручной панели.
- Fail-path шага не гарантировал `StopAsync()` для всех неуспешных выходов shared-session сценария.
- `EcuErrorSyncService` очищал состояние только на `Disconnecting`, но не на `Stopped`.

## Resolution

- `CheckCommsStep` после захвата runtime-lease требует новый ping уже в ownership runtime и не принимает прежний `LastPingData` как успешную проверку.
- Любой fail-path `NoDiagnosticConnection` теперь сначала пытается остановить dispatcher, чтобы не оставлять background reconnect/ping при reuse/shared-session.
- `EcuErrorSyncService` очищает активную ECU-ошибку и внутреннее состояние и на `Disconnecting`, и на `Stopped`.
- `ConnectionTestPanel` normal-mode reset теперь возвращает реальный результат `AccessLevelManager.ResetToNormalModeAsync()`, а не unconditional success.

## Verification

- Добавлены регрессии на:
  - stale panel ping -> требуется свежий runtime ping;
  - `AutoReady=false` при уже запущенном shared dispatcher -> `NoDiagnosticConnection` + `StopAsync()`;
  - очистку ECU-состояния на `IModbusDispatcher.Stopped`;
  - возврат реального результата `ResetToNormalModeAsync()`.

## Notes

- `ReadSoftCodePlug` для кода `1054` возвращён к операторской диагностике по котлу/жгуту и не деградирует до generic boiler wording.
