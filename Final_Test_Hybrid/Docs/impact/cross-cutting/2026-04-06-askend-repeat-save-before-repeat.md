# 2026-04-06 AskEnd repeat save before repeat

## Контур

- PLC soft reset / post-`AskEnd` repeat
- interrupt reason dialog / admin auth
- runtime interrupt snapshot contract
- reset cleanup guardrails

## Что изменено

- В `PreExecutionCoordinator.PostAskEnd` путь `AskEnd -> Req_Repeat` для уже идущего теста больше не стартует repeat сразу.
- Перед `AskRepeat=true` теперь обязателен тот же interrupt-save flow, что и при полном прерывании:
  - ввод причины;
  - при `UseMes=true` авторизация администратора;
  - отправка причины и runtime snapshot через существующий save callback;
  - сразу после успешного `interrupt` запускается новая repeat-операция:
    `UseMes=true` -> `start operation` на сервере,
    `UseMes=false` -> новая локальная `TB_OPERATION` через existing DB init path.
- Новый pre-repeat gate включается только для `wasTestRunning=true`.
- Если `UseInterruptReason=false` или нет валидного `serialNumber`, сохранено прежнее прямое поведение без save gate.
- Для repeat-save режима dialog contract сделан opt-in:
  - в `AdminAuthDialog` скрывается кнопка `Отмена`;
  - в `InterruptReasonDialog` скрывается кнопка `Отмена`;
  - локальная cancel-ветка не завершает flow как success.
- Regression coverage усилен на самые рискованные post-`AskEnd` ветки:
  - `save fail => AskRepeat не пишется и repeat не стартует`;
  - `external soft reset во время repeat-save => flow отменяется без старта repeat`;
  - `UseMes repeat-save => порядок запросов строго interrupt -> start`;
  - `retry after start failure => interrupt не дублируется в рамках текущей dialog-сессии`;
  - direct rework-вызовы `AdminAuthDialog` сохраняют старую cancel-семантику через безопасные default-параметры.
- Для direct non-interrupt callers сохранена legacy-совместимость:
  - `AdminAuthDialog` по умолчанию оставляет `ShowCancelButton=true` и `RequireProtectedCancel=true`;
  - repeat-save path скрывает `Отмена` только явной передачей флагов через `InterruptDialogService`.
- При server/DB ошибке сохранения или при ошибке `start operation` reason dialog не закрывается, repeat не стартует, `AskRepeat=true` не пишется.
- При внешнем `soft reset` repeat-save окно закрывается через существующий cancellation path; repeat не запускается.
- Обычный full-interrupt flow не изменён:
  - cancel-кнопка остаётся;
  - protected cancel в admin auth остаётся;
  - post-`AskEnd` cleanup path по non-repeat остаётся прежним.

## Контракт и совместимость

- `StartRepeatAfterReset(...)` не менял семантику; меняется только момент, когда до него допускается управление.
- Допуск к `AskRepeat=true` теперь требует два последовательных шага:
  1. закрыть текущую `InWork` операцию через `interrupt`;
  2. открыть новую repeat-операцию.
- Для `UseMes=true` второй шаг идёт через server `start`.
- Для `UseMes=false` второй шаг идёт через local `InitializeDatabaseAsync()` -> `BoilerDatabaseInitializer` -> `OperationService.CreateAsync()`.
- Порядок `interrupt -> start/create` зафиксирован намеренно: обратный порядок опасен, потому что interrupt-storage закрывает последнюю `InWork` операцию по serial.
- Повторный submit после fail `start` не шлёт второй `interrupt` в рамках той же открытой dialog-сессии; retry повторяет только `start`.
- `FinalizeResetCleanup(...)`, `HandleChangeoverAfterInterrupt(...)` и full cleanup не вызываются из repeat-save gate до успешного сохранения.
- Completion repeat и `NOK repeat` не менялись.
- `StepTimingService` не менялся.
- `BoilerState` timers (`TestTime`, `ChangeoverTime`) не менялись.
- Changeover semantics не менялась.
- Новый gate переиспользует существующий interrupt-save контракт и не вводит второго пути сборки snapshot.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.PostAskEnd.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Subscriptions.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanDialogCoordinator.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/ScanBarcodeMesStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/ScanBarcodeStep.cs`
- `Final_Test_Hybrid/Services/SpringBoot/Operation/Interrupt/InterruptDialogService.cs`
- `Final_Test_Hybrid/Services/SpringBoot/Operation/Interrupt/InterruptFlowExecutor.cs`
- `Final_Test_Hybrid/Components/Main/BoilerInfo.razor`
- `Final_Test_Hybrid/Components/Main/Modals/Interrupt/InterruptReasonDialog.razor`
- `Final_Test_Hybrid/Components/Main/Modals/Rework/AdminAuthDialog.razor`
- `Final_Test_Hybrid/Docs/runtime/PlcResetGuide.md`
- `Final_Test_Hybrid/Docs/execution/CycleExitGuide.md`
- `Final_Test_Hybrid.Tests/Runtime/InterruptFlowExecutorTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/PostAskEndRepeatSaveTests.cs`

## Проверки

- `dotnet msbuild .\Final_Test_Hybrid\Final_Test_Hybrid.csproj /t:CoreCompile /p:Configuration=Debug` — passed
- `dotnet build .\Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj /p:BuildProjectReferences=false` — passed
- `dotnet test .\Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~PostAskEndRepeatSaveTests"` — passed (`7/7`)
- `dotnet test .\Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~InterruptFlowExecutorTests|FullyQualifiedName~PostAskEndRepeatSaveTests"` — passed (`11/11`)
- `dotnet build Final_Test_Hybrid.slnx` — passed
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — passed
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — passed
- `jb inspectcode Final_Test_Hybrid.slnx ... -e=WARNING` по changed `.cs` — passed, warning-level findings не зафиксированы
- `jb inspectcode Final_Test_Hybrid.slnx ... -e=HINT` по changed `.cs` — suggestion/hint only, новых runtime findings не выявлено

Фактический статус ограничений среды:

- `dotnet build Final_Test_Hybrid.slnx` проходит, но среда продолжает печатать существующие `MSB3277` warnings по конфликту `WindowsBase 4.0.0.0` vs `5.0.0.0`; текущий change-set их не вводил.

## Residual Risks

- Pending-state для уже отправленного `interrupt` живёт только в памяти процесса: внешний `soft reset` внутри одной runtime-сессии покрыт, но crash/restart приложения между успешным `interrupt` и успешным `start` оставит recovery на следующий запуск вне текущего контракта.
- Поведение repeat-save gate покрыто unit-тестами; полноценного ручного прогона `AskEnd -> Req_Repeat -> external soft reset` в UI в этой сессии нет.

## Инциденты

- no new incident
