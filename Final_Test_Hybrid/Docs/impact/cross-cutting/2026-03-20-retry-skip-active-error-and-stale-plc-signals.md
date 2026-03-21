# 2026-03-20 retry skip active error and stale plc signals

## Контур

- Execution retry/skip queue ownership
- PLC retry stale-signal guard
- Pre-execution `BlockBoilerAdapterStep` retry
- Runtime/source-of-truth documentation

## Что изменено

- В execution retry-path добавлен coordinator-level freshness guard после `WaitForRetrySignalResetAsync(...)`:
  - guard не меняет глобальный `TagWaiter`;
  - ждёт только known stale `Block.Error=true`, затем `Block.End=true`;
  - при timeout уводит в существующий fail-fast `TagTimeout` path;
  - `Retry` по-прежнему не пишет `Start=false`.
- В execution error-resolution введён явный active error context на время открытого диалога.
- `Retry` и `Skip` больше не снимают очередь через blind `DequeueError()`:
  - используется адресное `TryRemoveError(error)`;
  - сохраняется порядок остальных pending ошибок;
  - инвариант `Skip`: удаление ошибки до `ClearFailedState()` сохранён.
- В `ExecutionStateManager` добавлено адресное удаление ошибки с сохранением FIFO порядка остальных элементов.
- В pre-execution retry для `BlockBoilerAdapterStep` добавлен отдельный freshness guard между `SendAskRepeatAsync(...)` и повторным запуском шага.
- В pre-execution retry для `BlockBoilerAdapterStep` восстановлен полный retry-handshake:
  - `SendAskRepeatAsync(...)`;
  - обязательное ожидание `Req_Repeat=false`;
  - затем freshness guard и повторный запуск шага.
- В execution retry добавлено suppression-состояние по колонке на период `RetryRequested -> RetryCompleted`, чтобы одна и та же ошибка не могла повторно попасть в очередь до фактического старта/завершения retry.
- `PreExecutionInfrastructure` расширен доступом к `PausableTagWaiter`, чтобы pre-execution guard использовал pause-aware wait path.
- Stable docs синхронизированы:
  - `Docs/execution/RetrySkipGuide.md`
  - `Docs/execution/StepsGuide.md`
  - `Docs/runtime/ErrorCoordinatorGuide.md`
  - `Docs/execution/StateManagementGuide.md`
  - `Docs/execution/CancellationGuide.md`

### Обновление 2026-03-21

- По явному решению пользователя coordinator-level freshness guard удалён и из execution retry-path, и из pre-execution retry `BlockBoilerAdapterStep`.
- Текущий контракт retry:
  - `SendAskRepeatAsync(...)`;
  - обязательное ожидание `Req_Repeat=false`;
  - затем немедленный rerun без фильтра stale `Block.Error/End`.
- `Skip`-ветка не менялась: для шагов с PLC-блоком по-прежнему ждёт `Block.Error=false` и `Block.End=false`.
- Мёртвый helper `PlcRetrySignalFreshnessGuard` и его тесты удалены.
- Stable docs и существующий change-doc обновлены под новый контракт.

## Затронутые файлы

- `Final_Test_Hybrid/Models/Steps/ExecutionStateManager.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionDependencies.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Retry.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Plc/PlcRetrySignalFreshnessGuard.cs`
- `Final_Test_Hybrid.Tests/Runtime/ExecutionStateManagerTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/PlcRetrySignalFreshnessGuardTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/PreExecutionRetryHandshakeTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/RetryCoordinationStateTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/TagWaiterWaitAnyAsyncTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/PostAskEndDecisionLoopTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/PreExecutionStopReasonTests.cs`
- `Final_Test_Hybrid/Docs/changes/2026-03-20-retry-skip-active-error-and-stale-plc-signals.md`
- `Final_Test_Hybrid/Docs/execution/RetrySkipGuide.md`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`
- `Final_Test_Hybrid/Docs/runtime/ErrorCoordinatorGuide.md`
- `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md`
- `Final_Test_Hybrid/Docs/execution/CancellationGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно.
- `dotnet test Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~ExecutionStateManagerTests|FullyQualifiedName~PlcRetrySignalFreshnessGuardTests|FullyQualifiedName~TagWaiterWaitAnyAsyncTests|FullyQualifiedName~TagWaiterWaitForFalseAsyncTests|FullyQualifiedName~PostAskEndDecisionLoopTests|FullyQualifiedName~PreExecutionStopReasonTests"` — успешно, 14/14.
- `dotnet test Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~RetryCoordinationStateTests|FullyQualifiedName~PreExecutionRetryHandshakeTests|FullyQualifiedName~ExecutionStateManagerTests|FullyQualifiedName~PlcRetrySignalFreshnessGuardTests|FullyQualifiedName~TagWaiterWaitAnyAsyncTests"` — успешно, 12/12.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx ... -e=WARNING` по изменённым `*.cs` — warning-level чистый отчёт (`artifacts/inspect-warning-retry-skip-2.txt`).
- `jb inspectcode Final_Test_Hybrid.slnx ... -e=HINT` по изменённым `*.cs` — только низкоприоритетные suggestions/hints:
  - существующие hint по лог-аргументам и unused API в coordinator/state manager;
  - suggestion про `GC.SuppressFinalize` в `TestExecutionCoordinator.Dispose()`;
  - один test-only hint про overload с cancellation support.

### Проверки 2026-03-21

- `dotnet build Final_Test_Hybrid.slnx` — успешно; сохранены существующие warnings `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0`.
- `dotnet test Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~PreExecutionRetryHandshakeTests|FullyQualifiedName~RetryCoordinationStateTests|FullyQualifiedName~TagWaiterWaitAnyAsyncTests|FullyQualifiedName~ExecutionStateManagerTests"` — успешно, 9/9.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Retry.cs;Final_Test_Hybrid.Tests/Runtime/PreExecutionRetryHandshakeTests.cs" --no-build --format=Text "--output=artifacts/inspect-warning-retry-rollback.txt" -e=WARNING` — warning-level чистый отчёт.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Retry.cs;Final_Test_Hybrid.Tests/Runtime/PreExecutionRetryHandshakeTests.cs" --no-build --format=Text "--output=artifacts/inspect-hint-retry-rollback.txt" -e=HINT` — только низкоприоритетные hints:
  - существующие подсказки про потенциально дорогие аргументы логирования в `TestExecutionCoordinator.ErrorResolution.cs` и `TestExecutionCoordinator.PlcErrorSignals.cs`;
  - suggestion `Invert 'if' statement to reduce nesting` в `TestExecutionCoordinator.PlcErrorSignals.cs`.

## Инциденты

- Новый failure mode зафиксирован в `Docs/changes/2026-03-20-retry-skip-active-error-and-stale-plc-signals.md`.
- `2026-03-21`: no new incident; обновлён существующий change-doc под текущий retry-контракт.
