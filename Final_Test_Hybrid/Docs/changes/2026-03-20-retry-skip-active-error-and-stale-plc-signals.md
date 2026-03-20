# 2026-03-20 retry skip active error and stale plc signals

## Failure mode

- После `Req_Repeat=false` execution retry PLC-шага мог мгновенно повторно завершиться по stale `Block.Error=true` или `Block.End=true`, которые ещё оставались в OPC cache до нового PLC цикла.
- Визуальный симптом: оператор сбросил `Req_Repeat`, но шаг не повторился штатно и оставался в ошибке либо ловил ложный success/fail без реального rerun.
- При множественных ошибках `Retry/Skip` освобождали очередь через удаление головы FIFO, а не явно подтверждённой активной ошибки.
- Из-за этого pending ошибка другой колонки могла быть удалена или остаться в неверном состоянии относительно текущего active error dialog.
- После адресного удаления active error execution retry оставлял короткое окно, где колонка ещё была `HasFailed=true`, но запись уже исчезала из очереди.
- Любой посторонний `OnStateChanged` в это окно мог заново enqueue той же ошибки и повторно открыть уже подтверждённый dialog во время in-flight retry.

## Root cause

- `TagWaiter.WaitAnyAsync(...)` по контракту сначала проверяет уже известный runtime cache и принимает already-active `End/Error` без ожидания нового edge от PLC.
- Execution retry-path после `WaitForRetrySignalResetAsync(...)` сразу публиковал `RetryRequested` и не фильтровал stale `Block.Error/End`.
- `Skip` и `Retry` завершали ошибку через head-based `DequeueError()`, хотя решение уже было принято по конкретному `StepError`, привязанному к открытому диалогу.
- Pre-execution retry `BlockBoilerAdapterStep` после `SendAskRepeatAsync(...)` не ждал `Req_Repeat=false`, поэтому handshake оставался слабее execution-ветки.
- Execution queue ownership фиксировал только адресное удаление, но не подавлял повторный enqueue той же колонки до фактического старта retry.

## Resolution

- Добавлен coordinator-level freshness guard для retry PLC-шага:
  - после `Req_Repeat=false`;
  - ждёт только known stale `Block.Error=true` и затем `Block.End=true`;
  - не пишет `Start=false`;
  - при timeout переводит execution в тот же fail-fast `TagTimeout` path.
- Для pre-execution retry `BlockBoilerAdapterStep` восстановлен полный handshake:
  - `SendAskRepeatAsync(...)`;
  - ожидание `WaitForRetrySignalResetAsync(...)`;
  - затем freshness guard и только после этого повторный `ExecuteAsync(...)`.
- `Retry/Skip` теперь снимают из очереди именно active error context через адресное удаление, а не через blind `DequeueError()` головы FIFO.
- Для execution retry добавлено подавление повторного enqueue по колонке на окно `RetryRequested -> RetryCompleted`, чтобы active error не возвращалась в очередь до фактического исхода retry.
- Stable guides синхронизированы с новым контрактом retry/skip и queue ownership.

## Verification

- Добавлены регрессии на:
  - `ExecutionStateManager.TryRemoveError(...)` с сохранением порядка остальных ошибок;
  - `PlcRetrySignalFreshnessGuard` для known true / unknown / cancellation path;
  - suppression состояния execution retry до `RetryCompleted`;
  - pre-execution retry handshake c обязательным `Req_Repeat=false`;
  - `TagWaiter.WaitAnyAsync(...)` на already-active `Block.End/Block.Error`.
- Локально подтверждены:
  - `dotnet build Final_Test_Hybrid.slnx`;
  - выборочный `dotnet test` по runtime regression набору для retry/skip, pre-execution guard и `TagWaiter`.

## Notes

- Глобальный cache-first контракт `TagWaiter` не менялся; fix сделан точечно на уровне coordinator/guard.
- `Skip` по-прежнему остаётся единственным путём, который пишет `Start=false`.
- Отдельного incident-registry в репозитории не обнаружено; данный change-doc используется как явная фиксация нового failure mode и должен упоминаться из impact.
