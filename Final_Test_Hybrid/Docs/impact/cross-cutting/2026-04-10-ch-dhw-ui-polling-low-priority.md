# 2026-04-10 ch-dhw-ui-polling-low-priority

## Контур

- display-only polling `CH/DHW`
- Modbus command priority для UI-температур
- очередь `IModbusDispatcher`

## Что изменено

- UI polling `CH` читает температуру подающей линии через `CommandPriority.Low`.
- UI polling `DHW` читает температуру ГВС через `CommandPriority.Low`.
- `BoilerTemperatureService` получил overload с явным `CommandPriority` для температур `1006/1009`.
- Совместимый overload без явного priority сохранён и по умолчанию остаётся `CommandPriority.High`, чтобы не менять поведение прочих caller-ов.
- `RegisterReader` default priority не менялся.
- Execution steps, `Check_Comms`, ping/keep-alive и диагностические write/read операции вне display-only polling не переводились в `Low`.
- `DiagnosticGuide` обновлён: display-only `CH/DHW` polling не должен конкурировать с execution step-level Modbus операциями в high-priority очереди.

## Затронутые файлы

- `Final_Test_Hybrid/Components/Main/Parameter/CH.razor`
- `Final_Test_Hybrid/Components/Main/Parameter/DHW.razor`
- `Final_Test_Hybrid/Services/Diagnostic/Services/BoilerTemperatureService.cs`
- `Final_Test_Hybrid/Docs/diagnostics/DiagnosticGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid\Final_Test_Hybrid.csproj -c Codex /p:UseSharedCompilation=false` — успешно; остаётся baseline warning `MSB3277` по `WindowsBase`.
- `dotnet build Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj -c Codex` — успешно; остаётся baseline warning `MSB3277` по `WindowsBase`.
- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj -c Codex --no-build --filter SafetyTimeStepTests` — успешно, `5/5`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Diagnostic/Services/BoilerTemperatureService.cs;Final_Test_Hybrid/Components/Main/Parameter/CH.razor;Final_Test_Hybrid/Components/Main/Parameter/DHW.razor" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-ch-dhw-low-priority.txt" -e=WARNING` — только naming-предупреждения по историческому `DHW` acronym.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Diagnostic/Services/BoilerTemperatureService.cs;Final_Test_Hybrid/Components/Main/Parameter/CH.razor;Final_Test_Hybrid/Components/Main/Parameter/DHW.razor" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-ch-dhw-low-priority.txt" -e=HINT` — только non-blocking cleanup/unused hints и существующий `DHW` naming hint.
- `dotnet build Final_Test_Hybrid.slnx /p:UseSharedCompilation=false` — не завершён из-за занятого Debug output: запущенный процесс `Final_Test_Hybrid (16224)` блокирует `Final_Test_Hybrid.exe`.

## Residual Risks

- При плотной `High` очереди display-only значения `CH/DHW` могут обновляться с задержкой или отменяться по своему UI cancellation token. Это не влияет на execution pipeline: команды остаются последовательными через dispatcher, а execution step-level операции не переводились в `Low`.
- Уже начатый `Low` read занимает serial line до завершения своей Modbus-транзакции; priority регулирует порядок очереди, а не прерывает команду, которая уже выполняется.
- Если периодический `type mismatch` вызван не конкуренцией UI polling, а конкретным протокольным ответом/типом регистра, это изменение только снижает давление на очередь; root-cause всё ещё потребует точного лога фазы.

## Инциденты

- `no new incident`
