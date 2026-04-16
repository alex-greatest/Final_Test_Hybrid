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
  - `Отмена` снова показывается внутри repeat-save flow;
  - `UseMes=true`:
    - `AdminAuthDialog` и `InterruptReasonDialog` возвращают новый outcome `RepeatBypass`;
    - bypass проходит через existing protected-cancel и не отправляет `interrupt` / `start operation` в MES;
  - `UseMes=false`:
    - новый admin-auth шаг не добавляется;
    - `InterruptReasonDialog` может вернуть `RepeatBypass` без local interrupt-save и без local DB operation create;
  - `RepeatBypass` не идёт в новый cleanup path: PC сразу пишет `AskRepeat=true` и продолжает через existing `StartRepeatAfterReset(...)`.
- Regression coverage расширен на самые рискованные post-`AskEnd` ветки:
  - `save fail => AskRepeat не пишется и repeat не стартует`;
  - `external soft reset во время repeat-save => flow отменяется без старта repeat`;
  - `UseMes repeat-save => порядок запросов строго interrupt -> start`;
  - `retry after start failure => interrupt не дублируется в рамках текущей dialog-сессии`;
  - `repeat bypass from admin auth => AskRepeat пишется без MES start/save`;
  - `repeat bypass from local reason dialog => AskRepeat пишется без local DB create`;
  - `RepeatBypass` теперь тоже завершает dialog-series latch, поэтому следующий soft reset в той же серии не перевооружает окно причины повторно;
  - bypass дополнительно проверяется на repeat outcome и сохранность `BoilerState` timers (`TestTime`, `ChangeoverTime`);
  - direct rework-вызовы `AdminAuthDialog` и обычный full-interrupt flow сохраняют старую cancel-семантику через безопасные default-параметры.
- Для direct non-interrupt callers сохранена legacy-совместимость:
  - `AdminAuthDialog` по умолчанию оставляет `ShowCancelButton=true` и `RequireProtectedCancel=true`;
  - repeat-save bypass включается только явной передачей флага через `InterruptDialogService` / `InterruptFlowExecutor`.
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
- Исключение введено только для аварийного `RepeatBypass`:
  - bypass не выполняет ни `interrupt`, ни `start/create operation`;
  - bypass не сохраняет причину и snapshot ни в MES, ни в локальную БД;
  - bypass теперь расходует тот же reset-series dialog latch, что и обычный `Save/Cancel`, чтобы следующий soft reset не открывал окно причины повторно;
  - bypass не меняет scanner/timing semantics и не вводит отдельный cleanup path.
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
- `Final_Test_Hybrid/Services/SpringBoot/Operation/Interrupt/InterruptFlowResult.cs`
- `Final_Test_Hybrid/Components/Main/BoilerInfo.razor`
- `Final_Test_Hybrid/Components/Main/Modals/Interrupt/InterruptReasonDialog.razor`
- `Final_Test_Hybrid/Components/Main/Modals/Rework/AdminAuthDialog.razor`
- `Final_Test_Hybrid/Components/Main/Modals/Rework/AdminAuthResult.cs`
- `Final_Test_Hybrid/Docs/runtime/PlcResetGuide.md`
- `Final_Test_Hybrid/Docs/execution/CycleExitGuide.md`
- `Final_Test_Hybrid/Docs/execution/StepTimingGuide.md`
- `Final_Test_Hybrid/Docs/diagnostics/ScannerGuide.md`
- `Final_Test_Hybrid.Tests/Runtime/InterruptFlowExecutorTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/PostAskEndRepeatSaveTests.cs`

## Проверки

- `dotnet msbuild .\Final_Test_Hybrid\Final_Test_Hybrid.csproj /t:CoreCompile /p:Configuration=Debug` — passed
- `dotnet build Final_Test_Hybrid.slnx` — passed
- `dotnet test .\Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~InterruptFlowExecutorTests|FullyQualifiedName~PostAskEndRepeatSaveTests"` — passed (`15/15`)
- `dotnet test .\Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~InterruptFlowExecutorTests|FullyQualifiedName~PostAskEndRepeatSaveTests"` после фикса series-latch и timer assertions — passed (`17/17`)
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — passed
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — passed
- `jb inspectcode Final_Test_Hybrid.slnx ... -e=WARNING` по changed `.cs` — passed, warning-level findings не зафиксированы
- `jb inspectcode Final_Test_Hybrid.slnx ... -e=HINT` по changed `.cs` — hint-level suggestions only, новых blocking/runtime findings не выявлено

Фактический статус ограничений среды:

- Текущая среда и раньше печатает `MSB3277` warnings по конфликту `WindowsBase 4.0.0.0` vs `5.0.0.0`; текущий change-set их не вводит.

## Residual Risks

- `RepeatBypass` намеренно обходит MES и локальную БД; дальнейшая трассировка repeat-попытки остаётся только в PLC/runtime UI-контуре, пока не появится отдельный backend-contract.
- Полноценного ручного прогона `AskEnd -> Req_Repeat -> Отмена -> AskRepeat -> repeat` в UI в этой сессии нет.

## Инциденты

- no new incident
