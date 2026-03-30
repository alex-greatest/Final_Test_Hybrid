# 2026-03-24 plc-error-monitor-duplicate-callback-dedup

## Контур

- PLC error monitoring
- Runtime log noise cleanup

## Что изменено

- В `PlcErrorMonitorService` добавлен локальный dedup последнего нормализованного bool-состояния по коду PLC-ошибки.
- Duplicate callback'и с тем же состоянием (`true -> true`, `false -> false`) больше не вызывают повторный `RaisePlc(...)` / `ClearPlc(...)`.
- Основной целевой сценарий: при удержании глобальной PLC-ошибки `О-001-01` (`DB_Message.Alarm4[2]`) повторные OPC callback'и больше не создают spam warning `ErrorService duplicate raise`.
- Runtime-семантика не менялась:
  - `OpcUaSubscription` по-прежнему доставляет каждый callback;
  - dedup сделан только на уровне `PlcErrorMonitorService`;
  - переходы состояния `false -> true` и `true -> false` по-прежнему поднимают и снимают ошибку штатно;
  - deferred-исключения `П-403-03` / `П-407-03` не затронуты.
- Stable doc `Docs/runtime/ErrorSystemGuide.md` обновлён под новый контракт monitor path.
- Добавлены regression-тесты:
  - duplicate `true` не вызывает второй `RaisePlc`;
  - duplicate `false` не вызывает второй `ClearPlc`.

## Что сознательно не менялось

- `ErrorService` duplicate-guard и его warning contract.
- `OpcUaSubscription.Callbacks` и глобальный delivery одинаковых OPC значений.
- Application error paths (`ErrorCoordinator`, `EcuErrorSyncService`, `ErrorScope`).
- Reconnect/full rebuild contract runtime OPC subscriptions.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Errors/PlcErrorMonitorService.cs`
- `Final_Test_Hybrid.Tests/Runtime/PlcErrorMonitorServiceTests.cs`
- `Final_Test_Hybrid/Docs/runtime/ErrorSystemGuide.md`

## Проверки

- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~PlcErrorMonitorServiceTests"` — успешно, `4/4`.
- `dotnet build Final_Test_Hybrid.slnx` — успешно; остались baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~PlcErrorMonitorServiceTests"` — успешно, `4/4`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Errors/PlcErrorMonitorService.cs;Final_Test_Hybrid.Tests/Runtime/PlcErrorMonitorServiceTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-plc-error-monitor-dedup.txt" -e=WARNING` — warning-level чисто.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Errors/PlcErrorMonitorService.cs;Final_Test_Hybrid.Tests/Runtime/PlcErrorMonitorServiceTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-plc-error-monitor-dedup.txt" -e=HINT` — остались только существующие low-priority hints про потенциально дорогие logging arguments в `PlcErrorMonitorService.cs`.

## Residual Risks

- Dedup хранит только последнее состояние внутри `PlcErrorMonitorService`; если кто-то вне стандартного PLC-path вручную очистит активную PLC-ошибку, повторный `true` callback с тем же значением не поднимет её заново до следующего реального перехода состояния.
- В текущем repo такой внешний runtime-path не используется; штатный контракт PLC-ошибок остаётся `ClearPlc(...)` по сигналу PLC.

## Инциденты

- no new incident
