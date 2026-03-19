# 2026-03-19 post-askend-reset-decision

## Контур

- PLC reset / PreExecution / completion UI / changeover
- ErrorCoordinator / TagWaiter / diagnostic shared dispatcher ownership

## Что изменено

- Для PLC reset путь после `AskEnd` переведён в отдельный post-AskEnd decision flow.
- На `AskEnd` система больше не делает немедленный cleanup и не завершает reset-window.
- После `AskEnd` показывается `red_smile`, затем:
  - при `Req_Repeat=true` пишется `AskRepeat=true`;
  - если тест уже шёл, цикл переводится в существующий repeat path;
  - если тест не шёл, выполняется обычный cleanup.
- Если repeat не выбран:
  - при активном тесте показывается диалог причины прерывания;
  - cleanup выполняется только после штатного завершения диалога;
  - при отсутствии активного теста cleanup выполняется сразу.
- Новый reset во время post-AskEnd flow отменяет предыдущую ветку, скрывает `red_smile` и закрывает активный диалог причины.
- Серийный latch окна причины переработан:
  - `Save` или `Cancel` завершают серию и запрещают повторный показ окна в этой серии;
  - принудительное закрытие диалога новым soft reset право на показ не расходует;
  - следующий `AskEnd` после reset-cancel снова может открыть окно причины.
- В `InterruptReasonDialog` добавлена явная кнопка `Отмена`; она завершает серию так же, как и успешное сохранение.
- Changeover для PLC soft reset больше не стартует непосредственно от `AskEnd`; он привязан к финальному cleanup path и не участвует в repeat-сценарии.
- `ScanModeController` больше не трактует `OnResetCompleted` как немедленный финал PLC soft reset:
  - ранний restart scan timing/session откладывается, пока post-AskEnd flow активен;
  - возврат scanner-ready состояния выполняется catch-up после завершения post-AskEnd ветки;
  - catch-up теперь различает `full cleanup` и `repeat`, чтобы repeat не поднимал scan timing/session раньше `_skipNextScan`.
- Для гонки `AskEnd -> OnResetCompleted` добавлен ранний guard в `HandleGridClear()`:
  - post-AskEnd flow помечается активным синхронно до первого `await`;
  - это блокирует ранний `HandlePlcResetCompleted()` и не даёт стереть `BoilerState`/barcode/context до repeat decision.
- Классификация PLC reset маршрута больше не опирается только на literal `ScanModeController.IsInScanningPhase`:
  - до логина оператора PLC reset принудительно идёт по soft-reset path;
  - это не даёт `PlcResetCoordinator` уйти в `ErrorCoordinator.Reset()` и убить post-AskEnd flow сразу после `AskEnd`;
  - pre-login `Req_Reset` теперь должен вести себя как reset в `ScanStep`: `red_smile` -> ожидание `Req_Repeat` или `AskEnd=false` -> cleanup.
- Повторный PLC reset во время активного/deferred post-AskEnd окна тоже остаётся soft-reset path:
  - второй `Req_Reset` больше не должен проваливаться в full reset только потому, что `ScanModeController` ещё держит `_isResetting = true`;
  - это сохраняет второй `AskEnd -> red_smile` вместо немедленного `Reset()` cleanup.
- Owner reset-подписок `PreExecutionCoordinator` отделён от lazy `AutoReady`-подписок:
  - startup поднимает только reset-path через `EnsureResetSignalsSubscribed()`;
  - `ChangeoverStartGate` и replay `AutoReady` остаются в обычном `EnsureSubscribed()`;
  - порядок старта изменён на `EnsureResetSignalsSubscribed()` -> `ResetSubscription.SubscribeAsync()`, чтобы источник `Req_Reset` не включался раньше owner `AskEnd/post-AskEnd` flow.
- Для changeover reset semantics добавлен отдельный latch:
  - `ChangeoverResetMode` вычисляется один раз на входе в `HandleStopSignal()` и сохраняется на весь reset-cycle;
  - stop и поздний restart changeover больше не зависят от повторного чтения mutable `FlowState.StopReason`.
- `BoilerState.IsTestRunning` в repeat-путях больше не считается завершённым раньше PLC outcome:
  - normal repeat -> `SetTestRunning(false)` перенесён в `HandleRepeatRequestedExit` / `HandleNokRepeatRequestedExit`;
  - repeat after reset -> `SetTestRunning(false)` выполняется в `StartRepeatAfterReset`.
- Engineer/UI gating и `MessageService` больше не используют только `PlcResetCoordinator.IsActive` как признак активного reset:
  - во время post-AskEnd окна дополнительно учитывается `PreExecutionCoordinator.IsPostAskEndFlowActive()`.
- `MessageService` подписан на `PreExecutionCoordinator.OnStateChanged`, чтобы сообщение reset немедленно пересчитывалось после `FinishPostAskEndFlow()` и не зависало на `Сброс теста...` после full cleanup.
- `ScanModeController` теперь поднимает собственный `OnStateChanged` после обработки `PreExecution.OnStateChanged`, чтобы consumers, слушающие только `ScanModeController`, не зависели от косвенных refresh-событий.
- Completion-flow после штатного завершения теста больше не зависит только от `_resetCts`:
  - linked CTS в `HandleTestCompletionAsync()` теперь включает и `_currentCts.Token`;
  - это закрывает зависание при non-PLC hard reset (`PlcConnectionLost -> ErrorCoordinator.Reset()`), который отменяет текущий цикл через `_currentCts.Cancel()`, но не вооружает PLC reset-window;
  - при потере связи во время картинки/ожидания `End=false` completion выходит в существующий `HardReset` path, а `HandleHardResetExit()` доходит до `OperationalReset` cleanup вместо зависшего заполненного грида.
- Completion-flow после штатного завершения теста теперь принимает PLC decision без дополнительной задержки после `End=true`:
  - сначала подтверждается запись `End=true`;
  - затем completion ждёт либо `Req_Repeat=true`, либо `End=false`;
  - если `Req_Repeat=true` приходит раньше сброса `End`, repeat запускается сразу, без ожидания `End=false`;
  - если PLC сбрасывает `End` и `Req_Repeat` не поднят, выполняется обычное завершение теста.
- Stable doc `CycleExitGuide.md` синхронизирован: completion handshake должен прерываться и PLC reset-токеном, и cycle CTS hard reset-пути.

### Дополнение: runtime terminal race package

- В `OpcUaSubscription` добавлен safe-read контракт `TryGetValue<T>(...)` для decision-loop'ов, где `unknown` нельзя трактовать как `false`.
- Completion и post-AskEnd больше не принимают PLC decision по пустому/invalid runtime-cache:
  - `Req_Repeat=true` и `End=false`/`AskEnd=false` учитываются только при known bool;
  - при `unknown` цикл продолжает ждать реальное PLC-значение, reset или cancel.
- `TagWaiter.WaitForFalseAsync` больше не может ложно завершиться после `SubscribeAsync()`/`Resume()` на пустом cache:
  - recheck после subscribe/resume переведён на raw-cache семантику;
  - generic `WaitGroup/WaitForAllTrue` в этот пакет не расширялись.
- Добавлен singleton `RuntimeTerminalState`:
  - `TestCompletionCoordinator` владеет `IsCompletionActive`;
  - `PreExecutionCoordinator` владеет `IsPostAskEndActive`;
  - `ErrorCoordinator` использует `HasTerminalHandshake` как owner terminal window.
- Для ownership interrupt-ов сужена граница `AutoReady`:
  - `AutoReady OFF` во время completion/post-AskEnd не поднимает `AutoModeDisabled`;
  - `AutoReady ON` резюмит только `CurrentInterrupt == AutoModeDisabled`;
  - `BoilerLock`, `PlcConnectionLost`, `TagTimeout` broad-resume не снимаются.
- `PreExecutionCoordinator` получил hardening `_pendingExitReason` и `_resetSignal`:
  - `_pendingExitReason` переведён в атомарный sentinel;
  - `_resetSignal` читается через local snapshot на цикл;
  - fallback stop-reason теперь идёт через единый resolver перед `PipelineCancelled` / `SoftReset`.
- `ConnectionTestPanel.DisposeAsync()` больше не гасит shared `IModbusDispatcher`, если панель его не стартовала.
- Manual screens и write-path'ы (`HandProgram`, `IoEditorDialog`, `AiCallCheck`, `PidRegulatorCheck`, `RtdCalCheck`) этим пакетом не меняются и остаются доступными во время runtime.
- Добавлен unit-test проект `Final_Test_Hybrid.Tests` с покрытием helper/runtime инвариантов:
  - `OpcUaSubscription.TryGetValue`;
  - `TagWaiter.WaitForFalseAsync`;
  - `RuntimeTerminalState`;
  - ownership `ErrorCoordinator`.
- Для change trail создан `openspec/changes/fix-runtime-terminal-race-package/`.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionDependencies.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Changeover.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.CycleExit.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Subscriptions.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.PostAskEnd.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.MainLoop.cs`
- `Final_Test_Hybrid/Services/Main/PlcReset/PlcResetCoordinator.cs`
- `Final_Test_Hybrid/Services/Main/Messages/MessageService.cs`
- `Final_Test_Hybrid/Form1.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Subscriptions.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Pipeline.Helpers.cs`
- `Final_Test_Hybrid/Components/Main/Modals/Interrupt/InterruptReasonDialog.razor`
- `Final_Test_Hybrid/Docs/runtime/PlcResetGuide.md`
- `Final_Test_Hybrid/Docs/execution/CycleExitGuide.md`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.Flow.cs`
- `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.Callbacks.cs`
- `Final_Test_Hybrid/Services/OpcUa/TagWaiter.cs`
- `Final_Test_Hybrid/Services/OpcUa/TagWaiter.WaitGroup.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/RuntimeTerminalState.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.Interrupts.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.Resolution.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinatorDependencies.cs`
- `Final_Test_Hybrid/Services/DependencyInjection/StepsServiceExtensions.cs`
- `Final_Test_Hybrid/Components/Overview/ConnectionTestPanel.razor`
- `Final_Test_Hybrid/Docs/runtime/ErrorCoordinatorGuide.md`
- `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md`
- `Final_Test_Hybrid/Docs/runtime/ScanModeControllerGuide.md`
- `Final_Test_Hybrid/Docs/diagnostics/DiagnosticGuide.md`
- `Final_Test_Hybrid/Docs/runtime/TagWaiterGuide.md`
- `Final_Test_Hybrid.Tests/*`
- `Final_Test_Hybrid.slnx`
- `openspec/changes/fix-runtime-terminal-race-package/*`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — не выполнен: `Final_Test_Hybrid.exe` был заблокирован запущенным процессом `Final_Test_Hybrid (24876)`, MSB3027/MSB3021 на copy `apphost.exe -> bin\\Debug\\net10.0-windows\\Final_Test_Hybrid.exe`.
- `dotnet build Final_Test_Hybrid.slnx -p:BaseOutputPath=... -p:BaseIntermediateOutputPath=...` — неуспешно: в workspace уже присутствуют дубли generated-атрибутов/`ValidatableTypeAttribute` из `obj\\Release` и альтернативного `obj`; это отдельный build-blocker окружения, не указывает на новый compile-error в правке completion-flow.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.MainLoop.cs" --no-build --format=Text "--output=inspect-warning-completion-hard-reset.txt" -e=WARNING` — без warning.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.MainLoop.cs" --no-build --format=Text "--output=inspect-hint-completion-hard-reset.txt" -e=HINT` — без hint после удаления trailing comma.
- `dotnet build Final_Test_Hybrid.slnx` после правки completion handshake — неуспешно по той же внешней причине: `Final_Test_Hybrid.exe` остаётся заблокирован процессом `Final_Test_Hybrid (24876)`, MSB3027/MSB3021 на copy `apphost.exe -> bin\\Debug\\net10.0-windows\\Final_Test_Hybrid.exe`.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` после правки completion handshake — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` после правки completion handshake — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.Flow.cs" --no-build --format=Text "--output=inspect-warning-completion-repeat-decision.txt" -e=WARNING` — без warning по отчёту.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.Flow.cs" --no-build --format=Text "--output=inspect-hint-completion-repeat-decision.txt" -e=HINT` — без hint по отчёту.
- `dotnet build Final_Test_Hybrid.slnx` после runtime-terminal пакета — успешно; baseline warning только `MSB3277` по `WindowsBase`.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj` — успешно, 8/8.
- `dotnet build Final_Test_Hybrid.slnx` после финального cleanup `TestCompletionCoordinator` — успешно; baseline warning только `MSB3277` по `WindowsBase`.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj` после финального cleanup `TestCompletionCoordinator` — успешно, 8/8.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` после финального cleanup `TestCompletionCoordinator` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` после финального cleanup `TestCompletionCoordinator` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx` по списку изменённых `*.cs` с `-e=WARNING` — без warning после выноса `EmptyWaitGroupMessage`.
- `jb inspectcode Final_Test_Hybrid.slnx` по списку изменённых `*.cs` с `-e=HINT` — только неблокирующие structural/style hint в затронутых runtime helper/service файлах; новых warning нет.
- `openspec validate fix-runtime-terminal-race-package --strict --no-interactive` — успешно.
- `dotnet build Final_Test_Hybrid.slnx` после rollback UI/runtime gating-среза — успешно; baseline warning только `MSB3277` по `WindowsBase`.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj` после rollback UI/runtime gating-среза — успешно, 8/8.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` после rollback UI/runtime gating-среза — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` после rollback UI/runtime gating-среза — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx` по списку изменённых `*.cs` с `-e=WARNING` после rollback UI/runtime gating-среза — отчёт пуст (`Solution Final_Test_Hybrid.slnx`).
- `jb inspectcode Final_Test_Hybrid.slnx` по списку изменённых `*.cs` с `-e=HINT` после rollback UI/runtime gating-среза — только неблокирующие structural/style hint; новых warning нет.
- `openspec validate fix-runtime-terminal-race-package --strict --no-interactive` после rollback UI/runtime gating-среза — успешно.

## Инциденты

- Confirmed failure modes, зафиксированные этим пакетом:
  - stale-cache false-finish / false-cleanup в completion и post-AskEnd;
  - false-success `TagWaiter.WaitForFalseAsync` после subscribe/resume на пустом cache;
  - shared dispatcher ownership в `ConnectionTestPanel`.
- Так как отдельного incident-контура в `Docs` нет, change trail вынесен в `openspec/changes/fix-runtime-terminal-race-package/` и должен поддерживаться вместе с этим impact.
