# 2026-03-21 repeat-autoready-gate-after-post-askend

## Контур

- PreExecution / post-AskEnd repeat path
- ErrorCoordinator / AutoReady ownership
- Message semantics / repeat runtime gating

## Что изменено

- Закрыт runtime-gap после `post-AskEnd -> Req_Repeat`:
  repeat/pre-execution больше не продолжает подготовку молча, если `AutoReady=false` и PLC-связь жива.
- В `PreExecutionCoordinator` добавлен единый pre-execution AutoReady gate перед:
  - `StartTimer1`;
  - `BlockBoilerAdapterStep`;
  - стартом `TestExecution`.
- Новый gate:
  - сначала проверяет `OpcUaConnectionState.IsConnected`;
  - не поднимает `AutoModeDisabled`, если PLC-связь уже потеряна;
  - при `IsConnected=true && !AutoReady.IsReady && CurrentInterrupt == null`
    повторно использует существующий `HandleInterruptAsync(InterruptReason.AutoModeDisabled)`;
  - затем ждёт `PauseToken.WaitWhilePausedAsync(ct)`.
- Terminal suppression не менялся:
  - во время active `post-AskEnd` `AutoReady OFF` по-прежнему не поднимает `AutoModeDisabled`;
  - message-owner в этом окне по-прежнему `Сброс подтверждён. Ожидание решения PLC...`.
- После выхода из terminal window repeat/pre-execution возвращается в normal ownership:
  - `Ожидание автомата` снова становится допустимым main message;
  - `StartTimer1`, `BlockBoilerAdapterStep` и старт теста блокируются до `AutoReady=true`.
- Сам `BlockBoilerAdapterStep` не получил отдельный `PauseTokenSource`:
  - шаг остаётся на существующем паттерне `context.OpcUa + tagWaiter`;
  - pause-awareness обеспечивается pausable-сервисами и новым pre-execution gate.
- Тестовые фабрики `PreExecutionInfrastructure` синхронизированы с новым контрактом
  `connectionState + autoReady`, чтобы runtime gate проверялся на реальной связке зависимостей.
- Stable docs сверены с новым контрактом:
  - `Docs/ui/MessageSemanticsGuide.md`
  - `Docs/runtime/ErrorCoordinatorGuide.md`
  - `Docs/execution/CycleExitGuide.md`

## Что сознательно не менялось

- `_skipNextScan` как механизм repeat.
- `StartRepeatAfterReset()` и сама post-AskEnd decision-модель.
- Подавление `AutoModeDisabled` внутри active terminal handshake.
- Приоритет `PlcConnectionLost` над `AutoModeDisabled`.
- `BlockBoilerAdapterStep` не переведён в error/reset-path при живой PLC-связи и `AutoReady=false`.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Pipeline.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionDependencies.cs`
- `Final_Test_Hybrid.Tests/Runtime/PreExecutionAutoReadyGateTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/PostAskEndDecisionLoopTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/PreExecutionRetryHandshakeTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/PreExecutionStopReasonTests.cs`
- `Final_Test_Hybrid.Tests/TestSupport/PreExecutionTestContextFactory.cs`
- `Final_Test_Hybrid/Docs/ui/MessageSemanticsGuide.md`
- `Final_Test_Hybrid/Docs/runtime/ErrorCoordinatorGuide.md`
- `Final_Test_Hybrid/Docs/execution/CycleExitGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; baseline warning только `MSB3277` по `WindowsBase`.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj` — успешно, `88/88`.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Pipeline.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionDependencies.cs;Final_Test_Hybrid.Tests/TestSupport/PreExecutionTestContextFactory.cs;Final_Test_Hybrid.Tests/Runtime/PostAskEndDecisionLoopTests.cs;Final_Test_Hybrid.Tests/Runtime/PreExecutionRetryHandshakeTests.cs;Final_Test_Hybrid.Tests/Runtime/PreExecutionStopReasonTests.cs;Final_Test_Hybrid.Tests/Runtime/PreExecutionAutoReadyGateTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-preexecution-autoready-final.txt" -e=WARNING` — отчёт пуст (`Solution Final_Test_Hybrid.slnx`).
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Pipeline.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionDependencies.cs;Final_Test_Hybrid.Tests/TestSupport/PreExecutionTestContextFactory.cs;Final_Test_Hybrid.Tests/Runtime/PostAskEndDecisionLoopTests.cs;Final_Test_Hybrid.Tests/Runtime/PreExecutionRetryHandshakeTests.cs;Final_Test_Hybrid.Tests/Runtime/PreExecutionStopReasonTests.cs;Final_Test_Hybrid.Tests/Runtime/PreExecutionAutoReadyGateTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-preexecution-autoready-final.txt" -e=HINT` — только неблокирующие hint:
  - старый неполный `switch` по `scanResult.Status` в `PreExecutionCoordinator.Pipeline.cs`;
  - `PreExecutionInfrastructure.TagWaiter` пока не читается новым change-set;
  - test-only structural hints (`private`/overload suggestion).

## Риски / остатки

- Новый gate не вводит generic blocking по любому `CurrentInterrupt != null`; он закрывает именно зазор `AutoReady=false` после post-AskEnd.
- Hint про неполный `switch` в `ExecutePreExecutionPipelineAsync` остался без изменения, потому что этот пакет не меняет старую матрицу `ScanStep`-статусов и не должен расширять поведение вне целевого AutoReady-gap.

## Инциденты

- `no new incident`
- Пакет закрывает уже подтверждённый runtime-gap между terminal repeat path и normal `AutoReady` gating, не вводя новый failure mode.
