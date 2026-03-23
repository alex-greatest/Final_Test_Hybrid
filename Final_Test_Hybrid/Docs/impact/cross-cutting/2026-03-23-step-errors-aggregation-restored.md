# 2026-03-23 step-errors-aggregation-restored

## Контур

- PLC error monitoring
- step error definitions aggregation

## Что изменено

- В `ErrorDefinitions.StepErrors` возвращены ранее объявленные, но выпавшие из агрегатора step-ошибки для контуров:
  - `CH/Check_Flow_Temperature_Rise`
  - `CH/Close_Circuit_Valve`
  - `CH/Purge_Circuit_Normal_Direction`
  - `DHW/Get_Flow_NTC_Cold`
  - `DHW/Check_Flow_Rate`
  - `DHW/Compare_Flow_NTC_Temperature_Hot`
  - `DHW/Check_Water_Flow_When_In_DHW_Mode`
  - `DHW/High_Pressure_Test`
  - `DHW/Set_Circuit_Pressure`
  - `DHW/Compare_Flow_NTC_Temperature_Cold`
  - `DHW/Set_Tank_Mode`
- После возврата в `StepErrors` эти определения снова попадают в `All -> PlcErrors` и становятся доступны для стандартного PLC-monitoring path через `PlcErrorMonitorService`.
- В том же workstream выполнен узкий hotfix-откат для `Coms/Safety_Time`:
  - `П-111-00` / `AlNotStendReadySafetyTime`
  - `П-111-01` / `AlCloseTimeSafetyTime`
  - оба кода снова исключены из `StepErrors -> All -> PlcErrors`, потому что startup log подтвердил попытку подписки на несуществующие OPC-теги `DB_Gas_Safety_Time.*` и падение в `PlcInitializationCoordinator.InitializeAllAsync()`.
- Причина отката подтверждена stable docs:
  - `Coms/Safety_Time` работает по Modbus-only safety-контракту;
  - шаг не использует отдельный PLC/diagnostic latch и не должен участвовать в `PlcErrorMonitorService`.
- `EcuE9Stb` сознательно не менялся:
  - это не отдельная протокольная ECU-ошибка `ID 1-26`,
  - special-case остаётся только в `EcuErrorSyncService` как локальная классификация `E9 + CH < 100°C`.
- `DeferredPlcErrors` сознательно не менялся:
  - список продолжает использоваться как исключение immediate-monitoring для `П-403-03` и `П-407-03` через `GasValveTubeDeferredErrorService`.
- Stable docs не менялись: текущий source-of-truth уже описывает требуемое поведение (`все PLC-ошибки мониторятся автоматически, кроме deferred-исключений`), а change-set восстанавливает соответствие кода этому контракту.
- Stable docs не менялись: source-of-truth уже отдельно фиксирует исключение для `Coms/Safety_Time` как Modbus-only step без PLC-latch, поэтому hotfix возвращает код в соответствие с этим контрактом.
- Добавлен regression-тест каталога ошибок:
  - все публичные `ErrorDefinition` в `ErrorDefinitions` должны входить в `All`,
    кроме intentional special-cases `EcuE9Stb`, `AlNotStendReadySafetyTime`, `AlCloseTimeSafetyTime`;
  - deferred-коды должны оставаться в `All` и не создавать duplicate code в общем каталоге.

## Затронутые файлы

- `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs`
- `Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остались baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~PlcErrorMonitorServiceTests|FullyQualifiedName~GasValveTubeDeferredErrorServiceTests"` — успешно, `9/9`.
- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --no-build` — успешно, `135/135`.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-step-errors-aggregation.txt" -e=WARNING` — warning-level чисто.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-step-errors-aggregation.txt" -e=HINT` — остался один неблокирующий hint: `Property 'StepErrors' can be made private`.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs;Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-error-catalog-regression.txt" -e=WARNING` — warning-level чисто.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs;Final_Test_Hybrid.Tests/Runtime/ErrorDefinitionsCatalogTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-error-catalog-regression.txt" -e=HINT` — только неблокирующие hints: `StepErrors can be made private`, loop-to-LINQ suggestion в regression-тесте.

## Residual Risks

- Change-set меняет runtime-поведение: для восстановленных кодов PLC-monitoring теперь снова будет поднимать ошибки по соответствующим тегам, кроме сознательно откатанных `П-111-00/01`.
- Отдельных runtime-тестов именно на каждый возвращённый код поштучно не добавлялось; гарантия держится на общем regression-контракте каталога и существующих integration-style тестах мониторинга.

## Инциденты

- `no new incident`
