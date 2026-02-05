# План диагностики (максимально подробно): зависания, гонки и пограничные случаи в Execution / Retry / Skip

## TL;DR
Цель — не просто “поставить таймаут/сообщение”, а **убрать первопричины зависаний** и сделать систему предсказуемой (safety‑critical): явные инварианты, bounded‑ожидания, корректная пауза/сброс, и достаточная телеметрия, чтобы любой hang разбирался по одному логу.

По логу `Final_Test_Hybrid/3.txt` подтверждён **детерминированный hang на переходе блока** после Retry: coordinator ждёт `idle` по UI‑признаку `IsVisible`, а колонка может “закончить карту” в failed‑состоянии и позже перейти в success без очистки UI‑статуса → `IsVisible=true` навсегда → следующий Map не стартует.

---

## 1) Как это работает (коротко, но по сути)

### 1.1 Execution (TestExecutionCoordinator)
- `RunAllMaps()` последовательно выполняет `RunCurrentMap(map)`.
- Перед запуском каждого Map: `WaitForExecutorsIdleAsync()` (должно гарантировать “все колонки idle”).
- Запуск Map:
  - открывается `_mapGate` (AsyncManualResetEvent) через `ActivateMap(mapIndex)`, формируется `MapRunId`;
  - параллельно запускаются 4 `ColumnExecutor.ExecuteMapAsync(map, mapIndex, mapRunId, ct)`;
  - параллельно крутится обработка ошибок: channel‑сигнал → `HandleErrorsIfAny()`.
- По завершению Map: `_mapGate` закрывается через `DeactivateMap(...)`.

### 1.2 ColumnExecutor (по колонке)
- Внутри `ExecuteMapAsync` колонка идёт по списку шагов:
  - ждёт `_continueGate` (открыт по умолчанию; закрывается при ошибке);
  - учитывает паузу через `pauseToken.WaitWhilePausedAsync(ct)`;
  - ждёт доступ к активному Map через `_mapGate` + сверку `(mapIndex, mapRunId)` (`WaitForMapAccessAsync`);
  - выполняет `ExecuteStep(step, ct)`.
- При ошибке шага:
  - `SetErrorState(...)` делает `_continueGate.Reset()` + `HasFailed=true`;
  - состояние отражается в UI (`Status="Ошибка"`, etc.);
  - coordinator подхватывает это через `OnStateChanged` и ставит в очередь ошибок.
- При Retry:
  - coordinator запускает `executor.RetryLastFailedStepAsync(ct)` (fire‑and‑forget) и при успехе делает `executor.OpenGate()` (это `_continueGate.Set()`).
- При Skip:
  - coordinator делает `executor.ClearFailedState()` (там тоже `_continueGate.Set()`).

### 1.3 Error Queue + Error Resolution
- `TestExecutionCoordinator.HandleExecutorStateChanged` → `EnqueueFailedExecutors()` → `StateManager.EnqueueError()` (дедуп по ColumnIndex).
- При первой ошибке включается channel‑сигнал, и loop вызывает `HandleErrorsIfAny()`:
  - показывает диалог;
  - ждёт решение через `ErrorCoordinator.WaitForResolutionAsync(options, ct)` (Retry/Skip/Timeout);
  - выполняет `ProcessRetryAsync` или `ProcessSkipAsync`.

---

## 2) Подтверждённый hang-класс из `Final_Test_Hybrid/3.txt`

### 2.1 Симптом (из лога)
В момент зависания на переходе блока:
- `HasFailed=False`
- `Status=Готово`
- `IsVisible=True` (потому что `Status != null`)
- `WaitForExecutorsIdleAsync` не может дождаться `AreExecutorsIdle()`.

### 2.2 Корень
`WaitForExecutorsIdleAsync` использует UI‑прокси (видимость) как “занятость”. При этом возможен сценарий:
1) Ошибка произошла на **последнем шаге** колонки в текущем Map.
2) `ExecuteMapAsync()` доходит до конца списка шагов, но `HasFailed=true`, поэтому не вызывает `ClearStatusIfNotFailed()` и завершается.
3) Оператор делает Retry → шаг становится success (`HasFailed=false`), но **очистка UI‑статуса (`Status=null`) не выполняется**, потому что `ExecuteMapAsync()` уже закончился.
4) Колонка “успешна”, но остаётся `IsVisible=true` → переход Map зависает.

---

## 3) Полная диагностика: что именно проверяем (инвентаризация рисков)

### 3.1 Набор “инвариантов”, которые должны быть истинны
1) **Map transition invariant**: перед стартом нового Map все колонки должны быть в execution‑idle состоянии (НЕ UI‑idle).
2) **Error resolution invariant**: если колонка упала, она не должна “завершить Map” до того, как ошибка разрешена (Retry/Skip/Stop/Reset).
3) **Pause invariant**: при pause (AutoReady OFF) шаги и ожидания должны вести себя согласно выбранной политике (см. раздел 6).
4) **PLC handshake invariant**: ожидания PLC‑сигналов должны быть bounded‑time и диагностируемы (subscription vs direct read).
5) **Reset/Stop invariant**: Reset/Stop должны прерывать все ожидания без дедлоков и оставлять систему в чистом состоянии.

### 3.2 Полный список “waitpoints” (что инвентаризировать)
Использовать шаблон `plan/waitpoints-audit-template.md` и заполнить минимум по зонам:
- Execution:
  - `WaitForExecutorsIdleAsync` (между Map)
  - `_mapGate.WaitAsync` + `WaitForMapAccessAsync`
  - `_continueGate.WaitAsync` (по шагам/ошибке)
- Error resolution:
  - `ErrorCoordinator.WaitForResolutionAsync` (WaitGroup: Retry/Skip)
  - `WaitForSkipSignalsResetAsync` (Block.End/Error/Test_End_Step)
  - `SendAskRepeatAsync` / `WaitForRetrySignalResetAsync`
- Completion:
  - `HandleTestCompletedAsync`: ожидание `End=false` (сейчас может быть бесконечным)
  - Save retry loop (диалог)
- PreExecution:
  - `WaitForBarcodeAsync`
  - reset/signals: `_askEndSignal`, `_resetSignal`, linked CTS

---

## 4) План реализации по фазам (строго по приоритету)

### Фаза 0 — Наблюдаемость (без изменения поведения)
**Цель:** любой hang/гонку можно разобрать по одному логу.

**Действия:**
- Ввести корреляционные идентификаторы в логи:
  - `TestRunId`, `MapIndex`, `MapRunId`, `ColumnIndex`, `UiStepId`, `StepName`, `PlcBlockPath`.
- Логировать изменения gate’ов:
  - `_continueGate.Reset/Set` + причина (ошибка, retry success, skip, cancel).
  - `_mapGate.Set/Reset` + ожидаемый/активный snapshot.
- Логировать начало/конец ключевых ожиданий:
  - ожидание idle между Map;
  - ожидание skip‑reset;
  - ожидание ask‑repeat / req‑repeat reset;
  - ожидание completion End reset.

**Acceptance:**
- По одному логу можно восстановить цепочку: Map → шаги → ошибка → решение → переход Map.

---

### Фаза 1 (P0) — Фикс корня hang после Retry (ошибка на последнем шаге)
**Цель:** колонка не должна “заканчивать Map” пока не разрешена ошибка.

**Рекомендуемая минимальная реализация:**
- В `ColumnExecutor.ExecuteMapAsync` добавить финальный барьер:
  - если после прохода всех шагов `_state.HasFailed==true`, колонка ждёт решения оператора (`_continueGate`) до тех пор, пока `HasFailed` не станет `false` или не придёт отмена/stop/reset.
  - после разрешения — гарантированно привести колонку к execution‑idle (и при необходимости очистить UI‑статус).

**Acceptance:**
- Сценарий “ошибка на последнем шаге → Retry → следующий Map стартует” не зависает.
- Сценарий “ошибка на последнем шаге → Skip → следующий Map стартует” не зависает.

---

### Фаза 2 (P0/P1) — Развязать execution‑idle от UI (`IsVisible`)
**Цель:** Map transition не должен зависеть от того, что UI “показывает/не показывает”.

**Решение:**
- Ввести в `ColumnExecutor` отдельный признак “занятости” (например, `ExecutorActivityState`/`IsMapActive`), который:
  - true во время выполнения шага/ожидания разрешения;
  - false когда колонка действительно завершила Map и не ждёт действий.
- `WaitForExecutorsIdleAsync` должен ждать именно execution‑idle, а не `IsVisible`.

**Acceptance:**
- Даже если UI оставляет “Готово”/историю на экране, следующий Map стартует корректно.

---

### Фаза 3 — Аудит всех ожиданий/таймаутов/рекурсий (P1/P2)
**3.1 Рекурсивные ожидания → циклы**
- `WaitForMapAccessAsync` использует рекурсию + delay → риск роста стека при длительной блокировке.
- Переписать в `while` + rate‑limited logging.

**3.2 Completion: потенциально бесконечное ожидание**
- В `TestCompletionCoordinator.HandleTestCompletedAsync` есть `WaitForFalseAsync(..., timeout:null)` → может висеть вечно, если PLC не сбросит End и reset не придёт.
- Ввести bounded timeout (например, 60с) + чёткую политику реакции (см. раздел 6).

**3.3 Pause‑семантика для ожиданий**
- Зафиксировать политику: должны ли решения оператора/PLC приниматься во время pause (AutoReady OFF).
- Привести `TagWaiter`-ожидания к этой политике (pause-aware там, где нужно).

**3.4 Error signal channel invariant**
- Channel bounded + DropWrite: проверить инвариант “ошибка в очереди не может остаться без обработки”.
- Добавить страховочный механизм (например, “если HasPendingErrors → HandleErrorsIfAny” при определённых переходах/тикере).

---

### Фаза 4 — PLC импульсы / подписка vs direct read (P1/P2)
**Цель:** короткие импульсы End/Error/Skip не теряются.

**Единый паттерн:**
1) Direct read snapshot (если уже в нужном состоянии — выйти).
2) Subscription wait (если нужно).
3) На timeout — лог subscription vs direct read.

Опционально: PLC‑latch/handshake вместо импульсов.

---

### Фаза 5 — Тесты/симуляция/регрессия (обязательно)
**Цель:** воспроизводимость и защита от возврата багов.

**Минимум:**
- Тест “ошибка на последнем шаге → Retry → Map transition OK”.
- Тест “ошибка на последнем шаге → Skip → Map transition OK”.
- Тест “Completion End не сброшен → bounded timeout policy”.

При необходимости — фейковый слой PLC (subscription + direct read) и управляемое время.

---

## 5) Список потенциальных багов/краевых случаев (стартовый)
Полный чеклист — в `plan/edge-cases-checklist.md`.

P0 кандидаты:
- Map transition зависит от UI (`IsVisible`).
- Ошибка на последнем шаге и поздний Retry/Skip.
- Completion: бесконечное ожидание PLC сброса End.

P1 кандидаты:
- Рекурсивные async‑ожидания (`WaitForMapAccessAsync`).
- Неопределённая семантика pause для ожиданий.
- DropWrite channel: проверить инвариант “ошибка не потеряется”.

P2 кандидаты:
- Rate‑limit логов ожидания (анти‑spam).
- Стандартизация логов “subscription vs direct read”.

---

## 6) Политики (decision log, чтобы не спорить по месту)
Для каждого timeout/аномалии фиксируем реакцию:
- Continue / Pause / Stop / Require operator action.

Рекомендуется сделать policy настраиваемой через конфиг (например секция `Execution.*`), чтобы в production держать “safe default”, а на стенде/в отладке — менее агрессивное поведение.

