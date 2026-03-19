## 1. Implementation

- [x] 1.1 Добавить safe-read контракт `TryGetValue<T>` и перевести completion/post-AskEnd на known/unknown decision-loop.
- [x] 1.2 Добавить `RuntimeTerminalState`, обновить ownership в `ErrorCoordinator` и hardening stop-reason/reset-signal.
- [x] 1.3 Исправить `TagWaiter.WaitForFalseAsync` raw-cache recheck'ом после subscribe/resume.
- [x] 1.4 Сохранить ownership shared `IModbusDispatcher` в `ConnectionTestPanel`.
- [x] 1.5 Добавить unit-test проект и покрыть ключевые helper/runtime инварианты.

## 2. Verification

- [x] 2.1 Обновить stable docs, active impact и change trail.
- [x] 2.2 Запустить `dotnet build Final_Test_Hybrid.slnx`.
- [x] 2.3 Запустить `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj`.
- [x] 2.4 Запустить `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx`.
- [x] 2.5 Запустить `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx`.
- [x] 2.6 Запустить `jb inspectcode` по изменённым `*.cs` с `-e=WARNING` и `-e=HINT`.
- [x] 2.7 Запустить `openspec validate fix-runtime-terminal-race-package --strict --no-interactive`.
