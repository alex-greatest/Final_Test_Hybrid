# 2026-04-08 dhw-set-tank-mode-high-pressure-error

## Контур

- PLC error catalog
- `DHW/Set_Tank_Mode`
- traceability boiler error reseed scripts

## Что изменено

- В `ErrorDefinitions.Steps.Dhw` добавлена отсутствовавшая PLC-ошибка `П-213-02` для тега `ns=3;s="DB_DHW"."DB_Set_Tank_Mode"."Al_PressureHight"`.
- Описание ошибки задано как `DB_Set_Tank_Mode. Неисправность. Давление выше заданного`.
- Новая ошибка включена в `ErrorDefinitions.StepErrors`, поэтому снова попадает в стандартный path `All -> PlcErrors -> PlcErrorMonitorService`.
- В `ErrorDefinitionsCatalogTests` добавлена regression-проверка `ByPlcTag(...)`, которая подтверждает lookup по точному PLC-тегу `DB_Set_Tank_Mode.Al_PressureHight`.
- Оба reseed-скрипта traceability boiler errors синхронизированы и теперь содержат строку для `П-213-02`.
- Stable docs не менялись:
  текущий source-of-truth уже фиксирует, что PLC-ошибки берутся из общего каталога и мониторятся через стандартный PLC monitoring path; change-set устраняет рассинхронизацию каталога для одного конкретного тега.

## Причина

- По коду и поиску по репозиторию подтверждено, что для `DHW/Set_Tank_Mode` были заведены только:
  - `П-213-00` / `Al_WaterFlowLow`
  - `П-213-01` / `Al_PressureLow`
- Тег `DB_DHW.DB_Set_Tank_Mode.Al_PressureHight` отсутствовал в `ErrorDefinitions` и в reseed-скриптах, хотя аналогичный high-pressure failure mode уже используется в соседних DHW-контурах (`High_Pressure_Test`, `Set_Circuit_Pressure`).

## Затронутые файлы

- `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs`
- `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs`
- `Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs`
- `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program.sql`
- `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program_for_pgadmin.sql`

## Проверки

- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --filter FullyQualifiedName~ErrorDefinitionsCatalogTests` — успешно, `3/3`; остались baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet build Final_Test_Hybrid.slnx` — успешно; остались baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs;Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs;Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-dhw-set-tank-mode-high-pressure.txt" -e=WARNING` — warning-level чисто.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs;Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs;Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-dhw-set-tank-mode-high-pressure.txt" -e=HINT` — только baseline hints: `StepErrors can be made private`, множество existing `ErrorDefinition` fields can be made private, и loop-to-LINQ suggestion в regression-тесте.

## Residual Risks

- Change-set исправляет только каталог и traceability seed; если база уже была заполнена старым набором кодов, для фактической синхронизации справочника потребуется отдельный запуск reseed-скрипта.
- Семантика PLC-тега берётся из текущего контракта `Al_PressureHight`; орфография `Hight` остаётся частью существующего PLC/OPC контракта и сознательно не менялась.

## Инциденты

- `no new incident`
