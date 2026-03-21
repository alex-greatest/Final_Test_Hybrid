# 2026-03-21 plc auto false on app shutdown

## Контур

- OPC UA startup/shutdown lifecycle
- PLC tag `DB_Station.Test.Auto`
- WinForms application closing flow

## Что изменено

- Сохранён текущий runtime-контракт:
  - при connect/reconnect сервис `PlcAutoWriterService` по-прежнему пишет `DB_Station.Test.Auto = true`;
  - обычный disconnect/reconnect-path не менялся.
- Добавлен отдельный shutdown-path `WriteAutoFalseOnShutdownAsync(...)` в `PlcAutoWriterService`.
- Новый shutdown-path:
  - вызывается только явно из `Form1.OnFormClosing(...)`;
  - выполняется до `OpcUaConnectionService.DisconnectAsync()`;
  - пишет `DB_Station.Test.Auto = false` только при закрытии приложения.
- Shutdown-запись сделана как `best effort`:
  - если OPC UA уже не подключён, запись пропускается без блокировки закрытия;
  - используется bounded retry для transient-ошибок;
  - локальный дедлайн вызова из `Form1` ограничен `2 c`, чтобы закрытие приложения не зависало.

## Затронутые файлы

- `Final_Test_Hybrid/Form1.cs`
- `Final_Test_Hybrid/Services/OpcUa/Auto/PlcAutoWriterService.cs`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; сохранены существующие warnings `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0`.
- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --no-build` — успешно, 68/68.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Form1.cs;Final_Test_Hybrid/Services/OpcUa/Auto/PlcAutoWriterService.cs" --no-build --format=Text "--output=artifacts/inspect-warning-plc-auto-shutdown.txt" -e=WARNING` — новых warning по change-set нет; в `Form1.cs` остались существующие замечания общего характера.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Form1.cs;Final_Test_Hybrid/Services/OpcUa/Auto/PlcAutoWriterService.cs" --no-build --format=Text "--output=artifacts/inspect-hint-plc-auto-shutdown.txt" -e=HINT` — только существующие hints в `Form1.cs`, без новых замечаний по shutdown-path.

## Инциденты

- no new incident
