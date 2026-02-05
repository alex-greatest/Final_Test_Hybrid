# Edge Cases Checklist — Execution / Retry / Skip / Reset (Final_Test_Hybrid)

Цель файла — собрать **все пограничные случаи**, которые потенциально дают зависание/гонку/неверный результат.  
Используется как чеклист для регрессии и как список сценариев для тестов/симуляции.

---

## 1) Переходы Map / “все колонки idle”

### EC-1: Ошибка на последнем шаге → Retry → следующий Map
- **Setup:** шаг X (последний в колонке) падает (`HasFailed=true`, gate закрыт).
- **Action:** оператор жмёт Retry, шаг становится success (`HasFailed=false`).
- **Expected:** колонка становится execution‑idle; `WaitForExecutorsIdleAsync` завершается; стартует следующий Map.
- **Must log:** финальный snapshot колонки в конце Map + событие “ошибка разрешена после конца списка шагов”.

### EC-2: Ошибка на последнем шаге → Skip → следующий Map
- Аналогично EC‑1, но через Skip.

### EC-3: Ошибка в середине Map → Retry → продолжение шагов в том же Map
- **Expected:** после Retry gate открывается, цикл продолжает следующий шаг в текущем Map, а не “залипает”/не перескакивает.

### EC-4: Две колонки упали почти одновременно
- **Expected:** очередь ошибок корректно показывает по одной (дедуп по ColumnIndex); вторая не теряется; после разрешения первой показывается вторая.

### EC-5: Колонка A success, колонка B waiting (pause/interrupt) — переход Map
- **Expected:** переход Map не начинается, пока все колонки реально execution‑idle (а не UI‑idle).

---

## 2) Gate / WaitForMapAccess / Snapshot mismatch

### EC-6: Map gate закрылся пока шаг ждёт доступа (snapshot mismatch)
- **Setup:** шаг в `WaitForMapAccessAsync`, coordinator переключает MapRunId.
- **Expected:** шаг не стартует на “старом” Map; корректно переходит в ожидание нового Map или завершает по cancel.
- **Risk:** рекурсивный вызов → рост стека при длительном ожидании.

### EC-7: Частые mismatch события (быстрые перезапуски)
- **Expected:** нет stack overflow; лог не спамится (rate limit).

---

## 3) Retry Flow (PLC AskRepeat / Req_Repeat)

### EC-8: Retry на последнем шаге + AutoReady OFF/ON во время ожидания
- **Expected:** поведение строго по policy pause: либо ожидания “замораживаются”, либо продолжают (зафиксировать).
- **Must log:** pause transitions + remaining timeout.

### EC-9: PLC не сбросил `Req_Repeat` за 60с
- **Expected:** срабатывает `HandleTagTimeoutAsync` (жёсткий стоп) — или новая policy.

### EC-10: `SendAskRepeatAsync` записал AskRepeat, но Block.Error не сброшен
- **Expected:** timeout → interrupt TagTimeout → cancel теста (или policy).

---

## 4) Skip Flow (Block.End + Block.Error / Test_End_Step)

### EC-11: Skip коротким импульсом (1–3 сек)
- **Expected:** фиксируется (direct read +/или subscription); Skip выполняется.

### EC-12: Skip с блоком: End=true & Error=true → затем PLC быстро сбросил оба
- **Expected:** `WaitForSkipSignalsResetAsync` завершается сразу (direct read guard).

### EC-13: Skip без блока: Test_End_Step импульсный
- **Expected:** не теряем импульс; корректно ждём сброс.

### EC-14: PLC “залип” End/Error в true
- **Expected:** bounded timeout → policy (stop/pause/operator action) + диагностика subscription vs direct.

---

## 5) Pause / Interrupts (AutoReady)

### EC-15: AutoReady OFF во время выполнения шага
- **Expected:** шаги используют pause‑aware API (context.DelayAsync/DiagReader/Writer) и корректно замирают на safe points.

### EC-16: AutoReady OFF во время ожидания резолюции (Retry/Skip)
- **Expected:** поведение по policy: либо заморозка, либо продолжаем принимать решение.

### EC-17: AutoReady “дребезг” OFF→ON→OFF
- **Expected:** нет deadlock; нет пропуска interrupt; состояние прерывания согласовано.

---

## 6) Stop / Reset (PLC reset + operator stop)

### EC-18: PLC ForceStop во время активного шага
- **Expected:** coordinator отменяет execution, очищает очередь ошибок, состояние consistent.

### EC-19: Hard reset во время ожидания completion End=false
- **Expected:** ожидание прерывается, UI очищается, цикл PreExecution корректно выходит.

### EC-20: Reset пришёл во время WaitForBarcode
- **Expected:** linked CTS отменяет ожидание, input корректно деактивируется, barcode не “переиспользуется” ошибочно.

---

## 7) Completion (End handshake + Save)

### EC-21: PLC не сбрасывает End=false (completion)
- **Expected:** bounded timeout + policy; не бесконечное ожидание.

### EC-22: SaveAsync падает, оператор выбирает Retry несколько раз
- **Expected:** цикл retry корректен, диалог не дублируется, cancel/reset прерывает.

### EC-23: SaveAsync завис (внешняя зависимость)
- **Expected:** bounded timeout (если введём) + policy.

---

## 8) Subscription / direct read consistency

### EC-24: Подписка “отстаёт” от прямого чтения (latency/jitter)
- **Expected:** direct read guard уменьшает зависания; лог фиксирует расхождение.

### EC-25: Реконнект OPC-UA во время ожидания (TagWaiter)
- **Expected:** либо корректное восстановление, либо controlled failure (без вечного ожидания).

---

## 9) Queue/Channel invariants

### EC-26: Ошибка в очереди есть, а сигнал в channel был dropped
- **Expected:** есть backup trigger (план) → `HandleErrorsIfAny` всё равно запускается.

### EC-27: Race при Skip: порядок `DequeueError()` vs `ClearFailedState()`
- **Expected:** сохраняем порядок (Dequeue BEFORE ClearFailedState) чтобы не терять новую ошибку.

---

## 10) Soak / load (длительная работа)

### EC-28: 100 циклов подряд OK/NOK/Repeat/Reset
- **Expected:** нет утечек подписок, нет накопления callback’ов, нет деградации UI/логов, нет ростов памяти.

### EC-29: Long pause (AutoReady OFF 10+ минут)
- **Expected:** таймауты учитывают pause (если pause-aware), система корректно продолжает после resume.

