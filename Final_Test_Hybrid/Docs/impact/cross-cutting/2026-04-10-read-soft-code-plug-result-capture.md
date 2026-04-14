# 2026-04-10 read-soft-code-plug-result-capture

## Контур

- `Coms/Read_Soft_Code_Plug`
- Runtime results `ITestResultsService`
- Контракт сохранения результатов SoftCodePlug-проверок

## Что изменено

- `Coms/Read_Soft_Code_Plug` теперь сохраняет фактическое значение регистра `1054` как `NumberOfContours`.
- `Coms/Read_Soft_Code_Plug` теперь сохраняет фактическое значение регистра `1071` как `Thermostat_Jumper`.
- Оба результата добавлены в action-table шага, поэтому участвуют в общем `ClearPreviousResults()` и удаляются перед retry вместе с остальными результатами шага.
- Для NOK-проверок значение сохраняется до возврата `Fail`:
  - mismatch `1054` сохраняется со `status = 2`;
  - отсутствующая перемычка `1071 = 0` сохраняется со `status = 2`.
- При ошибке Modbus-read результат не сохраняется, потому что фактического значения регистра нет; текущий read-failure contract шага не менялся.
- Новые параметры сохраняются как raw numbers, без текстового формата `Open/Closed` или `Single/Dual`.
- `Docs/execution/StepsGuide.md` и `Docs/diagnostics/DiagnosticGuide.md` обновлены как source-of-truth по сохраняемым результатам `Read_Soft_Code_Plug`.
- После sub-agent review stable docs дополнены metadata-контрактом `raw/status/isRanged/min/max/unit/test`, а helper-методы execution partial вынесены в отдельный partial, чтобы вернуть `ReadSoftCodePlugStep.Actions.Execution.cs` ниже runtime-critical size limit.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Models.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Execution.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Execution.Helpers.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Table.Part1.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Table.Part2.cs`
- `Final_Test_Hybrid.Tests/Runtime/ReadSoftCodePlugStepTests.cs`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`
- `Final_Test_Hybrid/Docs/diagnostics/DiagnosticGuide.md`

## Проверки

- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj -c Codex --filter ReadSoftCodePlugStepTests` — успешно, `5/5`; остаётся baseline warning `MSB3277` по `WindowsBase`.
- `dotnet build Final_Test_Hybrid\Final_Test_Hybrid.csproj -c Codex` — успешно; остаётся baseline warning `MSB3277` по `WindowsBase`.
- `dotnet build Final_Test_Hybrid.slnx` — не завершён из-за занятого Debug output: запущенный процесс `Final_Test_Hybrid (16224)` блокирует `Final_Test_Hybrid\bin\Debug\net10.0-windows\Final_Test_Hybrid.exe`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно; workspace warning без влияния на результат.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Models.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Execution.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Execution.Helpers.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Table.Part1.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Table.Part2.cs;Final_Test_Hybrid.Tests/Runtime/ReadSoftCodePlugStepTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-read-soft-code-plug-results.txt" -e=WARNING` — новых warning по change-set нет; остался pre-existing warning `float == float` в общем `ExecuteVerifyFloatAction`.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Models.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Execution.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Execution.Helpers.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Table.Part1.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Table.Part2.cs;Final_Test_Hybrid.Tests/Runtime/ReadSoftCodePlugStepTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-read-soft-code-plug-results.txt" -e=HINT` — только non-blocking cleanup/performance hints; новых runtime-contract замечаний не найдено.
- Sub-agent review — найденные P3 закрыты: stable docs дополнены metadata-контрактом, `ReadSoftCodePlugStep.Actions.Execution.cs` уменьшен до `296` строк.

## Residual Risks

- `dotnet build Final_Test_Hybrid.slnx` в Debug остаётся непроверенным до остановки локального процесса `Final_Test_Hybrid (16224)`.
- Старый warning `float == float` в `ExecuteVerifyFloatAction` не относится к новым raw-result записям и не менялся в этом change-set.

## Инциденты

- `no new incident`
