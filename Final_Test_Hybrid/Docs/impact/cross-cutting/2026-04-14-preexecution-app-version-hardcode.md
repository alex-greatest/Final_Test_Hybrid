# 2026-04-14 preexecution-app-version-hardcode

## Контур

- PreExecution / scan-служебные результаты
- Results UI / storage payload
- Stable execution docs

## Что изменено

- В `ScanStepBase.CaptureScanServiceContext()` источник `App_Version` заменён:
  вместо чтения из `RecipeProvider` используется жёсткая application-константа `v1.0`.
- `Plant_ID` продолжает читаться из `RecipeProvider`; контракт остальных scan-служебных полей не менялся.
- Stable docs синхронизированы с новым source-of-truth:
  - `Docs/execution/StepsGuide.md`
  - `Docs/execution/CycleExitGuide.md`

## Внешний эффект

- В `TestResultsService` и далее в storage/MES path `App_Version` больше не зависит от recipe payload.
- Каждый новый старт теста и repeat сохраняет одинаковое значение `App_Version = v1.0`.
- Отсутствие или изменение recipe-поля `App_Version` больше не влияет на scan-служебные результаты.

## Что сознательно не менялось

- `Plant_ID` остаётся частью внутреннего `ScanServiceContext` и по-прежнему читается из рецептов.
- Состав scan-служебных результатов не менялся:
  - `App_Version`
  - `Shift_No`
  - `Tester_No`
  - `Pres_atmosph.`
  - `Pres_in_gas`
- Логика repeat-flow, reset-flow и reread давлений из OPC не менялась.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Steps/ScanStepBase.cs`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`
- `Final_Test_Hybrid/Docs/execution/CycleExitGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остался baseline warning `MSB3277` по `WindowsBase`.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/ScanStepBase.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-app-version-hardcode.txt" -e=WARNING` — warning-level отчёт пуст.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/ScanStepBase.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-app-version-hardcode.txt" -e=HINT` — новых hints по change-set нет; остались существующие suggestions:
  - `PreExecutionCoordinator` можно перевести на primary constructor;
  - в `ScanStepBase` есть старые structural hints вне сути этой правки (`private`, `static`, `inline temporary variable`).

## Риски / остатки

- Значение `v1.0` не связано с реальной assembly version; при следующем релизе обновление придётся делать вручную.
- Отдельного автотеста на источник `App_Version` в scan-служебных результатах сейчас нет; change-set подтверждается кодовой сверкой и общими проверками.

## Инциденты

- no new incident
