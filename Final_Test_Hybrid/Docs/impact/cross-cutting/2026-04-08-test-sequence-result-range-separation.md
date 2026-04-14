# 2026-04-08 test-sequence-result-range-separation

## Контур

- `TestSequenseGrid`
- контракты `Result` / `Range` для runtime-step сообщений
- `Coms/Read_CH_Poti_Setpoint`
- `Coms/Read_ECU_Version`
- `Coms/Safety_Time`
- `CH/Check_Flow_Temperature_Rise`
- `DHW/Check_Flow_Temperature_Rise`

## Что изменено

- В `ReadChPotiSetpointStep` из `TestStepResult.Message` убрано дублирование диапазона `[{MinTemp}..{MaxTemp}]`.
- В `ReadEcuVersionStep` из `TestStepResult.Message` убрано дублирование диапазона версии `[{versionMinStr}..{versionMaxStr}]`.
- В `SafetyTimeStep` fail-сообщение больше не вшивает конкретные пределы `[{min} .. {max}]`; в `Result` остаётся только факт выхода за пределы.
- Пределы для этих шагов по-прежнему формируются через `IProvideLimits` и остаются отдельным источником данных для колонки `Пределы`.
- В `Docs/execution/StepsGuide.md` добавлено stable-doc правило: колонка `Результаты` должна содержать только фактический результат шага или краткий текст ошибки, а пределы должны выводиться через `IProvideLimits` в колонке `Пределы`.
- Вынесен общий formatter для temperature-rise сообщений, чтобы каждая метка в multi-value `Result` стояла сразу перед своим значением.
- В `CH/CheckFlowTemperatureRiseStep` сообщение нормализовано в вид `Температура: <value>, Разница: <value>`.
- В `DHW/CheckFlowTemperatureRiseStep` исправлена сломанная строка `Температура: Разница: ...`; теперь сообщение формируется как `Температура: <value>, Разница: <value>, Расход: <value>`.
- В `Docs/execution/StepsGuide.md` добавлено stable-doc правило для multi-value результатов: каждая пара обязана быть в формате `<Метка>: <Значение>`, а пары разделяются `, ` или `; `.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadChPotiSetpointStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadEcuVersionStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/SafetyTimeStep.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/TemperatureRiseResultMessageFormatter.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/CH/CheckFlowTemperatureRiseStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/DHW/CheckFlowTemperatureRiseStep.cs`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`
- `Final_Test_Hybrid.Tests/Runtime/TemperatureRiseResultMessageFormatterTests.cs`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остались baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadChPotiSetpointStep.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadEcuVersionStep.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/SafetyTimeStep.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-result-range-split.txt" -e=WARNING` — новых warning по сути change-set не выявлено; в отчёте остались pre-existing замечания про redundant base interface и nullable return в `GetLimits`.
- `dotnet build .codex-build\\verify-copy-20260408-temperature-rise-message\\Final_Test_Hybrid.slnx` — успешно; остались baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet test .codex-build\\verify-copy-20260408-temperature-rise-message\\Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~Final_Test_Hybrid.Tests.Runtime.TemperatureRiseResultMessageFormatterTests"` — успешно, `2/2`.
- `dotnet format analyzers .codex-build\\verify-copy-20260408-temperature-rise-message\\Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style .codex-build\\verify-copy-20260408-temperature-rise-message\\Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/TemperatureRiseResultMessageFormatter.cs;Final_Test_Hybrid/Services/Steps/Steps/CH/CheckFlowTemperatureRiseStep.cs;Final_Test_Hybrid/Services/Steps/Steps/DHW/CheckFlowTemperatureRiseStep.cs;Final_Test_Hybrid.Tests/Runtime/TemperatureRiseResultMessageFormatterTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-temperature-rise-message.txt" -e=WARNING` — не выполнен: `jb inspectcode` падает на этой машине с `I/O error occurred.` после `Start Roslyn...`.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/TemperatureRiseResultMessageFormatter.cs;Final_Test_Hybrid/Services/Steps/Steps/CH/CheckFlowTemperatureRiseStep.cs;Final_Test_Hybrid/Services/Steps/Steps/DHW/CheckFlowTemperatureRiseStep.cs;Final_Test_Hybrid.Tests/Runtime/TemperatureRiseResultMessageFormatterTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-temperature-rise-message.txt" -e=HINT` — не выполнен: `jb inspectcode` падает на этой машине с `I/O error occurred.` после `Start Roslyn...`.

## Residual Risks

- `inspectcode` warning по `ReadChPotiSetpointStep` и `ReadEcuVersionStep` остаются в кодовой базе вне рамок этого точечного change-set:
  - redundant `ITestStep` при наличии `IProvideLimits` / `IRequiresRecipes`
  - nullable `GetLimits` в `ReadChPotiSetpointStep`
- `jb inspectcode` для текущего change-set не дал результата из-за локального tool-level сбоя `I/O error occurred.`; warning/hint слой остался непроверенным именно этим инструментом.
- Ручной интерактивный прогон desktop UI в этом сеансе не выполнялся; разделение `Result` / `Range` и формат multi-value сообщений подтверждены кодом, точечными тестами и обязательными статическими проверками.

## Инциденты

- `no new incident`
