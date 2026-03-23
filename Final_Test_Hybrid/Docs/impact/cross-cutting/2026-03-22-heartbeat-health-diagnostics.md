# 2026-03-22 - Heartbeat health diagnostics for AutoReady incidents

## Что изменено

- Добавлен отдельный диагностический монитор `HmiHeartbeatHealthMonitor` с тремя состояниями:
  - `Healthy`
  - `WriteFailed`
  - `MissedWindow`
- `HmiHeartbeatService` теперь:
  - ведёт snapshot последней успешной heartbeat-записи;
  - переводит heartbeat в `WriteFailed` или `MissedWindow` без изменения `OpcConnected`;
  - пишет transition-логи `Heartbeat: ошибка записи`, `Heartbeat: превышен допустимый интервал` и `Heartbeat восстановлен` через `DualLogger`, чтобы они были и в app log, и в test/UI log.
- `AutoReadySubscription` не меняет семантику `Ask_Auto`, но расширяет incident-log:
  - `HeartbeatState`
  - `HeartbeatAgeMs`
  - `LastHeartbeatWriteResult`
- DI-контур OPC дополнили регистрацией `TimeProvider.System` и `HmiHeartbeatHealthMonitor`.
- Тестовая инфраструктура и runtime-тесты переведены на новый ctor `AutoReadySubscription`.

## Зачем

- Закрыть расследовательский gap, когда PLC может уронить `Ask_Auto`, пока PC всё ещё видит `OpcConnected=true`.
- Развести в логах два разных сценария:
  - настоящая потеря OPC session;
  - heartbeat/degraded HMI при живом OPC.
- Не менять reset/completion/pipeline поведение до отдельного решения по контракту.

## Инварианты

- `OpcUaConnectionState.IsConnected` не менялся и не зависит от heartbeat.
- Heartbeat не запускает `Reset()`, не прерывает completion, не меняет UI-gating.
- Контракт PLC по `Ask_Auto` и `DB_HMI.PLC_Flag` не менялся.

## Проверки

- `dotnet build` в изолированной verify-copy:
  - `D:\projects\Final_Test_Hybrid\.codex-build\heartbeat-verify-20260322`
- `dotnet test` в verify-copy:
  - 28/28 green, включая новый `HeartbeatDiagnosticsTests`
- `dotnet format analyzers --verify-no-changes`
- `dotnet format style --verify-no-changes`
- `jb inspectcode -e=WARNING`
- `jb inspectcode -e=HINT`

## Остаточные замечания

- В `inspectcode -e=HINT` остались только pre-existing подсказки:
  - `AutoReadySubscription.HasEverBeenReady` / `ResetFirstAutoFlag`
  - видимость test stub-классов в `PreExecutionTestContextFactory`
- Сборка основного workspace была занята запущенным `Final_Test_Hybrid.exe`, поэтому обязательные build/test gates выполнялись в отдельной verify-copy без изменения runtime пользователя.

## Документация

- Stable docs не менялись: runtime-поведение не изменено, добавлена только observability-надстройка.

## Incident status

- `no new incident`
