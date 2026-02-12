# Change: Защита от зависания при Skip и переходе блока

## Why
Короткие импульсы PLC End/Error теряются при ожидании по подписке, что приводит к зависанию между блоками. Нужна защита от вечного ожидания и явный сигнал оператору.

## What Changes
- Прямое чтение PLC-тегов BlockEnd/BlockError перед ожиданиями для фиксации Skip.
- Сброс ожиданий после Skip с прямым чтением текущих значений.
- Таймаут 10 секунд при ожидании "все колонки idle": пауза и уведомление оператора без NOK.
- Обновление RetrySkipGuide.md.

## Impact
- Affected specs: error-coordinator
- Affected code: ErrorCoordinator.Resolution, TestExecutionCoordinator.PlcErrorSignals, TestExecutionCoordinator.Execution
- Affected docs: Docs/execution/RetrySkipGuide.md
