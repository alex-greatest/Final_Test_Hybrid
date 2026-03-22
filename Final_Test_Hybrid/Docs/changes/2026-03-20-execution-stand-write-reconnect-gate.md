# 2026-03-20 execution stand-write reconnect gate

## Failure mode

- Execution-steps, которые на retry сначала возвращают котёл в режим Стенд, могли стартовать `SetStandModeAsync(...)` в active Modbus reconnect-window.
- В этом сценарии запись ключа `0xD7F8DB56` не доходила до устройства: dispatcher сразу завершал команду `communication-fail`/`State=rejected`, потому что reconnect уже был начат до исполнения.
- Локальный multi-write retry внутри того же шага не помогал, если все попытки попадали в один и тот же reconnect-period.

## Root cause

- Queue reconnect-contract работает корректно и fail-fast reject'ит новые команды во время `IsReconnecting`.
- Retry-ветки execution шагов не отличали "реальная ошибка записи" от "мы зашли в write-path до завершения reconnect".
- Перед `SetStandModeAsync(...)` не было общего readiness-gate по состоянию dispatcher и свежему runtime ping.
- После введения readiness-gate осталась узкая гонка: reconnect мог стартовать уже после успешной проверки ready-state, но до фактического `EnqueueAsync(...)` записи.

## Resolution

- Для execution stand-write введён общий helper `StandModeWriteExecutionHelper`.
- Перед фактической записью helper ждёт ready-state диагностики:
  - `IsStarted = true`;
  - `IsConnected = true`;
  - `IsReconnecting = false`;
  - `LastPingData != null`.
- Ожидание ограничено `20 c` с polling `100 мс`, выполняется через `context.DelayAsync(...)`, поэтому остаётся pause-aware и cancellation-aware.
- После восстановления ready-state helper делает одну реальную запись.
- Если эта запись упала reconnect-reject ошибкой (`State=pending` / `начато переподключение Modbus до начала выполнения`), helper считает это race-window между ready-check и enqueue, повторно ждёт ready-state и делает ещё одну попытку только в пределах того же общего дедлайна `20 c`.
- По обычным write/read ошибкам helper не делает дополнительный retry и сохраняет прежний fail-path шага.
- Если ready-state не восстановился в отведённое окно или dispatcher уже остановлен, шаг получает communication-fail своего текущего fail-path.

## Verification

- Добавлены регрессии на:
  - immediate write при уже ready dispatcher;
  - bounded wait до восстановления reconnect;
  - reconnect-race после ready-check с успешной повторной записью;
  - отсутствие retry для обычной ошибки записи;
  - исчерпание общего дедлайна после reconnect-race;
  - communication-fail по timeout ожидания ready-state;
  - отмену во время ожидания;
  - fail при уже остановленном dispatcher.

## Notes

- Change-set затрагивает только execution-path шагов с `SetStandModeAsync(context.PacedDiagWriter, ...)`.
- Manual/UI сценарии и низкоуровневый queue reconnect-contract не меняются.
- Это уточнение уже зафиксированного failure mode; новый incident-document не требуется.
