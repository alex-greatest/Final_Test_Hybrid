# 2026-03-21 scanner-ownership-unified-with-boiler-info

## Контур

- raw scanner / PreExecution / `BoilerInfo` / scanner-dialog ownership
- PLC reset / non-PLC hard reset / scan-mode readiness

## Что изменено

- Добавлен единый router ownership `ScannerInputOwnershipService`:
  - ordinary raw scanner теперь маршрутизируется только через него;
  - service держит три состояния owner: `PreExecution`, `Dialog`, `None`;
  - scanner-dialog больше не владеют raw input напрямую через `RawInputService.RequestScan(...)`.
- `ScanSessionManager` переведён с прямого raw session lease на ownership-service:
  - ordinary scan-mode поднимает только `PreExecution` owner;
  - release ordinary scan-mode теперь снимает только `PreExecution` owner.
- `ScanModeController` при PLC reset снимает весь scanner ownership через `ReleaseAllForReset()`:
  - это закрывает окно, где barcode мог прийти в stale dialog-owner или в пустой handler-state;
  - возврат ordinary scanner-ready по-прежнему идёт только через действующий reset/post-AskEnd lifecycle.
- `BoilerInfo.razor` получил новый gating contract:
  - editable состояние теперь требует не только `IsAcceptingInput`, `IsConnected` и scan-mode, но и активного `PreExecution` owner;
  - если scanner ownership у dialog-mode или reset ещё не завершён, `BoilerInfo` не выглядит ordinary-ready.
- `UserScanDialog` и QR-ветка `AdminAuthDialog` переведены на явный `dialog-mode`:
  - вход — через `AcquireDialogOwner(dialogKey, handler)`;
  - штатный close/cancel/success — через явный `ReleaseDialogOwner(dialogKey)`;
  - `Dispose()` остаётся fail-safe, но больше не является единственным release-path.
- Для reset/close hooks вне `ScanModeController` добавлен узкий release dialog-owner:
  - `BoilerInfo.CloseDialogs()` и `ReworkDialogService` снимают только dialog owners;
  - это особенно важно для non-PLC hard reset, где active dialog должен закрыться сразу, но ordinary `PreExecution` owner не должен теряться от одного UI-close hook.
- `OperatorInfo` закрыл неявные close-path для `UserScanDialog`:
  - отключены `Esc` и крестик, чтобы dialog ownership не зависел от пассивного принудительного закрытия.
- Диагностика barcode routing усилена:
  - `ScannerInputOwnershipService` пишет `scanner_owner_changed`, `barcode_dispatched_to_owner`, `barcode_rejected_no_owner`;
  - `BarcodeDispatcher` больше не молчит при отсутствии и session, и fallback handler.
- Добавлено unit coverage на новый ownership-service:
  - barcode идёт в `PreExecution`;
  - dialog owner перехватывает barcode и release возвращает routing в `PreExecution`;
  - stale release не сбрасывает текущий dialog owner;
  - completed barcode без owner логируется как `barcode_rejected_no_owner`.

## Контракт и совместимость

- Новый source-of-truth контракт:
  - `BoilerInfo editable` <=> ordinary raw scanner доставляется в `PreExecution`;
  - `BoilerInfo blocked` => raw scanner не должен запускать ordinary pre-execution flow.
- Явный scanner-dialog остаётся допустимым исключением:
  - при active `dialog-mode` barcode идёт в dialog handler;
  - ordinary `BoilerInfo` в этот момент не должен выглядеть scanner-ready.
- PLC reset и non-PLC hard reset не смешиваются:
  - PLC reset снимает весь scanner ownership в `ScanModeController`;
  - non-PLC hard reset обязан немедленно снять только dialog ownership через UI/reset hooks;
  - возврат ordinary readiness в обоих случаях по-прежнему определяется существующим reset lifecycle, а не фактом закрытия окна.
- Таймерный контур не переписан:
  - change-set не меняет semantics `AutoReadySubscription`;
  - change-set не меняет post-AskEnd ownership;
  - новый ownership-state только перестаёт ложно сигнализировать ordinary scanner-ready.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScannerInputOwnershipService.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanSessionManager.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs`
- `Final_Test_Hybrid/Services/DependencyInjection/StepsServiceExtensions.cs`
- `Final_Test_Hybrid/Services/Scanner/RawInput/Processing/BarcodeDispatcher.cs`
- `Final_Test_Hybrid/Components/Main/BoilerInfo.razor`
- `Final_Test_Hybrid/Components/Main/Modals/UserScanDialog.razor`
- `Final_Test_Hybrid/Components/Main/Modals/Rework/AdminAuthDialog.razor`
- `Final_Test_Hybrid/Components/Main/OperatorInfo.razor`
- `Final_Test_Hybrid/Services/SpringBoot/Operation/ReworkDialogService.cs`
- `Final_Test_Hybrid/Docs/diagnostics/ScannerGuide.md`
- `Final_Test_Hybrid/Docs/runtime/ScanModeControllerGuide.md`
- `Final_Test_Hybrid/Docs/runtime/PlcResetGuide.md`
- `Final_Test_Hybrid.Tests/Runtime/ScannerInputOwnershipServiceTests.cs`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остались baseline warning `MSB3277` по конфликту `WindowsBase`.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~ScannerInputOwnershipServiceTests|FullyQualifiedName~PreExecutionHardResetScannerTests|FullyQualifiedName~BarcodeDebounceHandlerTests"` — успешно.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~ScannerInputOwnershipServiceTests|FullyQualifiedName~PreExecutionHardResetScannerTests|FullyQualifiedName~BarcodeDebounceHandlerTests|FullyQualifiedName~PostAskEndDecisionLoopTests|FullyQualifiedName~CompletionDecisionLoopTests|FullyQualifiedName~PreExecutionStopReasonTests|FullyQualifiedName~ErrorCoordinatorOwnershipTests|FullyQualifiedName~MessageServiceResolverTests|FullyQualifiedName~RuntimeTerminalStateTests"` — успешно, `31/31`.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScannerInputOwnershipService.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanSessionManager.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs;Final_Test_Hybrid/Services/DependencyInjection/StepsServiceExtensions.cs;Final_Test_Hybrid/Services/Scanner/RawInput/Processing/BarcodeDispatcher.cs;Final_Test_Hybrid/Services/SpringBoot/Operation/ReworkDialogService.cs;Final_Test_Hybrid.Tests/Runtime/ScannerInputOwnershipServiceTests.cs" --no-build --format=Text "--output=.codex-build/inspect-warning-scanner-ownership.txt" -e=WARNING` — новых warning по ownership/runtime fix не выявлено; в соседнем `ReworkDialogService` остались старые warning про неиспользуемые getter-ы DTO (`IsSuccess`, `AdminUsername`, `Data`), контур этой задачи их не менял.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScannerInputOwnershipService.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanSessionManager.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs;Final_Test_Hybrid/Services/Scanner/RawInput/Processing/BarcodeDispatcher.cs;Final_Test_Hybrid/Services/SpringBoot/Operation/ReworkDialogService.cs" --no-build --format=Text "--output=.codex-build/inspect-hint-scanner-ownership.txt" -e=HINT` — только неблокирующие structural/logging hints:
  - `ReworkDialogService` (`private` visibility / DTO accessor hints / `GC.SuppressFinalize` suggestion),
  - `ScanModeController.IsInScanningPhase` unused,
  - `ScannerInputOwnershipService` logging-cost suggestions,
  - `ScanSessionManager.Dispose()` `GC.SuppressFinalize` suggestion.

## Residual Risks

- В этой сессии нет интерактивного WinForms + Blazor Hybrid прогона сценариев:
  - `BoilerInfo editable -> scanner submit`;
  - `scanner-dialog -> soft reset`;
  - `scanner-dialog -> non-PLC hard reset`.
  Контракт подтверждён кодом и unit/regression tests, но окно приложения вручную не прогонялось.

## Инциденты

- no new incident
