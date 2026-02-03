# План фазы 5: Refactor PreExecutionCoordinator.Retry

## Краткое резюме
Переписать `ExecuteRetryLoopAsync` в `PreExecutionCoordinator.Retry.cs` с использованием локальных функций для читаемости, без изменения поведения. Сохранить все логи, порядок `await`, и семантику `ErrorScope` (try/finally).

## Изменения интерфейсов/публичных API
- Публичных изменений нет.
- Изменения только внутри `PreExecutionCoordinator.Retry.cs`.

## Детальный план реализации
1. В `ExecuteRetryLoopAsync` оставить внешний `try/finally`, где `ErrorScope` создаётся до цикла и очищается в `finally`.
2. Внутри цикла `while (currentResult.IsRetryable)` заменить линейный код на локальные функции:
   - `ProcessIteration()` — выполняет показ диалога, `WaitForResolutionAsync`, ветвление по `Retry/не Retry`.
   - `ExecuteRetry()` — отправка `SendAskRepeatAsync`, обработка `TimeoutException`, закрытие диалога, `errorScope.Clear()`, вызов `RetryStepAsync`.
3. Логирование:
   - Все существующие `infra.Logger.LogInformation/LogError` должны остаться в том же порядке.
   - Обязательно сохранить:
     - “Retry loop: IsRetryable=true…”
     - “Диалог показан, ожидаем WaitForResolutionAsync…”
     - “WaitForResolutionAsync вернул: …”
     - “Отправляем SendAskRepeatAsync…”
     - “Block.Error не сброшен… жёсткий стоп…”
     - Логи выхода из цикла (“не Retry, выходим…”), если они есть.
4. Семантика `await`:
   - Никакого fire-and-forget; все `await` остаются обязательными.
   - `coordinators.DialogCoordinator.CloseBlockErrorDialog()` вызывается в тех же местах, что и сейчас.
5. Поведение `ErrorScope`:
   - Создаётся один раз на всю `ExecuteRetryLoopAsync`.
   - `errorScope.Clear()` вызывается при успешном retry и в `finally`.
6. Никаких дополнительных побочных эффектов:
   - Не менять исключения, return-ветвления, и статус `IsRetryable`.

## Тесты/сценарии проверки
- Retry-петля при ошибке шага:
  - Диалог появляется, логируется ожидание `WaitForResolutionAsync`.
  - При `Retry` выполняется `SendAskRepeatAsync`, шаг повторяется.
- При `TimeoutException` в `SendAskRepeatAsync`:
  - Лог с ошибкой сохранён.
  - Диалог закрывается.
  - Идёт `HandleInterruptAsync`, возвращается `Fail`.
- При `Skip/SoftStop/HardReset`:
  - Выход из цикла корректен, лог “не Retry, выходим…” сохранён.
- Проверка порядка логов — как часть ручной валидации.

## Допущения
- Поведение и порядок логов — ключевой контракт, их меняем только если есть прямое указание.
- Никаких изменений в UI/координаторах и внешних сервисах.
