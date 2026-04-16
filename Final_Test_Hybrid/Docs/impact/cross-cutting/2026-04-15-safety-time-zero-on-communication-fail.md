# 2026-04-15 safety-time-zero-on-communication-fail

## Контур

- `Coms/Safety_Time`
- Runtime-запись результата `Safety time`
- Step-level Modbus communication-fail

## Что изменено

- В `SafetyTimeStep` при communication-fail до сохранения измеренного значения теперь записывается результат `Safety time = 0.00 сек`.
- Запись получает статус NOK (`status = 2`), `test = "Coms/Safety_Time"` и пределы `ignSafetyTimeMin/Max`, если рецепты доступны.
- После записи нулевого результата попытка по-прежнему завершается ошибкой связи с `canSkip = false`; локальный reconnect/retry внутри шага не добавлялся.
- Если реальное значение уже сохранено, а ошибка связи происходит позже на проверке статуса или сбросе блокировки Б, сохранённое значение не заменяется нулём.
- Stable docs обновлены: `StepsGuide` фиксирует новый fallback-result для communication-fail до сохранения измерения.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Steps/Coms/SafetyTimeStep.cs`
- `Final_Test_Hybrid.Tests/Runtime/SafetyTimeStepTests.cs`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остаётся baseline warning `MSB3277` по `WindowsBase`.
- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --filter SafetyTimeStepTests /p:UseSharedCompilation=false` — успешно, `5/5`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/Coms/SafetyTimeStep.cs;Final_Test_Hybrid.Tests/Runtime/SafetyTimeStepTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-safetytime-zero-on-comm-fail.txt" -e=WARNING` — отчёт пуст.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/Coms/SafetyTimeStep.cs;Final_Test_Hybrid.Tests/Runtime/SafetyTimeStepTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-safetytime-zero-on-comm-fail.txt" -e=HINT` — только test cleanup hint `Convert into 'return' statement`.
- Sub-agent review — runtime-логика без замечаний; замечания по проверке `Min/Max` и late-failure overwrite закрыты тестами и уточнением `StepsGuide`.

## Residual Risks

- Нулевой NOK результат является fallback-маркером сбоя связи, а не фактическим измерением времени отключения. Диагностика причины остаётся в ошибке шага и Modbus-логах.
- Если рецепты `ignSafetyTimeMin/Max` не загружены, fallback-запись сохраняется без диапазона; штатный pipeline должен загружать эти рецепты через `IRequiresRecipes`.
- Rollup не выполнялся: активная safety-time запись от `2026-04-10` относится к polling interval, текущая запись фиксирует отдельный result-contract.

## Инциденты

- `no new incident`
