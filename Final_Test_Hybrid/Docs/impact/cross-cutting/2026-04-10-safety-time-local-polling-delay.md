# 2026-04-10 safety-time-local-polling-delay

## Контур

- `Coms/Safety_Time`
- Step-level Modbus polling внутри `Coms/Safety_Time`

## Что изменено

- В `SafetyTimeStep` локальный интервал между итерациями polling увеличен с `100 мс` до `300 мс`.
- Глобальный `Diagnostic:WriteVerifyDelayMs` не менялся: общий pacing для остальных Modbus-шагов остаётся прежним.
- Runtime-контракт `Coms/Safety_Time` не менялся:
  - источником истины по связи остаются фактические step-level Modbus `read/write`;
  - краткий reconnect внутри текущей попытки не пережидается;
  - продолжение после communication-fail остаётся только через штатный `Retry`.
- Stable docs обновлены: `StepsGuide` фиксирует, что `Safety time` измеряется по polling detection с интервалом `300 мс`, а не по аппаратному timestamp.
- Ожидаемый эффект: уменьшить частоту последовательных batch-read `EV1/EV2` в шаге, где на стенде периодически наблюдался `type mismatch`, без грубого `500 мс` квантования measurement-фазы.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Steps/Coms/SafetyTimeStep.cs`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остаётся baseline warning `MSB3277` по `WindowsBase`.
- `dotnet build Final_Test_Hybrid\Final_Test_Hybrid.csproj -c Codex /p:UseSharedCompilation=false` — успешно; остаётся baseline warning `MSB3277` по `WindowsBase`.
- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj -c Codex --no-build --filter SafetyTimeStepTests` — успешно, `5/5`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/Coms/SafetyTimeStep.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-safetytime-local-polling.txt" -e=WARNING` — отчёт пуст.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/Coms/SafetyTimeStep.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-safetytime-local-polling.txt" -e=HINT` — только non-blocking cleanup hint `Merge into pattern`.

## Residual Risks

- Более редкий polling снижает временное разрешение измерения safety time: результат фиксируется по моменту обнаружения отключения на очередном Modbus-read. Интервал `300 мс` выбран как меньший компромисс после review риска `500 мс`, чтобы снизить Modbus pressure без чрезмерного маскирования нижнего предела.
- Если `type mismatch` приходит не от частоты polling, а от конкретного протокольного ответа/адреса/типа регистра, этот change-set только снизит вероятность проявления; для полного root-cause потребуется лог с точной фазой (`EV1/EV2 batch-read` или чтение статуса `1005`).

## Инциденты

- `no new incident`
