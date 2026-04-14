# 2026-04-11 ch-dhw-missing-plc-error-catalog-entries

## Контур

- PLC error catalog
- `CH/Slow_Fill_Circuit`
- `CH/Compare_Flow_NTC_Temperatures_Hot`
- `DHW/Get_Flow_NTC_Cold`
- `DHW/Compare_Flow_NTC_Temperature_Hot`
- traceability boiler error reseed scripts

## Что изменено

- В `ErrorDefinitions.Steps.Ch` добавлены отсутствовавшие PLC-ошибки:
  - `П-301-03` для `DB_CH.DB_CH_Slow_Fill_Circuit.Al_WaterPressureHight`
  - `П-305-10` для `DB_CH.DB_CH_Compare_Flow_NTC_Temp_Hot.Al_LowTemp`
- В `ErrorDefinitions.Steps.Dhw` добавлены отсутствовавшие PLC-ошибки:
  - `П-206-01` для `DB_DHW.DB_DHW_Get_Flow_NTC_Cold.Al_WaterFlowMin`
  - `П-206-02` для `DB_DHW.DB_DHW_Get_Flow_NTC_Cold.Al_WaterFlowMax`
  - `П-208-02` для `DB_DHW.DB_DHW_Compare_Flow_NTC_Temp_Hot.Al_LowTemp`
- Все пять записей включены в `ErrorDefinitions.StepErrors`, поэтому снова попадают в стандартный путь `All -> PlcErrors -> PlcErrorMonitorService`.
- `ErrorDefinitionsCatalogTests` расширен до параметризованной lookup-регрессии `ByPlcTag(...)`, которая проверяет точное сопоставление тега, кода и описания для новых записей.
- Оба reseed-скрипта traceability boiler errors синхронизированы и теперь содержат те же пять кодов, описаний и `related_step_name`.
- Stable docs не менялись:
  source-of-truth уже фиксирует стандартный PLC monitoring path, а change-set устраняет только пропуски в каталоге и traceability seed.

## Причина

- По коду и поиску по репозиторию подтверждено, что перечисленные PLC-теги уже существуют в PLC-контракте, но отсутствовали в программном каталоге ошибок и reseed-источниках traceability boiler.
- Без этих записей стандартный lookup по `ByPlcTag(...)` и последующая синхронизация справочника ошибок в traceability boiler оставались неполными.

## Затронутые файлы

- `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Ch.cs`
- `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs`
- `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs`
- `Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs`
- `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program.sql`
- `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program_for_pgadmin.sql`

## Проверки

- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --filter FullyQualifiedName~ErrorDefinitionsCatalogTests`
- `dotnet build Final_Test_Hybrid.slnx`
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes`
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes`
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Ch.cs;Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs;Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs;Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-missing-plc-error-catalog.txt" -e=WARNING`

## Residual Risks

- Change-set исправляет только программный каталог и reseed-источники. Для фактической синхронизации справочника в traceability boiler потребуется отдельный запуск обновлённых reseed-скриптов.
- Орфография PLC-тега `Al_WaterPressureHight` остаётся частью существующего PLC/OPC контракта и сознательно не менялась.

## Инциденты

- `no new incident`
