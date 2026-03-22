# 2026-03-22 autoready-log-dedup

## Контур

- ErrorCoordinator / AutoReady observability
- Runtime log noise cleanup

## Что изменено

- Убран spam логов `AutoReady ON -> resume` и `AutoReady OFF -> pause` при повторных OPC callback'ах с тем же значением `Ask_Auto`.
- В `ErrorCoordinator.HandleAutoReadyChanged()` логирование и no-op dispatch теперь происходят только для meaningful action:
  - `AutoReady ON -> resume` пишется только если реально активна пауза `AutoModeDisabled`;
  - `AutoReady OFF -> pause` пишется только при первом валидном входе в `AutoModeDisabled`;
  - duplicate callback `true -> true` или `false -> false` больше не создаёт повторный log-spam.
- Runtime-семантика не менялась:
  - `AutoReadySubscription.OnStateChanged` по-прежнему вызывается на каждый callback;
  - `AutoReady ON/OFF. OpcConnected=... RawValue=...` transition-log в `AutoReadySubscription` сохранён;
  - OPC subscription layer и delivery одинаковых значений не менялись.
- Добавлены тесты на отсутствие повторных side effect при duplicate `AutoReady` callback'ах:
  - повторный `AutoReady=true` после recovery не вызывает второй recovery-path;
  - повторный `AutoReady=false` при уже активном `AutoModeDisabled` не вызывает повторный interrupt-path.

## Что сознательно не менялось

- `PauseToken`, `CurrentInterrupt`, `FireAndForgetResume`, `FireAndForgetInterrupt`.
- `AutoReadySubscription` и его контракт `OnStateChanged`.
- `OpcUaSubscription.Callbacks` и глобальный dedup одинаковых OPC values.
- Ownership-модель `BoilerLock`, `PlcConnectionLost`, terminal handshake и residual gap `BoilerLock -> AutoReady OFF -> AutoReady ON`.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.cs`
- `Final_Test_Hybrid.Tests/Runtime/ErrorCoordinatorOwnershipTests.cs`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; baseline warning только `MSB3277` по конфликту `WindowsBase 4.0.0.0` vs `5.0.0.0`.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj` — успешно, `126/126`.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.cs;Final_Test_Hybrid.Tests/Runtime/ErrorCoordinatorOwnershipTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-autoready-log-dedup.txt" -e=WARNING` — отчёт пуст (`Solution Final_Test_Hybrid.slnx`).
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.cs;Final_Test_Hybrid.Tests/Runtime/ErrorCoordinatorOwnershipTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-autoready-log-dedup.txt" -e=HINT` — отчёт пуст (`Solution Final_Test_Hybrid.slnx`).

## Инциденты

- no new incident
