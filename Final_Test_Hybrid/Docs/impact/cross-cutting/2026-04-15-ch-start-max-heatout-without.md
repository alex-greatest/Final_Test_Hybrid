# 2026-04-15 ch-start-max-heatout-without

## Контур

- Execution-шаг `Coms/CH_Start_Max_Heatout_Without`
- PLC-подписки для step-level блоков
- Source-of-truth документация steps / diagnostics

## Что изменено

- Добавлен отдельный PLC-only шаг `Coms/CH_Start_Max_Heatout_Without`.
- Шаг использует блок `DB_Coms.DB_CH_Start_Max_Heatout_Without` и подписывается только на `Start`, `End`, `Error`.
- Поведение шага повторяет простой PLC-block паттерн: запись `Start=true`, ожидание `End/Error`, при успехе сброс `Start=false`.
- Существующий `Coms/CH_Start_Max_Heatout` не менялся и остаётся владельцем текущего Modbus/`1036` сценария максимального нагрева.
- Stable docs уточняют, что `_Without` не пишет `1036`, не вызывает `BoilerOperationModeRefreshService.ArmMode(...)` и не входит в retained-mode контракт.
- В `ErrorDefinitions.Steps.Coms` добавлена PLC-ошибка `П-109-02` для `DB_Coms.DB_CH_Start_Max_Heatout_Without.Al_NoWaterFlow` с описанием `Неисправность. Нет протока воды`.
- Ошибка включена в `ErrorDefinitions.StepErrors`, поэтому попадает в стандартный путь `All -> PlcErrors -> PlcErrorMonitorService`.
- `ErrorDefinitionsCatalogTests` расширен lookup-регрессией по полному OPC NodeId и проверкой привязки ошибки к `Coms/CH_Start_Max_Heatout_Without`.
- Оба reseed-скрипта traceability boiler errors синхронизированы с кодом `П-109-02`, описанием и `related_step_name = Coms/CH_Start_Max_Heatout_Without`.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChStartMaxHeatoutWithoutStep.cs`
- `Final_Test_Hybrid.Tests/Runtime/ChStartMaxHeatoutWithoutStepTests.cs`
- `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Coms.cs`
- `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs`
- `Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`
- `Final_Test_Hybrid/Docs/diagnostics/DiagnosticGuide.md`
- `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program.sql`
- `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program_for_pgadmin.sql`

## Проверки

- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --filter FullyQualifiedName~ChStartMaxHeatoutWithoutStepTests` — успешно, `3/3`; остаётся baseline warning `MSB3277` по `WindowsBase`.
- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --filter FullyQualifiedName~ErrorDefinitionsCatalogTests` — успешно, `12/12`; остаётся baseline warning `MSB3277` по `WindowsBase`.
- `dotnet build Final_Test_Hybrid.slnx` — успешно; остаётся baseline warning `MSB3277` по `WindowsBase` в app/test проектах.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/Coms/ChStartMaxHeatoutWithoutStep.cs;Final_Test_Hybrid.Tests/Runtime/ChStartMaxHeatoutWithoutStepTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-ch-start-max-heatout-without.txt" -e=WARNING` — найден только осознанный warning `Base interface 'ITestStep' is redundant`, оставлен по runtime/editor contract требованию явного `ITestStep` для отображения в последовательности.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Coms.cs;Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs;Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-coms-start-max-without-water-flow.txt" -e=WARNING` — warning-level чисто.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Coms.cs;Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs;Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-coms-start-max-without-water-flow.txt" -e=HINT` — только baseline hints: existing `ErrorDefinition` fields / `StepErrors` can be made private и loop-to-LINQ suggestion в regression-тесте.

## Residual Risks

- Фактическое существование PLC-тегов `DB_Coms.DB_CH_Start_Max_Heatout_Without.Start/End/Error` проверяется runtime-валидацией подписок при запуске приложения/сценария, не unit-тестом.
- Фактическое существование PLC-тега `DB_Coms.DB_CH_Start_Max_Heatout_Without.Al_NoWaterFlow` проверяется runtime-подпиской `PlcErrorMonitorService`; unit-тест фиксирует только программный каталог и lookup.
- Change-set обновляет программный каталог и traceability seed; для уже заполненной базы потребуется отдельный запуск обновлённых reseed-скриптов.

## Инциденты

- `no new incident`
