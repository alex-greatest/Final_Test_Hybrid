# Обновление фазы 4: PreExecutionCoordinator.Pipeline

## Краткое резюме
Сделать рефакторинг Pipeline через `InitializeTestRunning()`, но с учётом текущего fix changeover: вместо `StopChangeoverTimer()` использовать `StopChangeoverAndAllowRestart()` и сохранить диагностические логи.

## Изменения интерфейсов/публичных API
- Публичных изменений нет.
- Используется существующий приватный метод `StopChangeoverAndAllowRestart()`.

## Шаги (фаза 4)
1. В `PreExecutionCoordinator.Pipeline.cs` выделить общий блок старта теста в метод `InitializeTestRunning()` (если ещё не выделен).
2. Внутри `InitializeTestRunning()`:
   - Оставить `ClearForNewTestStart()`, `AddAppVersionToResults()`, `infra.ErrorService.IsHistoryEnabled = true`, `state.BoilerState.SetTestRunning(true)`, `state.BoilerState.StartTestTimer()`.
   - Заменить `StopChangeoverTimer()` на `StopChangeoverAndAllowRestart()`.
   - Сохранить диагностические логи вокруг stop-операции (если уже есть).
3. В `ExecutePreExecutionPipelineAsync()` и `ExecuteRepeatPipelineAsync()` заменить дублированные блоки на `InitializeTestRunning()`.

## Тесты/сценарии
- Обычный запуск теста после скана: changeover-таймер остановлен корректно и готов к следующему запуску.
- Повторный запуск (Repeat) после завершённого цикла: changeover не «зависает» и может стартовать снова.
- Не влияет на уже реализованный багфикс: после HardReset changeover всё ещё стартует.

## Допущения
- Диагностические логи `=== DIAG ===` остаются временно и будут сняты после подтверждения исправления.
- Поведение changeover после HardReset не изменяется этим рефактором.
