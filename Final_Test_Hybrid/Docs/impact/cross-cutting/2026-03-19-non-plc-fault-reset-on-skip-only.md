# 2026-03-19 non-plc-fault-reset-on-skip-only

## Контур

- Test execution / retry-skip / non-PLC fault handshake

## Что изменено

- Для шагов без PLC-блока `Fault=false` больше не пишется в retry-flow.
- Retry теперь повторно выполняет шаг, но не гасит общий `DB_Station.Test.Fault`.
- Сброс `Fault=false` оставлен только в skip-flow после подтверждения skip через `EndStep`.
- Source-of-truth по retry/skip синхронизирован в `RetrySkipGuide.md`.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs`
- `Final_Test_Hybrid/Docs/execution/RetrySkipGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — не выполнен: `bin\\Debug\\net10.0-windows\\Final_Test_Hybrid.exe` заблокирован запущенным процессом `Final_Test_Hybrid (24876)`.
- `dotnet build Final_Test_Hybrid.slnx -p:UseAppHost=false` — не выполнен по той же причине: заблокирован `Final_Test_Hybrid.dll`.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs" --no-build --format=Text "--output=inspect-warning-non-plc-fault-reset.txt" -e=WARNING` — новых warning по изменению нет.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs" --no-build --format=Text "--output=inspect-hint-non-plc-fault-reset.txt" -e=HINT` — только существующие hint по стоимости вычисления аргументов логирования в этом файле; новых замечаний по изменённой логике нет.

## Инциденты

- no new incident
