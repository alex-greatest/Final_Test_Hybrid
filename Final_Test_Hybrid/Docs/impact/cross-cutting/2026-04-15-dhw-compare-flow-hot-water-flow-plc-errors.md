# 2026-04-15 dhw-compare-flow-hot-water-flow-plc-errors

## Контур

- PLC error catalog
- `DHW/Compare_Flow_NTC_Temperature_Hot`
- traceability boiler error reseed scripts

## Что изменено

- В `ErrorDefinitions.Steps.Dhw` добавлены две PLC-ошибки для блока `DB_DHW.DB_DHW_Compare_Flow_NTC_Temp_Hot`:
  - `П-208-03` для `Al_WaterFlowMin`
  - `П-208-04` для `Al_WaterFlowMax`
- Обе ошибки привязаны к существующему execution step `DHW/Compare_Flow_NTC_Temperature_Hot`.
- Новые записи включены в `ErrorDefinitions.StepErrors`, поэтому попадают в стандартный путь `All -> PlcErrors -> PlcErrorMonitorService`.
- `ErrorDefinitionsCatalogTests` расширен lookup-регрессией `ByPlcTag(...)` для обоих новых PLC-тегов.
- Оба reseed-скрипта traceability boiler errors синхронизированы с теми же кодами, описаниями и `related_step_name`.
- Stable docs не менялись: `StepsGuide` и `ErrorSystemGuide` уже фиксируют стандартный PLC monitoring path, а change-set только дополняет каталог.

## Причина

- По коду подтверждено, что имя шага — `DHW/Compare_Flow_NTC_Temperature_Hot`.
- Имя `DB_DHW_Compare_Flow_NTC_Temp_Hot` относится к PLC/OPC-блоку, а не к `ITestStep.Name`.
- Для `П-208` уже существовали `00..02`; новые расходные ошибки продолжили последовательность как `П-208-03` и `П-208-04`.

## Затронутые файлы

- `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs`
- `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs`
- `Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs`
- `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program.sql`
- `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program_for_pgadmin.sql`

## Проверки

- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --filter FullyQualifiedName~ErrorDefinitionsCatalogTests` — успешно, `10/10`; остался baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet build Final_Test_Hybrid.slnx` — успешно; остался baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs;Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs;Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-dhw-compare-flow-hot-water-flow.txt" -e=WARNING` — warning-level чисто.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs;Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs;Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-dhw-compare-flow-hot-water-flow.txt" -e=HINT` — только baseline hints: `StepErrors can be made private`, existing `ErrorDefinition` fields can be made private, и loop-to-LINQ suggestion в regression-тесте.

## Residual Risks

- Change-set исправляет только программный каталог и traceability seed; для уже заполненной базы потребуется отдельный запуск обновлённых reseed-скриптов.
- Safe rollup не выполнялся: ближайшие related impact entries свежие и описывают отдельные добавления каталога без stale-набора для lossless-compaction.

## Инциденты

- `no new incident`
