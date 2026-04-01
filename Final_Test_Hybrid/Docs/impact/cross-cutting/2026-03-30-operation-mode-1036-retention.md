# 2026-03-30 operation mode 1036 retention

## Контур

- Execution steps `Coms/CH_Start_Max_Heatout`, `Coms/CH_Start_Min_Heatout`, `Coms/CH_Start_ST_Heatout`, `Coms/CH_Reset`
- Diagnostics / Modbus runtime services
- Reset / completion / repeat lifecycle

## Что изменено

- Добавлен singleton `BoilerOperationModeRefreshService` в diagnostic runtime.
- Сервис хранит последний подтверждённый шагом режим `1036` как raw `ushort` и повторно подтверждает его через системные `RegisterWriter` / `RegisterReader` по интервалу `Diagnostic:OperationModeRefreshInterval` (по умолчанию `15 минут`).
- Refresh выполняется только при `dispatcher ready` (`IsStarted && IsConnected && !IsReconnecting && LastPingData != null`).
- При потере связи без reset retained-state не очищается и не пытается поднять диагностику сам; запись откладывается до восстановления ready-state.
- После post-review hardening runtime-refresh и step-owned write/read/clear по `1036` используют shared mode-change lease, чтобы stale refresh не мог вклиниться между шаговой записью, verify и `ArmMode(...)` / `Clear(...)`.
- После post-review hardening failed refresh использует отдельный slow retry `5 секунд`, а не общий `WriteVerifyDelayMs` step pacing.
- После post-review hardening wakeup worker-а больше не бросает `SemaphoreFullException` / `ObjectDisposedException` при concurrent signal/dispose.
- После follow-up hardening `Clear(...)` оставлен sync-path: он инвалидирует retained-state сразу и запускает coalesced background-drain без накопления fire-and-forget задач.
- Добавлен `ClearAndDrainAsync(...)`, который завершает cleanup только после выхода активного refresh из shared mode-change critical section.
- После review-fix `ClearAndDrainAsync(...)` теперь уважает caller `CancellationToken` на всём ожидании drain/quiescence, а не только на входе в метод.
- После review-fix refresh дополнительно перепроверяет актуальность snapshot/token после захвата shared mode-change lease и перед каждым Modbus IO, чтобы invalidate в queued/ready-to-write окне не пропускал позднюю stale запись `1036`.
- Интервал refresh вынесен в `Diagnostic:OperationModeRefreshInterval`; при отсутствии или некорректном значении (`<= 0`) сервис откатывается к default `15 минут`.
- После follow-up hardening active `ChStartMaxHeatoutStep`, `ChStartMinHeatoutStep`, `ChStartStHeatoutStep` делают awaited `ClearAndDrainAsync(...)` на входе, поэтому предыдущий retained-mode больше не переживает новый step/retry/PLC-wait сценарий до нового arm.
- `TestExecutionCoordinator.CompleteAsync()` теперь при `ExecutionStopReason.Operator` ждёт `ClearAndDrainAsync(...)` до публикации `SequenceCompleted`.
- Источники arm:
  - `ChStartMaxHeatoutStep`;
  - `ChStartMinHeatoutStep`;
  - `ChStartStHeatoutStep`.
- Arm происходит сразу после успешного write/read-back `1036`.
- Источники clear:
  - `ChResetStep` только после подтверждённого `1036 == 0`;
  - `TestExecutionCoordinator.CompleteAsync()` при `ExecutionStopReason.Operator`;
  - `PlcResetCoordinator.OnForceStop`;
  - `ErrorCoordinator.OnReset`;
  - `BoilerState.OnCleared`;
  - `TestExecutionCoordinator.ResetForRepeat()`.
- `Form1.ConfigureDiagnosticEvents()` теперь заранее активирует сервис, чтобы runtime-hooks были подписаны сразу после старта приложения.
- Обновлены source-of-truth guides для steps/diagnostics/state management.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Diagnostic/Services/BoilerOperationModeRefreshService.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Connection/DiagnosticSettings.cs`
- `Final_Test_Hybrid/Services/DependencyInjection/DiagnosticServiceExtensions.cs`
- `Final_Test_Hybrid/Form1.cs`
- `Final_Test_Hybrid/appsettings.json`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.EventLoop.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.Execution.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChStartMaxHeatoutStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChStartMinHeatoutStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChStartStHeatoutStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChResetStep.cs`
- `Final_Test_Hybrid.Tests/Runtime/BoilerOperationModeRefreshServiceTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/BoilerOperationModeStepRetentionTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/TestExecutionCoordinatorCompletionTests.cs`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`
- `Final_Test_Hybrid/Docs/diagnostics/DiagnosticGuide.md`
- `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md`
- `Final_Test_Hybrid/Docs/changes/2026-03-30-operation-mode-1036-retention.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остаётся baseline warning `MSB3277` по `WindowsBase`.
- `dotnet build Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj` — успешно; остаются baseline warning `MSB3277` по `WindowsBase`.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~BoilerOperationMode"` — успешно, `28/28`.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~BoilerOperationMode|FullyQualifiedName~TestExecutionCoordinatorCompletionTests"` — успешно, `35/35`.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~TestExecutionCoordinatorCompletionTests"` — успешно, `5/5`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — см. текущий change-set.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — см. текущий change-set.
- `jb inspectcode` warning + hint по изменённым runtime `*.cs` — см. текущий change-set.

## Residual Risks

- Retained-state сознательно игнорирует ручные инженерные изменения режима и `SetStandModeAsync(...)`; источником истины остаётся только последний успешный шаговый write/read-back `1036`.
- Operator stop теперь ждёт выхода активного shared mode-change участка до `SequenceCompleted`; это сознательный sequencing trade-off ради гарантированного cleanup retained-mode без stale refresh.
- Legacy `*Old*.cs` шаги не обновлялись и не получают новый lifecycle-контракт автоматически.

## Инциденты

- Новый failure mode истечения `1036` во время длительной наладки зафиксирован в `Docs/changes/2026-03-30-operation-mode-1036-retention.md`.
