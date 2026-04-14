# 2026-03-30 operation mode 1036 retention

## Failure mode

- Шаги `Coms/CH_Start_Max_Heatout`, `Coms/CH_Start_Min_Heatout` и `Coms/CH_Start_ST_Heatout` переводят котёл в режим через регистр `1036`, но сам котёл держит этот режим только около `15 минут`.
- В длительных наладочных сценариях оператор мог остановиться внутри шага, настраивать котёл вручную и затем продолжить выполнение, уже потеряв ранее установленный режим.
- Из-за этого шаг продолжал workflow с ожиданием старого режима, хотя котёл сам вернулся в normal-mode.

## Root cause

- Execution pipeline считал, что успешный write/read-back `1036` достаточен на весь жизненный цикл шага.
- В runtime не было отдельного контура, который бы удерживал последний шаговый режим после успешной установки.
- Reset/completion/repeat lifecycle не имел явного single-source контракта для очистки такого retained-state.
- Operator stop не очищал retained-state отдельно, поэтому latch мог пережить ручную остановку execution.
- `Clear()` инвалидировал только in-memory state и не ждал выхода уже начатого refresh из shared mode-change critical section.

## Resolution

- Добавлен singleton `BoilerOperationModeRefreshService`.
- Сервис сохраняет raw `ushort` последнего подтверждённого шагом режима `1036` и повторно пишет его через системные `RegisterWriter` / `RegisterReader` по интервалу `Diagnostic:OperationModeRefreshInterval` (по умолчанию `15 минут`).
- Refresh выполняется только при `dispatcher ready`:
  - `IsStarted = true`;
  - `IsConnected = true`;
  - `IsReconnecting = false`;
  - `LastPingData != null`.
- При потере связи без reset retained-state не очищается: сервис ждёт восстановления ready-state и только потом дописывает режим.
- Сервис не вызывает `dispatcher.StartAsync()` сам.
- После review-hardening фоновые write/read по `1036` координируются с шаговыми write/read через shared mode-change lease, чтобы stale refresh не мог вклиниться внутрь шагового переключения режима.
- После review-hardening failed refresh больше не использует `WriteVerifyDelayMs` шагов: при write/read/verify fail сервис повторяет попытку через отдельный slow retry `5 секунд`.
- После review-hardening wakeup worker-а стал безопасным для concurrent signal/dispose и не бросает `SemaphoreFullException` / `ObjectDisposedException` из runtime callbacks.
- После follow-up hardening `Clear(...)` оставлен sync-path: он инвалидирует armed-state сразу и запускает coalesced background-drain без накопления новых fire-and-forget задач.
- Добавлен awaited-path `ClearAndDrainAsync(...)`, который завершает cleanup только после выхода активного refresh из mode-change critical section.
- Интервал refresh вынесен в `Diagnostic:OperationModeRefreshInterval`; при отсутствии или некорректном значении (`<= 0`) сервис откатывается к default `15 минут`.
- После follow-up hardening active `Coms/CH_Start_*` шаги делают awaited `ClearAndDrainAsync(...)` на входе, поэтому предыдущий retained-mode больше не переживает новый step/retry/PLC-wait сценарий до нового `ArmMode(...)`.
- `Elec/Boiler_Power_OFF` делает awaited `ClearAndDrainAsync(...)` сразу на входе в шаг, но это только memory-clear: шаг не пишет `1036` и не создаёт новый arm.
- `TestExecutionCoordinator.CompleteAsync()` теперь при `ExecutionStopReason.Operator` вызывает `ClearAndDrainAsync(...)` до `SequenceCompleted`, поэтому downstream больше не видит armed retained-mode после ручной остановки.
- Источники arm:
  - `Coms/CH_Start_Max_Heatout`;
  - `Coms/CH_Start_Min_Heatout`;
  - `Coms/CH_Start_ST_Heatout`.
- Arm ставится сразу после успешного write/read-back `1036`, а не по завершению всего шага.
- Источники clear:
  - вход в active `Coms/CH_Start_Max_Heatout`, `Coms/CH_Start_Min_Heatout`, `Coms/CH_Start_ST_Heatout`;
  - вход в `Elec/Boiler_Power_OFF` без записи `1036` в котёл;
  - успешный `Coms/CH_Reset` после подтверждённого `1036 == 0`;
  - `TestExecutionCoordinator.CompleteAsync()` при `ExecutionStopReason.Operator`;
  - `PlcResetCoordinator.OnForceStop`;
  - `ErrorCoordinator.OnReset`;
  - `BoilerState.OnCleared`;
  - `TestExecutionCoordinator.ResetForRepeat()`.
- Ручные инженерные изменения режима и `SetStandModeAsync(...)` retained-state не меняют.

## Verification

- Добавлены unit/regression tests на:
  - refresh после настроенного дедлайна из `DiagnosticSettings.OperationModeRefreshInterval`;
  - отложенный refresh до восстановления dispatcher ready-state;
  - отсутствие `StartAsync()` из runtime-сервиса;
  - clear по `Clear(...)`, `BoilerState.Clear()`, `OnForceStop`, `OnReset`;
  - awaited operator-stop cleanup до `SequenceCompleted`;
  - `ClearAndDrainAsync(...)` как barrier для активного refresh;
  - coalesced background-drain без task leak;
  - sync clear под уже удерживаемым shared lease без deadlock;
  - stale snapshot под удержанным mode-change lease;
  - отдельный slow retry/backoff для failed refresh;
  - concurrent signal и late callback после dispose;
  - arm в `CH_Start_Max_Heatout`, `CH_Start_Min_Heatout`, `CH_Start_ST_Heatout`;
  - entry-clear прежнего retained-mode в `CH_Start_Max_Heatout`, `CH_Start_Min_Heatout`, `CH_Start_ST_Heatout`;
  - entry-clear прежнего retained-mode в `BoilerPowerOffStep` без записи `1036`;
  - shared mode-change lease в `CH_Start_Max_Heatout`, `CH_Start_Min_Heatout`, `CH_Start_ST_Heatout`, `CH_Reset`;
  - clear-only-on-success для `CH_Reset`.

## Notes

- Новый retained-state относится только к runtime-шагам, которые напрямую пишут `1036`; `BoilerPowerOffStep` является только memory-clear source.
- Legacy-файлы `*Old*.cs` не менялись.
