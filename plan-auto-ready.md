# План фикса: AutoReady OFF, stale End и Retry/Skip при паузе

**Summary**
Исправить прохождение шага при выключенном AutoReady, блокировать Retry/Skip во время паузы, и защититься от `End=true` перед стартом шага.

**Public API / Interfaces**
Изменений публичных API нет.

**Implementation Plan**
1. **Pause‑guard перед фиксацией результата**
   - Файл: `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.cs`
   - В `ExecuteStepCoreAsync` добавить `await pauseToken.WaitWhilePausedAsync(ct);` между `ExecuteAsync` и `ProcessStepResult`.
2. **Pause‑guard перед Retry/Skip**
   - Файл: `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs`
   - В `ProcessRetryAsync` и `ProcessSkipAsync` добавить `await _pauseToken.WaitWhilePausedAsync(ct);` перед началом действий.
3. **Pause‑guard в RetryLastFailedStepAsync**
   - Файл: `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.cs`
   - В начале `RetryLastFailedStepAsync` добавить `await pauseToken.WaitWhilePausedAsync(ct);`.
4. **Глобальная защита от “End already true”**
   - Создать общий helper (например, в `PlcBlockTagHelper` или отдельном сервисе), который:
     - получает `EndTag` для PLC‑шага,
     - выполняет `WaitForFalseAsync(endTag, timeout: 5s)`,
     - при таймауте возвращает `TestStepResult.Fail("PLC не сбросил End")`.
   - В каждом PLC‑шаге перед `Start=true` вызвать этот helper.
   - Таймаут ожидания End=false: **5 секунд**.
5. **Логи AutoReady OFF/ON**
   - Файл: `Final_Test_Hybrid/Services/Main/AutoReadySubscription.cs`
   - Добавить лог при изменении `_isReady` (OFF/ON).
   - Файл: `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.cs`
   - Логи: “AutoReady OFF → pause” и “AutoReady ON → resume”.

**Test Cases / Scenarios**
1. AutoReady OFF во время шага → шаг не фиксируется до Resume.
2. AutoReady OFF + Retry → Retry ждёт Resume.
3. End остаётся true → шаг падает с “PLC не сбросил End”, работает Retry/Skip.
4. AutoReady ON → Resume, шаг продолжает выполнение.
5. PLC connection lost → поведение прежнее (прерывание теста).

**Assumptions**
AutoReady OFF → пауза; AutoReady ON → авто‑продолжение. Timeout End=false = 5 секунд; при таймауте: `Fail("PLC не сбросил End")`.

**Out of Scope**
Изменение поведения при PLC connection lost.
