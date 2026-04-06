# 2026-04-06 repeat success scanner owner rearm

## Контур

- scanner ownership / `BoilerInfo`
- pre-execution wait-for-barcode
- repeat after reset

## Что изменено

- В `ScanModeController` добавлен узкий self-heal ordinary scanner owner.
- Self-heal выполняется только при фактическом возврате `PreExecutionCoordinator` в ожидание barcode:
  - `IsAcceptingInput=true`
  - `AutoReady=true`
  - PLC connected
  - reset не активен
  - controller активирован
  - текущий scanner owner = `None`
- Возврат ordinary owner выполняется только через `ScanSessionManager.AcquireSession(...)`.
- При `Dialog` owner self-heal не перехватывает scanner обратно в обычный режим.
- В `ScanSessionManager` устранён short-circuit по cached handler:
  повторный `AcquireSession(...)` теперь заново поднимает `PreExecution` owner, если reset уже снял ownership через `ReleaseAllForReset()`.
- В runtime logs добавлено отдельное сообщение для успешного rearm ordinary owner при возврате в ожидание barcode.
- Добавлено regression coverage:
  - позитивный сценарий `repeat -> success -> owner rearm`;
  - позитивный сценарий `same handler + owner dropped by reset -> session rearm`;
  - негативные сценарии для `Dialog owner`, `AutoReady=false`, `IsConnected=false`, `IsAcceptingInput=false`;
  - UI/runtime guardrail: при активном ordinary owner и готовом scan-step `BoilerLock` всё равно оставляет `BoilerInfo` в `read-only`.

## Контракт и совместимость

- Completion-handshake не менялся.
- `StepTimingService` не менялся.
- `BoilerState` timers (`test time`, `changeover time`) не менялись.
- Changeover logic не менялась.
- `BoilerInfo` gating не менялся:
  поле по-прежнему editable только если ordinary scanner реально доставляется в `PreExecution`.
- PLC reset / post-AskEnd repeat contract не менялся:
  `repeat` по-прежнему не поднимает ordinary scanner-ready немедленно в terminal окне reset-flow.
- Новый self-heal закрывает только gap после уже завершённого repeat-теста, когда система снова ждёт новый barcode, но owner остался `None`.
- Дополнительно зафиксированы stable guardrails:
  - `StepTimingService` владеет только scan/step timers;
  - `BoilerState` остаётся владельцем `TestTime` и `ChangeoverTime`;
  - ordinary raw scanner и `BoilerInfo` должны разблокироваться и блокироваться вместе;
  - scanner rearm не должен менять completion-handshake и existing changeover logic.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs`
- `Final_Test_Hybrid.Tests/Runtime/PreExecutionHardResetScannerTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/BoilerInfoInputDraftTests.cs`
- `Final_Test_Hybrid/Docs/runtime/ScanModeControllerGuide.md`
- `Final_Test_Hybrid/Docs/diagnostics/ScannerGuide.md`
- `Final_Test_Hybrid/Docs/execution/StepTimingGuide.md`
- `Final_Test_Hybrid/Docs/ui/MainScreenGuide.md`
- `AGENTS.md`
- `Final_Test_Hybrid/Docs/changes/2026-04-06-repeat-success-scanner-owner-not-rearmed.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx`
- целевые runtime tests по scanner/pre-execution/completion
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx`
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx`
- `jb inspectcode` warning по изменённым `*.cs`
- `jb inspectcode` hint по изменённым `*.cs`
- Повторная ручная сверка guardrail’ов в `AGENTS.md`, `StepTimingGuide.md`, `ScanModeControllerGuide.md`, `ScannerGuide.md`, `MainScreenGuide.md`

Фактический статус:
- `dotnet build Final_Test_Hybrid.slnx` — passed
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~PreExecutionHardResetScannerTests|FullyQualifiedName~ScanSessionManagerTests|FullyQualifiedName~ScannerInputOwnershipServiceTests|FullyQualifiedName~CompletionDecisionLoopTests|FullyQualifiedName~PostAskEndDecisionLoopTests|FullyQualifiedName~BoilerInfoInputDraftTests"` — passed (`25/25`)
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — passed
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — passed
- `jb inspectcode` warning — no findings
- `jb inspectcode` hint — только существующие suggestion-level замечания по `GC.SuppressFinalize(...)` и неиспользуемому `IsInScanningPhase`; новых runtime-findings по change-set нет

## Residual Risks

- Self-heal опирается на `PreExecutionCoordinator.OnStateChanged`; если в будущем контракт входа в ожидание barcode изменится без этого события, regression-test нужно расширить.
- Fix умышленно не трогает timing/changeover контуры; если рядом есть отдельный дефект таймеров, этот пакет его не закрывает.
- В build/test остаются существующие `WindowsBase` warnings; этот change-set их не вводил и не исправлял.

## Инциденты

- Новый подтверждённый failure mode зафиксирован в change-doc [2026-04-06-repeat-success-scanner-owner-not-rearmed.md](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Docs/changes/2026-04-06-repeat-success-scanner-owner-not-rearmed.md).
