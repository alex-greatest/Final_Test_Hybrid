# 2026-04-15 wait five seven sec steps

## Контур

- runtime test steps
- `WaitTime/*` шаги ожидания
- reflection-регистрация `ITestStep`

## Что изменено

- Добавлен `WaitFiveSecStep` с контрактом:
  - `Id = wait-five-sec`
  - `Name = WaitTime/WaitFiveSec`
  - задержка через `context.DelayAsync(TimeSpan.FromSeconds(5), ct)`
  - успешный результат `5 секунд`
- Добавлен `WaitSevenSecStep` с контрактом:
  - `Id = wait-seven-sec`
  - `Name = WaitTime/WaitSevenSec`
  - задержка через `context.DelayAsync(TimeSpan.FromSeconds(7), ct)`
  - успешный результат `7 секунд`
- DI не менялся: `ITestStep` регистрируются через `TestStepRegistry` reflection-поиском и `ActivatorUtilities.CreateInstance(...)`.
- Stable docs не менялись: `Docs/execution/StepsGuide.md` уже фиксирует контракт обычных `ITestStep` и автоматическую регистрацию через рефлексию.
- Safe кандидатов на rollup в active impact по этому точечному контуру не найдено.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Steps/Wait/WaitFiveSecStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Wait/WaitSevenSecStep.cs`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остался baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/Wait/WaitFiveSecStep.cs;Final_Test_Hybrid/Services/Steps/Steps/Wait/WaitSevenSecStep.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-wait-five-seven-sec-steps.txt" -e=WARNING` — warning-level чистый отчёт.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/Wait/WaitFiveSecStep.cs;Final_Test_Hybrid/Services/Steps/Steps/Wait/WaitSevenSecStep.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-wait-five-seven-sec-steps.txt" -e=HINT` — только ожидаемые reflection false positives `Class ... is never used` для новых `ITestStep`, runtime-рисков не найдено.

## Инциденты

- no new incident
