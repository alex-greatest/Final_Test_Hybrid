# 2026-04-01 dhw-in-pres-recipe-tolerance-limits

## Контур

- `DHW/Set_Circuit_Pressure`
- контракт пределов runtime-результата `DHW_In_Pres`

## Что изменено

- В `SetCircuitPressureStep` исправлен контракт пределов для `DHW_In_Pres`.
- Вместо прежней схемы `min = DB_Recipe.DHW.PresTest.Value`, `max = 999.000` шаг теперь использует:
  - `min = DB_Recipe.DHW.PresTest.Value - DB_Recipe.DHW.PresTest.Tol`
  - `max = DB_Recipe.DHW.PresTest.Value + DB_Recipe.DHW.PresTest.Tol`
- В `RequiredRecipeAddresses` добавлен recipe `ns=3;s="DB_Recipe"."DHW"."PresTest"."Tol"`.
- `GetLimits(...)` для шага теперь показывает диапазон `Value ± Tol`, а не `Value .. 999`.
- В `TestResultsService.Add(...)` шаг сохраняет вычисленные `min/max` по тому же контракту `Value ± Tol`.
- PLC runtime-логика шага не менялась:
  - статус результата остаётся привязан к `End/Error`;
  - локальная дополнительная валидация фактического давления по диапазону не вводилась.
- В `Docs/execution/StepsGuide.md` добавлена stable-doc фиксация контракта для `DHW_In_Pres`.
- Добавлен regression-тест на `GetLimits(...)`, который подтверждает:
  - кейс `2.5 ± 0.5 -> [2,0 .. 3,0]`;
  - отсутствие hardcode через отдельный кейс `2.5 ± 0.3 -> [2,2 .. 2,8]`.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Steps/DHW/SetCircuitPressureStep.cs`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`
- `Final_Test_Hybrid.Tests/Runtime/SetCircuitPressureStepTests.cs`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остались baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --filter FullyQualifiedName~SetCircuitPressureStepTests` — успешно, `2/2`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/DHW/SetCircuitPressureStep.cs;Final_Test_Hybrid.Tests/Runtime/SetCircuitPressureStepTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-dhw-in-pres-limits.txt" -e=WARNING` — чисто.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/DHW/SetCircuitPressureStep.cs;Final_Test_Hybrid.Tests/Runtime/SetCircuitPressureStepTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-dhw-in-pres-limits.txt" -e=HINT` — чисто.

## Residual Risks

- Шаг по-прежнему не делает локальную runtime-валидацию фактического `In_Press` против диапазона `Value ± Tol`; диапазон используется только для отображения и сохранения результата.
- Числовой формат `GetLimits(...)` остаётся culture-aware (`2,0 .. 3,0` в текущей локали), потому что change-set не меняет общий UI-контракт форматирования пределов.

## Инциденты

- `no new incident`
