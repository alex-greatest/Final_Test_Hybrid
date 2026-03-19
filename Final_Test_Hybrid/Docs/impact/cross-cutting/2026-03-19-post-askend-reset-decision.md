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
- Changeover для PLC soft reset больше не стартует непосредственно от `AskEnd`; он привязан к финальному cleanup path и не участвует в repeat-сценарии.
- `ScanModeController` больше не трактует `OnResetCompleted` как немедленный финал PLC soft reset:
  - ранний restart scan timing/session откладывается, пока post-AskEnd flow активен;
  - возврат scanner-ready состояния выполняется catch-up после завершения post-AskEnd ветки;
  - catch-up теперь различает `full cleanup` и `repeat`, чтобы repeat не поднимал scan timing/session раньше `_skipNextScan`.
- Для гонки `AskEnd -> OnResetCompleted` добавлен ранний guard в `HandleGridClear()`:
  - post-AskEnd flow помечается активным синхронно до первого `await`;
  - это блокирует ранний `HandlePlcResetCompleted()` и не даёт стереть `BoilerState`/barcode/context до repeat decision.
- Для changeover reset semantics добавлен отдельный latch:
  - `ChangeoverResetMode` вычисляется один раз на входе в `HandleStopSignal()` и сохраняется на весь reset-cycle;
  - stop и поздний restart changeover больше не зависят от повторного чтения mutable `FlowState.StopReason`.
- `BoilerState.IsTestRunning` в repeat-путях больше не считается завершённым раньше PLC outcome:
  - normal repeat -> `SetTestRunning(false)` перенесён в `HandleRepeatRequestedExit` / `HandleNokRepeatRequestedExit`;
  - repeat after reset -> `SetTestRunning(false)` выполняется в `StartRepeatAfterReset`.
- Engineer/UI gating и `MessageService` больше не используют только `PlcResetCoordinator.IsActive` как признак активного reset:
  - во время post-AskEnd окна дополнительно учитывается `PreExecutionCoordinator.IsPostAskEndFlowActive()`.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionDependencies.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Changeover.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.CycleExit.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Subscriptions.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.PostAskEnd.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs`
- `Final_Test_Hybrid/Docs/runtime/PlcResetGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно

## Инциденты

- no new incident
