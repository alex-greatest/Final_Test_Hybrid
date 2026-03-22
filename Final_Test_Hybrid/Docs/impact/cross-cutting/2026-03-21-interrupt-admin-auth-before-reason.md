# 2026-03-21 interrupt admin auth before reason

## Контур

- PLC soft reset / post-AskEnd interrupt reason flow
- MES interrupt save path
- admin auth dialog / scanner dialog ownership

## Что изменено

- Убран временный bypass soft-reset interrupt-flow:
  - `UseMes=true` снова требует admin-auth перед окном причины;
  - `UseMes=false` остаётся direct reason path без auth.
- `InterruptDialogService` теперь открывает `AdminAuthDialog` для interrupt-flow с opt-in параметрами:
  - показывать `Отмена`;
  - закрывать окно только через инженерный пароль.
- `AdminAuthDialog` получил protected-cancel ветку:
  - парольная и QR-ветка используют один и тот же close-path;
  - `ScannerInputOwnershipService.ReleaseDialogOwner(...)` вызывается и при success, и при cancel;
  - rework-flow не меняет свой базовый сценарий, потому что opt-in cancel включается только из interrupt dialog service.
- `PreExecutionCoordinator.Subscriptions` снова использует обычное правило:
  - `requireAdminAuth = UseMes`.
- Двухшаговый runtime-orchestration не переписан:
  - `AdminAuthDialog -> InterruptReasonDialog`;
  - owner по-прежнему `ScanDialogCoordinator -> BoilerInfo -> InterruptDialogService -> InterruptFlowExecutor`.
- Добавлены unit-tests на `InterruptFlowExecutor`, фиксирующие выбор submit identity:
  - MES path -> `admin username`;
  - non-MES path -> `operator username`;
  - cancel auth dialog не открывает reason dialog.
- Stable docs синхронизированы:
  - `Docs/runtime/PlcResetGuide.md`
  - `Docs/diagnostics/ScannerGuide.md`

## Контракт и совместимость

- `UseMes=true` в interrupt-flow:
  - admin-auth обязательна;
  - только после `200 OK` открывается окно причины;
  - submit в MES использует `username` администратора;
  - новый запуск после `Cancel` снова начинается с admin-auth.
- `UseMes=false`:
  - поведение interrupt reason не меняется.
- Server-driven admin contract остаётся прежним:
  - `/api/admin/auth`, `/api/admin/auth/Qr`;
  - `200` -> success;
  - `404` -> остаться в auth dialog и показать server message;
  - прочие статусы -> остаться в auth dialog и показать `Неизвестная ошибка`.
- Runtime reset/scanner semantics не менялись:
  - post-AskEnd lifecycle;
  - reset sequence;
  - scanner reset/rearm;
  - cleanup path.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Subscriptions.cs`
- `Final_Test_Hybrid/Services/SpringBoot/Operation/Interrupt/InterruptDialogService.cs`
- `Final_Test_Hybrid/Components/Main/Modals/Rework/AdminAuthDialog.razor`
- `Final_Test_Hybrid/Docs/runtime/PlcResetGuide.md`
- `Final_Test_Hybrid/Docs/diagnostics/ScannerGuide.md`
- `Final_Test_Hybrid/Docs/changes/2026-03-21-interrupt-admin-auth-before-reason.md`
- `Final_Test_Hybrid.Tests/Runtime/InterruptFlowExecutorTests.cs`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остались только baseline warning `MSB3277` по конфликту `WindowsBase`.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~InterruptFlowExecutorTests|FullyQualifiedName~AdminAuthResponseParserTests|FullyQualifiedName~ScannerInputOwnershipServiceTests"` — итоговый прогон успешен, `10/10`.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Subscriptions.cs;Final_Test_Hybrid/Services/SpringBoot/Operation/Interrupt/InterruptDialogService.cs;Final_Test_Hybrid.Tests/Runtime/InterruptFlowExecutorTests.cs" --no-build --format=Text "--output=.codex-build/inspect-warning-interrupt-admin-auth.txt" -e=WARNING` — warning по change-set отсутствуют.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Subscriptions.cs;Final_Test_Hybrid/Services/SpringBoot/Operation/Interrupt/InterruptDialogService.cs" --no-build --format=Text "--output=.codex-build/inspect-hint-interrupt-admin-auth.txt" -e=HINT` — только неблокирующие hints:
  - `InterruptDialogService.CloseDialog` пока не переопределяется;
  - suggestion по уменьшению вложенности/inline temporary variable в `PreExecutionCoordinator.Subscriptions.cs`.

## Residual Risks

- Интерактивный WinForms + Blazor Hybrid прогон password/QR ветки interrupt auth dialog в этой сессии не доказывается автоматически; покрыт orchestration и server contract, но не полный UI runtime.

## Инциденты

- no new incident
