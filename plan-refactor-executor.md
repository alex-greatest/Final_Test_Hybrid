<proposed_plan>
**Итоговый подробный план: безопасная перестройка `TestExecutionCoordinator` без регрессии UI**

**Summary**
- Вводим единый асинхронный event loop как точку переходов *runtime*‑состояний (`Running/PausedOnError/Completed/Failed`), сохраняя текущий момент старта.
- Убираем `fire-and-forget` в retry/error‑flow через **tracked retry tasks**, сохраняя «быстрый» показ следующего диалога.
- Гарантируем: любой unhandled/timeout → `StopAsFailure=true` + отмена выполнения → итог `Failed`.
- Завершение event loop ждёт все pending retry и только затем вызывает `Complete()`.
- Не трогаем `RangeSlider`, формат логов и семантику UI‑состояний.

---

## Статус выполнения (фактически)

- ✅ Задачи 1–9: выполнены
- 🟡 Задача 10: в работе
- ⏳ Задача 11: регресс после Задачи 10

---

## 1) Требования и ограничения

**Нельзя менять**
- `RangeSlider` (UI/стили/логика).
- Внешнее UI‑поведение: статусы шагов, порядок видимых сигналов, формат логов.
- Цепочки PLC‑reset (`OnForceStop`/`OnReset`).

**Обязательно сохранить**
- Быстрый диалог следующей ошибки после Retry (сейчас это достигается `fire-and-forget`).
- Порядок операций Skip: `DequeueError()` перед `ClearFailedState()`.
- Компактные методы: не более 50 строк.
- Сервисы: не более 300 строк (при необходимости — частичные классы/разбиение).

**Обязательно исправить**
1) Race condition при retry.
2) `TimeoutException → Completed`.
3) Небезопасный `OnErrorOccurred`.
4) Unhandled/timeout должны **и** ставить `StopAsFailure`, **и** отменять `_cts`.
5) Критические события (`StopRequested`, `UnhandledException`) не должны теряться в очереди.

---

## 2) Новая архитектура (внутренняя)

### 2.1 Event Loop (единый контрольный контур)
- Единственный поток принятия решений по runtime‑переходам ExecutionState.
- Весь execution/error/retry проходит через `DispatchEvent`.
- `Complete()` вызывается строго один раз, после завершения event loop и всех pending retry.

### 2.1.1 Границы ответственности
- `BeginExecution()` остаётся точкой подготовки (CTS, лог, reset), но **переходы** `Running/PausedOnError/Completed/Failed` выполняются в event loop.
- `Idle` остаётся в `ResetForRepeat()` и reset‑цепочках (без изменений UI‑тайминга).

### 2.2 ExecutionEvent (новые типы событий)
- `ExecutionEventKind` enum:
  `StartRequested`, `ErrorDetected`, `RetryRequested`, `RetryStarted`, `RetryCompleted`, `SkipRequested`, `StopRequested`, `UnhandledException`, `MapStarted`, `MapCompleted`.

- `ExecutionEvent` record:
  содержит `Kind`, `StepError?`, `ColumnExecutor?`, `ExecutionStopReason?`, `Exception?`, `bool StopAsFailure`.

### 2.3 EventQueue
- `Channel<ExecutionEvent>` с `SingleReader=true`.
- **Не терять критические события**: для `StopRequested`/`UnhandledException` использовать гарантированную запись (`WriteAsync`/unbounded), `ErrorDetected` может быть drop‑friendly.
- Публикация событий с любого потока, обработка только в event loop.

### 2.4 Retry Work Tracking (без fire-and-forget)
- `DispatchEvent(RetryRequested)` **не блокирует** event loop.
- Retry запускается как `Task`, сохраняется в `pendingRetries`.
- По завершении retry публикуется `RetryCompleted`.
- Перед `Complete()` event loop дожидается `Task.WhenAll(pendingRetries)`.

### 2.5 Завершение event loop
- Канал событий закрывается **после** завершения выполнения maps.
- Event loop выходит когда: канал завершён **и** нет pending retry.
- После выхода: `Complete()` и финальные события/логи.

---

## 3) Детальный план по задачам

### Задача 1. Каркас событийной системы
**Цель:** создать структуру без изменения поведения.

**Изменения:**
- Новый файл: `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/ExecutionEvent.cs`.
- В `TestExecutionCoordinator`:
  - `Channel<ExecutionEvent>? _eventChannel`
  - `List<Task> _pendingRetries` + lock/guard
  - `StartEventChannel()`, `PublishEventCritical()`, `TryPublishEvent()`, `CompleteEventChannel()`
  - `TrackRetryTask()`, `AwaitPendingRetriesAsync()`
- В `TestExecutionCoordinator.Execution.cs`:
  - `RunEventLoopAsync()` и `DispatchEvent(ExecutionEvent evt)` как заглушки.

**Готово когда:** сборка успешна, поведение не изменилось.
**Статус:** выполнено.

---

### Задача 2. Перевод `RunWithErrorHandlingAsync` на event loop
**Цель:** единая точка ошибок/завершения.

**Изменения:**
- `StartAsync`/`TryStartInBackground` вызывают `RunEventLoopAsync`.
- `RunEventLoopAsync`:
  - стартует `StartEventChannel()` + `PublishEvent(StartRequested)` при старте.
  - `DispatchEvent(StartRequested)` выполняет `TransitionTo(Running)` и другие runtime‑переходы, чтобы не держать их в `BeginExecution()`.
  - запускает `RunAllMaps()` как `executionTask`.
  - читает события из channel и вызывает `DispatchEvent`.
  - `catch(Exception)` → `Stop(..., markFailed: true)` + log + `CancelExecution(...)`.
  - после завершения `executionTask` закрывает канал событий.
  - перед `Complete()` ждёт `AwaitPendingRetriesAsync()`.

---

**Статус:** выполнено.

---

### Задача 3. Разделение `TestExecutionCoordinator.Execution.cs` (лимит 300 строк)
**Цель:** соблюсти ограничение по размеру сервиса без изменения поведения.

**Изменения:**
- Вынести event loop и связанные методы в новый partial:
  - Новый файл: `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.EventLoop.cs`.
  - Перенести: `RunEventLoopAsync`, `DispatchEvent`, `DispatchStartEventAsync`, `RunEventReaderAsync`, `AwaitExecutionAsync`, `ApplyStartRequested`.
- Оставить в `TestExecutionCoordinator.Execution.cs` только map/execution-логику.
- Проверить, что в обоих файлах остаётся ≤300 строк, методы ≤50 строк.

**Готово когда:** сборка успешна, поведение не изменилось.

---

### Задача 4. Fix `TimeoutException → Completed`
**Цель:** любой unhandled/timeout → `Failed`.

**Изменения:**
- Ввести helper `RequestStopAsFailure(reason, message)`:
  - `_flowState.RequestStop(reason, stopAsFailure: true)`
  - `CancelExecution(message)`
- Для unhandled/timeout использовать `ExecutionStopReason.Operator` (без изменения маппинга) **или** добавить новый `ExecutionStopReason.UnhandledException` и явно согласовать последствия в `PreExecutionCoordinator` (если потребуется).
- `HandleTagTimeoutAsync` обязан вызывать `RequestStopAsFailure(...)` перед `CancelAsync()`.

---

### Задача 5. Safe invoke событий
**Цель:** исключения подписчиков не ломают flow.

**Изменения:**
- `InvokeErrorOccurredSafely` + лог `LogWarning`.
- Добавить safe‑обёртки для `OnRetryStarted`, `OnSequenceCompleted` (и `OnStateChanged`, если оставляем публичным).
- Все публичные события вызываются только в event loop (через `DispatchEvent`).

---

### Задача 6. Retry через event loop **с быстрым диалогом**
**Цель:** убрать race, сохранить UX.

**Новые требования из `RetrySkipGuide.md`:**
- Диалог следующей ошибки должен показываться сразу после `Req_Repeat`.
- Порядок PLC‑сигналов и `DequeueError()` должен соответствовать текущему протоколу.

**План:**
- `ProcessRetryAsync`:
  - `SendAskRepeatAsync(...)`
  - `InvokeRetryStartedSafely()`
  - `WaitForRetrySignalResetAsync(...)`
  - `DequeueError()` (как сейчас)
  - **Сразу** публикуем `RetryRequested` (чтобы очередь могла продолжить), но retry выполняется в **tracked task**.
- `DispatchEvent( RetryRequested )`:
  - Отмечаем `RetryState.IsActive = true` (thread-safe).
  - Запускаем `RunRetryAsync(...)` **без await**, но через `TrackRetryTask(...)`.
  - `RunRetryAsync` делает `RetryLastFailedStepAsync`, `ResetFaultIfNoBlockAsync`, `OpenGate()` и в `finally`:
    - `RetryState.IsActive = false`
    - `PublishEvent(RetryCompleted)`
    - освобождает tracked task.

**Важно:**
- `DequeueError()` остаётся в том же месте, как сейчас, чтобы не менять UX.

---

### Задача 7. Skip‑flow порядок
**Цель:** сохранить порядок `DequeueError → ClearFailedState`.

**Изменения:**
- Переместить skip‑логика в event loop так, чтобы порядок сохранился.

**Как сделали (фактически):**
- Skip‑flow оставили в `ProcessSkipAsync(...)` (вне event loop), событие `SkipRequested` не использовали.
- Инвариант сохранён: `StateManager.DequeueError()` → `executor.ClearFailedState()`.

**Почему так сделали:**
- Перенос `Dequeue/ClearFailedState` в очередь событий меняет тайминги (очередь/параллельные события) и может дать «лаг» после Skip или повторную вспышку ошибки из‑за гонок.
- Локальная последовательность в `ProcessSkipAsync` — самый стабильный UX‑контракт и защита от race condition.

---

### Задача 8. ErrorSignals через события
**Цель:** все ошибки проходят через event loop.

**Изменения:**
- `ProcessErrorSignalsAsync` публикует `ErrorDetected`.
- `DispatchEvent(ErrorDetected)` вызывает `HandleErrorsIfAny`.
- Исключение внутри → `UnhandledException`.

**Как сделали (фактически):**
- Отдельный error‑loop/канал (`ProcessErrorSignalsAsync`) убрали.
- Сигнал об ошибках коалесцируется через `QueueErrorDetected()` (защита от «спама» событий).
- `DispatchEvent(ErrorDetected)` запускает `EnsureErrorDrainStarted()` и фоновой drain `DrainErrorsSafelyAsync()` **без await**.
- Любое исключение внутри drain → `HandleUnhandledException(...)` (остановка как failure, с отменой выполнения).
- Перед `Complete()` event loop дожидается `_errorDrainTask` (через `AwaitErrorDrainSafelyAsync()`), чтобы не завершить тест до окончания обработки ошибок.

**Почему так сделали:**
- `HandleErrorsIfAny()` может ждать оператора/PLC; если `await`ить его прямо в event loop, блокируются остальные события (state changes/stop/retry) и ухудшается отзывчивость UI.
- Коалесцирование + фоновый drain сохраняют быстрый UI‑отклик и убирают гонки на многократных сигналах.
- Ожидание drain’а перед `Complete()` исключает преждевременный `Completed/Failed`, пока ещё идёт обработка ошибок.

---

### Задача 9. Race защита при Idle
**Цель:** нет преждевременного `Idle`.

**Изменения:**
- Ввести `RetryState` в coordinator.
- `AreExecutorsIdle()` учитывает `RetryState.IsActive` и наличие `pendingRetries`.

---

### Инцидент: зависание перехода между Map (фактически)

**Симптом:** после завершения блока N следующий блок не стартует; выполнение “висит” между блоками.

**Причина:** `AreExecutorsIdle()` использовал `executor.IsVisible` как критерий idle.  
Но `IsVisible` — это UI‑история (`Status != null`), которая по требованиям должна оставаться видимой (“Готово/Пропуск”).

**ВАЖНО (ЗАПРЕТ):** **НИКОГДА** не используйте `ColumnExecutor.IsVisible` / `Status != null` как критерий “idle/можно начинать следующий Map”.  
Это напрямую приводит к зависанию “между Map”, потому что UI‑статусы (“Готово/Пропуск”) могут оставаться видимыми после завершения блока.

Шпаргалка:
- ❌ Нельзя: `... && _executors.All(e => !e.IsVisible)`
- ✅ Нужно: тех.условия (pending errors/retry/pending retries/`HasFailed`), UI‑история не участвует

**Решение:** `AreExecutorsIdle()` переведён на технические условия (нет pending errors, нет активных/pending retry, нет `HasFailed`).  
UI‑история больше не участвует в решении “можно ли начинать следующий блок”.

---

### Задача 10. Reset/Cancellation без изменений
**Цель:** не ломать reset‑цепочки (`PlcResetGuide.md`, `CancellationGuide.md`).

**Изменения:**
- Не менять связку:
  - `OnForceStop` → `Stop()` → `ExecuteSmartReset` → `ForceStop/Reset`.
- `StopAsFailure` остаётся OR‑логикой.
- Linked CTS (`ct + _resetCts.Token`) в completion flow не трогаем.

**Дополнение (найдено при реальном прогоне):**
- При `TagTimeout`/hard reset возможно “УСПЕШНО” в финале, если общий `ExecutionFlowState` был очищен (`ClearStop()`) в `PreExecutionCoordinator` раньше, чем `TestExecutionCoordinator` дошёл до `Complete()`.

**Что делаем:**
- Вводим локальную “защёлку” stop‑состояния в `TestExecutionCoordinator` (Reason + StopAsFailure) на время прогона и используем её в `Complete()` (вместе с `_flowState.GetSnapshot()`), чтобы внешний `ClearStop()` не мог превратить hard reset/TagTimeout в success.

---

### Задача 11. Регрессионные проверки
**Сценарии:**
- Timeout → итог `Failed`.
- Exception в `OnErrorOccurred`/`OnStateChanged` → log warning, поток не ломается.
- Retry → следующий диалог ошибки появляется сразу.
- Skip → порядок `DequeueError → ClearFailedState`.
- Cancel/reset → поведение идентично текущему.
- Unhandled → `StopAsFailure=true` + отмена, итог `Failed`.
- Нет «потерянных» `StopRequested`/`UnhandledException` в очереди.

---

## 4) Файлы и зоны изменений

**Основные:**
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.Execution.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.EventLoop.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorSignals.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorQueue.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ExecutionFlowState.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.cs`
- Новый: `ExecutionEvent.cs`

---

## 5) Acceptance Criteria
- Нет `fire-and-forget` в retry/error‑flow.
- Любой unhandled/timeout → `StopAsFailure=true` + отмена → `ExecutionState.Failed`.
- Event loop — единственная точка **runtime**‑переходов `ExecutionState` (кроме init/Idle reset).
- Event loop дожидается `pendingRetries` перед `Complete()`.
- UI остаётся прежним: статусы/логи/диалоги не ломаются.
- Retry диалог остаётся «быстрым».
- Критические события не теряются в очереди.

---

## 6) Риски и контроль
- **Риск:** диалог ошибок замедлится при переносе retry в event loop.
  **Контроль:** разделяем `RetryRequested` и выполнение retry, чтобы диалог не ждал завершения retry.
- **Риск:** порядок PLC‑сигналов нарушится.
  **Контроль:** сохраняем `SendAskRepeatAsync → InvokeRetryStartedSafely → WaitForRetrySignalResetAsync → DequeueError`.
- **Риск:** reset‑цепочки затронуты.
  **Контроль:** не трогаем `PlcResetCoordinator` и `PreExecutionCoordinator`.

---

## 7) Предположения
- Разрешены внутренние изменения `TestExecutionCoordinator` и связанного flow.
- Публичные события остаются, UI поведение не меняется.
</proposed_plan>
