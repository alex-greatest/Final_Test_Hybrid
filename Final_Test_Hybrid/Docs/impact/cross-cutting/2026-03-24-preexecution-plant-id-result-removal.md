# 2026-03-24 preexecution-plant-id-result-removal

## Контур

- PreExecution / scan-служебные результаты
- Results UI / storage payload
- Stable execution docs

## Что изменено

- В `PreExecutionCoordinator.WriteScanServiceResultsAsync()` удалена запись
  `Plant_ID` в `TestResultsService`.
- Константа `PlantIdResult` удалена как больше не используемая.
- `Plant_ID` оставлен во внутреннем `ScanServiceContext`:
  scan-step по-прежнему читает его из рецепта и логирует в scan-контексте.
- Stable docs синхронизированы с новым контрактом:
  - `Docs/execution/StepsGuide.md`
  - `Docs/execution/CycleExitGuide.md`

## Внешний эффект

- `Plant_ID` больше не появляется в `TestResultsService`.
- `Plant_ID` исчезает из `Results` UI.
- `Plant_ID` исчезает из набора результатов, который далее использует storage/MES path.
- Остальные scan-служебные результаты не менялись:
  - `App_Version`
  - `Shift_No`
  - `Tester_No`
  - `Pres_atmosph.`
  - `Pres_in_gas`

## Что сознательно не менялось

- `ScanStepBase.CaptureScanServiceContext()`.
- Контракт `ScanServiceContext` и поле `PlantId`.
- Логика repeat-flow, reset-flow и чтение давлений из OPC.
- Источники значений `App_Version`, `Shift_No`, `Tester_No`.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.cs`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`
- `Final_Test_Hybrid/Docs/execution/CycleExitGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остались baseline warning `MSB3277` по `WindowsBase`.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~StartTimer1StepTimingTests|FullyQualifiedName~PreExecutionStopReasonTests"` — успешно, `3/3`.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-plant-id-preexecution.txt" -e=WARNING` — warning-level отчёт пуст.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-plant-id-preexecution.txt" -e=HINT` — только существующие hints вне change-set:
  - рекомендация перевести `PreExecutionCoordinator` на primary constructor;
  - suggestion по `Replace with primary constructor parameter`;
  - старые structural hints `Duplicated 'if' branches` и `Merge into pattern`.

## Риски / остатки

- Отдельного автотеста именно на состав scan-служебных результатов сейчас нет.
  Для этого path в тестах нет готового дешёвого harness вокруг чтения давлений
  из `ScanStepBase.ReadPressuresAsync()`, поэтому change-set подтверждён
  кодовой сверкой, build/inspectcode и смежным pre-execution smoke.
- Safe candidate на rollup в этом контуре не найден:
  активной same-topic impact-цепочки по `Plant_ID` / scan-служебным результатам нет.

## Инциденты

- no new incident
