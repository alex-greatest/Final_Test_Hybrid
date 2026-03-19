# 2026-03-19 post-askend-reset-decision

## Контур

- PLC reset / PreExecution / completion UI / changeover

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
- Stable doc `CycleExitGuide.md` синхронизирован: completion handshake должен прерываться и PLC reset-токеном, и cycle CTS hard reset-пути.

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

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — не выполнен: `Final_Test_Hybrid.exe` был заблокирован запущенным процессом `Final_Test_Hybrid (24876)`, MSB3027/MSB3021 на copy `apphost.exe -> bin\\Debug\\net10.0-windows\\Final_Test_Hybrid.exe`.
- `dotnet build Final_Test_Hybrid.slnx -p:BaseOutputPath=... -p:BaseIntermediateOutputPath=...` — неуспешно: в workspace уже присутствуют дубли generated-атрибутов/`ValidatableTypeAttribute` из `obj\\Release` и альтернативного `obj`; это отдельный build-blocker окружения, не указывает на новый compile-error в правке completion-flow.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.MainLoop.cs" --no-build --format=Text "--output=inspect-warning-completion-hard-reset.txt" -e=WARNING` — без warning.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.MainLoop.cs" --no-build --format=Text "--output=inspect-hint-completion-hard-reset.txt" -e=HINT` — без hint после удаления trailing comma.

## Инциденты

- no new incident
