# 2026-03-21 scanner-reset-rearm-after-plc-connection-loss

## Контекст

- Контур: pre-execution / scanner gating / PLC connection loss recovery
- Причина: после `PlcConnectionLost -> HardReset -> Reconnect` raw input продолжал писать `Barcode received`, но pre-execution мог не перевооружать фактическое ожидание barcode.
- Scope этого change-set умышленно узкий: без изменения `AutoReadySubscription`, `ScanModeController` semantics, `BoilerInfo` и `RawInputInterop`.

## Что изменено

- `PreExecutionCoordinator.BeginResetCycle(...)` теперь отменяет и сразу перевооружает reset-token/barcode-wait не только для PLC reset, но и для non-PLC HardReset.
- В `HandleHardReset()` добавлен debug-лог `non_plc_hard_reset_cancel_barcode_wait` с `origin`, `seq` и признаком активного barcode-wait.
- Для сценария `soft reset -> AskEnd -> post-AskEnd -> PlcConnectionLost/HardReset` добавлен explicit scanner-ready outcome:
  - non-PLC `HandleHardReset()` больше не гасит active `post-AskEnd` простым обнулением decision;
  - abort terminal-ветки публикует `full cleanup` outcome для deferred `ScanModeController` catch-up;
  - это закрывает зависание `BoilerInfo`/ordinary raw scanner после reconnect.
- `BarcodeDebounceHandler` получил debug-диагностику `barcode_drop`:
  - `reason=not_accepting_input`
  - `reason=opc_disconnected`
  - плюс текущие значения `isAcceptingInput` / `isConnected`.
- Source-of-truth docs обновлены:
  - `Docs/runtime/PlcResetGuide.md`
  - `Docs/runtime/ScanModeControllerGuide.md`
  - `Docs/diagnostics/ScannerGuide.md`

## Контракт и совместимость

- PLC soft reset / `AskEnd -> post-AskEnd -> repeat/full cleanup` этим change-set не меняется.
- Различие PLC reset и non-PLC HardReset сохраняется:
  - PLC path по-прежнему создаёт AskEnd-window;
  - non-PLC path по-прежнему не создаёт AskEnd-window.
- Изменён только контракт отмены текущего barcode-wait: теперь он симметричен для обоих reset-path.
- Новый explicit outcome применяется только к non-PLC hard reset во время active `post-AskEnd`.
- `repeat` path и completion terminal window этим update не меняются.
- После reconnect ordinary scanner-ready возвращается только при `AutoReady=true`; `AutoReady=false` по-прежнему держит `BoilerInfo` и raw scanner заблокированными.
- `AutoReadySubscription.OnFirstAutoReceived` и changeover ownership не менялись.

## Проверки

- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --no-restore --filter "FullyQualifiedName~PreExecutionHardResetScannerTests|FullyQualifiedName~BarcodeDebounceHandlerTests"` — passed.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~PreExecutionHardResetScannerTests|FullyQualifiedName~PostAskEndDecisionLoopTests|FullyQualifiedName~PreExecutionAutoReadyGateTests|FullyQualifiedName~BarcodeDebounceHandlerTests|FullyQualifiedName~BoilerInfoInputDraftTests|FullyQualifiedName~ErrorCoordinatorOwnershipTests"` — passed.
- Дополнительно после реализации должны оставаться зелёными regression-пакеты:
  - `PostAskEndDecisionLoopTests`
  - `CompletionDecisionLoopTests`
  - `PreExecutionStopReasonTests`
  - `ErrorCoordinatorOwnershipTests`
  - `MessageServiceResolverTests`
  - `RuntimeTerminalStateTests`
  - `BoilerInfoInputDraftTests`

## Incident

- `no new incident`
- Change-set закрывает несоответствие между кодом и уже действующим runtime contract для reset-cancel barcode wait.
