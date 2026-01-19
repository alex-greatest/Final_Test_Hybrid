# Tasks: Refactor Test Completion Cleanup Flow

## 1. Preparation

- [ ] 1.1 Добавить ITestResultsService в PreExecutionInfrastructure
- [ ] 1.2 Добавить IRecipeProvider в PreExecutionInfrastructure (если отсутствует)

## 2. Test Completion Cleanup

- [ ] 2.1 Создать метод `ClearForTestCompletion()` в PreExecutionCoordinator
  - ClearAllExceptScan (грид)
  - StepTimingService.Clear (время шагов)
  - RecipeProvider.Clear (рецепты)
  - BoilerState.Clear (состояние котла)
  - IsHistoryEnabled = false (выключить историю, но не чистить)

- [ ] 2.2 Вызвать `ClearForTestCompletion()` в HandleCycleExit для CycleExitReason.TestCompleted

## 3. Scan Timer Reset

- [ ] 3.1 Добавить вызов `StepTimingService.ResetScanTiming()` в `SetAcceptingInput(true)`
  - Таймер сканирования сбрасывается и запускается заново при готовности к новому штрихкоду

## 4. New Test Start Cleanup

- [ ] 4.1 Создать метод `ClearForNewTestStart()` в PreExecutionCoordinator
  - ErrorService.ClearHistory (история ошибок)
  - TestResultsService.Clear (результаты)

- [ ] 4.2 Вызвать `ClearForNewTestStart()` в ExecutePreExecutionPipelineAsync перед включением IsHistoryEnabled

- [ ] 4.3 Вызвать `ClearForNewTestStart()` в ExecuteRepeatPipelineAsync перед включением IsHistoryEnabled

## 5. Cleanup Existing Code

- [ ] 5.1 Удалить дублирующиеся вызовы очистки из ClearForRepeat и ClearForNokRepeat
- [ ] 5.2 Обновить CLAUDE.md — раздел про ErrorService и очистку данных

## 6. Verification

- [ ] 6.1 Проверить сценарий OK завершения — данные сохраняются для просмотра
- [ ] 6.2 Проверить сценарий NOK завершения — данные сохраняются для просмотра
- [ ] 6.3 Проверить сценарий нового теста — старые данные очищаются
- [ ] 6.4 Проверить сценарий OK Repeat — старые данные очищаются
- [ ] 6.5 Проверить сценарий NOK Repeat — старые данные очищаются
- [ ] 6.6 Проверить таймер сканирования — сбрасывается при готовности к новому штрихкоду
