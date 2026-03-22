# 2026-03-22 ch-flow-ntc-cold-error-confirmation

## Контур

- Execution / CH step `CH/Compare_Flow_NTC_Temperature_Cold`

## Что изменено

- В `CompareFlowNtcTemperatureColdStep` добавлено локальное окно подтверждения transient `Error` длиной `150 ms`.
- В фазе `WaitPhase1Async` шаг больше не принимает ранний `Error` мгновенно:
  - `End` сохраняет приоритет;
  - кратковременный `Error`, который сбрасывается в течение окна подтверждения, не завершает шаг;
  - подтверждённый `Error` по-прежнему завершает шаг как `Fail`.
- В фазе `WaitPhase2Async` применено то же правило подтверждения `Error`.
- Для случая гонки между `Ready_1` и terminal-сигналами добавлена локальная перепроверка:
  - `End` и `Error` имеют приоритет над `Ready_1`;
  - при активном `Error` используется то же окно подтверждения, чтобы `Ready_1` не выигрывал у transient/terminal ветки случайно.
- `TagWaiter` и общая runtime-механика подписок не менялись; логика локализована в самом шаге.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Steps/CH/CompareFlowNtcTemperatureColdStep.cs`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остаются baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --no-build` — успешно, `126/126`.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/CH/CompareFlowNtcTemperatureColdStep.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-compare-flow-ntc-cold.txt" -e=WARNING` — блокирующих warning не выявлено; остались только неблокирующие замечания style/cleanup уровня файла.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/CH/CompareFlowNtcTemperatureColdStep.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-compare-flow-ntc-cold.txt" -e=HINT` — остались только cleanup/reflection hints (`redundant interface`, `can be static`, `invert if`, `duplicated statements`), новых runtime-регрессий по change-set не найдено.

## Residual Risks

- Аналогичные шаги `CH/DHW CompareFlowNtc*` по-прежнему используют прежнюю мгновенную обработку `Error`; текущее изменение сознательно локально и не выравнивает семейство целиком.
- Поведение подтверждения опирается на текущий runtime-cache и события `TagWaiter`; отдельный polling в шаг не добавлялся.

## Инциденты

- `no new incident`
