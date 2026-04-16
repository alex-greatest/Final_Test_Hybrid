# 2026-04-15 read article without connection

## Контур

- Runtime test steps
- `Coms/Read_Article_Without_Connection`
- Runtime results `ITestResultsService`
- Stable execution docs

## Что изменено

- Добавлен execution-шаг `Coms/Read_Article_Without_Connection`.
- Шаг не использует PLC/OPC/Modbus и читает только `BoilerState.Article`, сохранённый scan-фазой текущего теста.
- Шаг сохраняет результат `Article_Without_Connection` со `status = 1`, `isRanged = false`, пустыми `min/max/unit`, `test = "Coms/Read_Article_Without_Connection"`.
- Перед записью результата шаг удаляет старый `Article_Without_Connection`, чтобы retry не создавал дубликаты.
- Если `BoilerState.Article` пустой, шаг завершается ошибкой без возможности skip и не сохраняет пустой результат.
- `Docs/execution/StepsGuide.md` обновлён как source-of-truth по контракту нового результата.
- Safe кандидатов на rollup в active impact по этому точечному контуру не найдено.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadArticleWithoutConnectionStep.cs`
- `Final_Test_Hybrid.Tests/Runtime/ReadArticleWithoutConnectionStepTests.cs`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`

## Проверки

- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --filter FullyQualifiedName~ReadArticleWithoutConnectionStepTests` — успешно, `3/3`; остаётся baseline warning `MSB3277` по `WindowsBase`.
- `dotnet build Final_Test_Hybrid.slnx` — успешно; остаётся baseline warning `MSB3277` по `WindowsBase` в app/test проектах.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadArticleWithoutConnectionStep.cs;Final_Test_Hybrid.Tests/Runtime/ReadArticleWithoutConnectionStepTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-read-article-without-connection.txt" -e=WARNING` — warning-level чисто.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadArticleWithoutConnectionStep.cs;Final_Test_Hybrid.Tests/Runtime/ReadArticleWithoutConnectionStepTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-read-article-without-connection.txt" -e=HINT` — hint-level чисто.

## Residual Risks

- Для сохранения в `TB_RESULT` активная история шагов должна содержать точное имя `Coms/Read_Article_Without_Connection`; иначе действует штатный `StepHistoryNotFound` warning + skip storage-записи.
- Ручной runtime-прогон на стенде не выполнялся; контракт подтверждён unit-тестами, build/format и точечным inspectcode.

## Инциденты

- no new incident
