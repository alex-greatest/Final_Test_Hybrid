# 2026-04-15 dhw-check-flow-temperature-rise-lowtemp

## Контур

- PLC error catalog
- `DHW/Check_Flow_Temperature_Rise`
- traceability boiler error reseed scripts

## Что изменено

- В `ErrorDefinitions.Steps.Dhw` добавлена PLC-ошибка `П-205-03` для `DB_DHW.DB_DHW_Check_Flow_Temperature_Rise.Al_LowTemp`.
- Описание ошибки: `Неисправность. Заданная температура не достигнута`.
- Ошибка привязана к существующему execution step `DHW/Check_Flow_Temperature_Rise`.
- Запись включена в `ErrorDefinitions.StepErrors`, поэтому попадает в стандартный путь `All -> PlcErrors -> PlcErrorMonitorService`.
- `ErrorDefinitionsCatalogTests` расширен lookup-регрессией `ByPlcTag(...)` для полного OPC NodeId.
- Оба reseed-скрипта traceability boiler errors синхронизированы с тем же кодом, описанием и `related_step_name`.
- Stable docs не менялись: `StepsGuide` и `ErrorSystemGuide` уже фиксируют стандартный PLC monitoring path, а change-set только дополняет каталог.

## Причина

- Для блока `DB_DHW_Check_Flow_Temperature_Rise` уже были заведены `П-205-00..02`, но PLC-сигнал `Al_LowTemp` отсутствовал в программном каталоге.
- Новая запись является только PLC-ошибкой: приложение не поднимает её через программный `Raise(...)` и не передаёт в `TestStepResult.Fail(...)`.

## Затронутые файлы

- `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs`
- `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs`
- `Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs`
- `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program.sql`
- `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program_for_pgadmin.sql`

## Проверки

- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --filter FullyQualifiedName~ErrorDefinitionsCatalogTests` — успешно, `13/13`; остался baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet build Final_Test_Hybrid.slnx` — успешно; остался baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs;Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs;Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-dhw-check-flow-temp-rise-lowtemp.txt" -e=WARNING` — warning-level чисто.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs;Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs;Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-dhw-check-flow-temp-rise-lowtemp.txt" -e=HINT` — только baseline hints: existing `ErrorDefinition` fields / `StepErrors` can be made private и loop-to-LINQ suggestion в regression-тесте.

## Residual Risks

- Change-set исправляет только программный каталог и traceability seed; для уже заполненной базы потребуется отдельный запуск обновлённых reseed-скриптов.
- Safe rollup не выполнялся: ближайшие related impact entries свежие и описывают отдельные добавления каталога без stale-набора для lossless-compaction.

## Инциденты

- `no new incident`
