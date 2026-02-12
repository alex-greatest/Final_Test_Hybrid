# План: Разбиение `TestExecutionCoordinator.ErrorHandling.cs` на partial-файлы

## Краткое резюме
Файл `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorHandling.cs` слишком большой (417 строк) и смешивает несколько ответственностей:
- channel-сигналы (async auto-reset сигнал через `Channel<bool>`)
- постановка ошибок в очередь + фабрика `StepError`
- основной цикл обработки/резолюции ошибок (Retry/Skip/Timeout)
- PLC-теги/ожидания (Selected/Fault/Block.*)

Цель: разнести код по нескольким `partial`-файлам внутри `TestExecutionCoordinator`, **без изменения поведения**. Это минимально рискованный вариант (в сравнении с выносом в DI-сервисы), но заметно улучшает читаемость и навигацию.

## Цели
- Уменьшить размер `TestExecutionCoordinator.ErrorHandling.cs` и привести файлы к правилу “< 300 lines”.
- Повысить читаемость за счёт группировки методов по зонам ответственности.
- Не менять публичные API/контракты и поведение исполнения.
- Обновить документацию, которая ссылается на старый файл/строки.

## Не цели
- Переписывать алгоритм обработки ошибок.
- Менять порядок `await`, порядок логов, таймауты, или семантику остановки/отмены.
- Вводить новые DI-сервисы/архитектурные изменения (это отдельная, более рискованная задача).

## Изменения интерфейсов/публичных API
- Публичных изменений нет.
- Все методы остаются `private` внутри `partial class TestExecutionCoordinator`.

## Предлагаемая структура файлов (и назначение)
Создать 4 partial-файла и разнести методы 1:1 (перенос без рефакторинга):

1) `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorSignals.cs` — Channel + loop
- `private Channel<bool>? _errorSignalChannel;`
- `StartErrorSignalChannel()`
- `SignalErrorDetected()`
- `CompleteErrorSignalChannel()`
- `RunErrorHandlingLoopAsync(ChannelReader<bool> reader, CancellationToken token)`
- `ProcessErrorSignalsAsync(ChannelReader<bool> reader, CancellationToken token)`

2) `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorQueue.cs` — enqueue ошибок от executors
- `HandleExecutorStateChanged()`
- `EnqueueFailedExecutors()`
- `CreateErrorFromExecutor(ColumnExecutor executor)`

3) `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs` — обработка текущей ошибки (Retry/Skip/Timeout)
- `HandleErrorsIfAny()`
- `HandleTagTimeoutAsync(string context, CancellationToken ct)`
- `ProcessErrorResolution(StepError error, ErrorResolution resolution, CancellationToken ct)`
- `InvokeRetryStartedSafely()`
- `ProcessRetryAsync(StepError error, ColumnExecutor executor, CancellationToken ct)`
- `ExecuteRetryInBackgroundAsync(StepError error, ColumnExecutor executor, CancellationToken ct)`
- `ProcessSkipAsync(StepError error, ColumnExecutor executor, CancellationToken ct)`

4) `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs` — PLC-теги Selected/Fault/Block.*
- `SetSelectedAsync(StepError error, bool value)`
- `SetFaultIfNoBlockAsync(ITestStep? step, CancellationToken ct)`
- `ResetFaultIfNoBlockAsync(ITestStep? step, CancellationToken ct)`
- `WaitForSkipSignalsResetAsync(ITestStep? step, CancellationToken ct)`
- `ResetBlockStartAsync(ITestStep? step, CancellationToken ct)`
- `GetBlockEndTag(ITestStep? step)`
- `GetBlockErrorTag(ITestStep? step)`

## Точное разнесение кода (что к чему)
Источник: `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorHandling.cs`.

### `TestExecutionCoordinator.ErrorSignals.cs`
Перенести:
- `_errorSignalChannel`
- `StartErrorSignalChannel()`
- `SignalErrorDetected()`
- `CompleteErrorSignalChannel()`
- `RunErrorHandlingLoopAsync(...)`
- `ProcessErrorSignalsAsync(...)`

### `TestExecutionCoordinator.ErrorQueue.cs`
Перенести:
- `HandleExecutorStateChanged()`
- `EnqueueFailedExecutors()`
- `CreateErrorFromExecutor(...)`

### `TestExecutionCoordinator.ErrorResolution.cs`
Перенести:
- `HandleErrorsIfAny()`
- `HandleTagTimeoutAsync(...)`
- `ProcessErrorResolution(...)`
- `InvokeRetryStartedSafely()`
- `ProcessRetryAsync(...)`
- `ExecuteRetryInBackgroundAsync(...)`
- `ProcessSkipAsync(...)`

### `TestExecutionCoordinator.PlcErrorSignals.cs`
Перенести:
- `SetSelectedAsync(...)`
- `SetFaultIfNoBlockAsync(...)`
- `ResetFaultIfNoBlockAsync(...)`
- `WaitForSkipSignalsResetAsync(...)`
- `ResetBlockStartAsync(...)`
- `GetBlockEndTag(...)`
- `GetBlockErrorTag(...)`

## План реализации (минимальный риск, пошагово)
1) Создать 4 новых partial-файла (см. структуру выше). В каждом:
   - `namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;`
   - `public partial class TestExecutionCoordinator { ... }`
   - добавить необходимые `using` (допускается дублирование `using` между файлами).
2) Перенести методы 1:1:
   - Не менять тела методов.
   - Не менять порядок вызовов, `await`, обработчиков исключений.
   - Не менять `lock (_enqueueLock)`, `Interlocked.Exchange`, `Volatile.Read`.
   - Не менять тексты логов и таймауты.
3) После переноса:
   - Убедиться, что компиляция проходит и нет дубликатов членов.
4) Удалить исходный файл:
   - `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorHandling.cs`
5) Обновить документацию (убрать ссылки на старый файл/строки):
   - `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md`:
     - В секции **TestExecutionCoordinator: Channel-based Error Handling** заменить список **Файлы** на новый:
       - `.../TestExecutionCoordinator.ErrorSignals.cs`
       - `.../TestExecutionCoordinator.ErrorQueue.cs`
       - `.../TestExecutionCoordinator.ErrorResolution.cs`
       - `.../TestExecutionCoordinator.PlcErrorSignals.cs`
       - `.../TestExecutionCoordinator.Execution.cs`
   - `Final_Test_Hybrid/Docs/execution/StepsGuide.md`:
     - заменить `TestExecutionCoordinator.ErrorHandling.cs:369 — ProcessSkipAsync` на `TestExecutionCoordinator.ErrorResolution.cs — ProcessSkipAsync` (без line-number).
   - `Final_Test_Hybrid/Docs/execution/RetrySkipGuide.md`:
     - в таблице **Ключевые файлы** заменить `TestExecutionCoordinator.ErrorHandling.cs | ProcessRetryAsync, ExecuteRetryInBackgroundAsync` на `TestExecutionCoordinator.ErrorResolution.cs | ProcessRetryAsync, ExecuteRetryInBackgroundAsync`.
   - `Final_Test_Hybrid/RefactoringPlan-SkipWithError.md`:
     - заменить упоминания `.../TestExecutionCoordinator.ErrorHandling.cs` на новый файл по смыслу (обычно `...ErrorResolution.cs` или `...PlcErrorSignals.cs`).

## Инварианты (что нельзя сломать)
Channel-based сигнализация:
- `Channel.CreateBounded<bool>(BoundedChannelOptions(1))`
- `FullMode = BoundedChannelFullMode.DropWrite`
- `SingleReader = true`, `SingleWriter = false`
- жизненный цикл: `StartErrorSignalChannel()` перед стартом колонок, `CompleteErrorSignalChannel()` после завершения `executionTask`

Очередь ошибок:
- постановка ошибок делается только в `Running` и `PausedOnError`
- защищено `lock (_enqueueLock)`
- сигнал в channel отправляется только при переходе из “ошибок нет” → “ошибки появились”

Резолюция ошибок:
- переход в `PausedOnError` происходит сразу при обнаружении `StateManager.CurrentError`
- порядок side-effects сохраняется:
  1) `TransitionTo(PausedOnError)`
  2) `SetSelectedAsync(error, true)`
  3) `SetFaultIfNoBlockAsync(...)`
  4) `OnErrorOccurred?.Invoke(error)`
  5) `WaitForResolutionAsync(...)`
  6) `ProcessRetryAsync` или `ProcessSkipAsync`
- `Retry` остаётся fire-and-forget: `_ = ExecuteRetryInBackgroundAsync(...)`
- таймауты 60 секунд и тексты логов не меняются
- `HandleTagTimeoutAsync` остаётся жёстким стопом: `HandleInterruptAsync(TagTimeout)` + `cts.CancelAsync()`

## Проверки (acceptance)
1) `dotnet build`
2) Проверка ссылок в доках:
   - `rg -n "TestExecutionCoordinator\\.ErrorHandling\\.cs" -S Final_Test_Hybrid` (должно быть 0 результатов)
   - `rg -n "ErrorHandling\\.cs:" -S Final_Test_Hybrid\\Docs` (должно быть 0 результатов)
3) Быстрые ручные сценарии:
   - Ошибка в 1 колонке: диалог появляется сразу, другие колонки продолжают выполнение.
   - 2 колонки падают почти одновременно: обе ошибки в очереди; лишние channel-сигналы “схлопываются” (DropWrite), но очередь обрабатывается полностью.
   - Ошибка во время диалога: добавляется в очередь и обрабатывается после текущей.
   - Retry: `SendAskRepeatAsync` → ожидание сброса `Req_Repeat` → `RetryLastFailedStepAsync` в фоне → gate открывается при успехе.
   - Skip: сброс `Block.Error`/`Block.End` или `Test_End_Step`, ошибка помечается skipped, состояние executor очищается.
   - Timeout (Retry/Skip/WaitForResolution): приводит к interrupt + отмене.

Критерии успеха:
- Сборка проходит.
- Поведение не изменилось (по сценариям выше).
- В документах нет ссылок на удалённый `TestExecutionCoordinator.ErrorHandling.cs` и `ErrorHandling.cs:<line>`.
- Каждый новый partial-файл существенно меньше и проще навигации.
