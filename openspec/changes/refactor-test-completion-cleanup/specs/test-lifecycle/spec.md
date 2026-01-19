## ADDED Requirements

### Requirement: Test Completion Cleanup

При завершении теста (OK или NOK) система ДОЛЖНА очистить рабочие данные, но сохранить результаты для просмотра оператором.

Очищаемые данные:
- Грид шагов (StatusReporter.ClearAllExceptScan)
- Время шагов (StepTimingService.Clear)
- Рецепты (RecipeProvider.Clear)
- Состояние котла (BoilerState.Clear)
- Запись истории (IsHistoryEnabled = false)

Сохраняемые данные:
- История ошибок (ErrorService.History)
- Результаты измерений (TestResultsService)

#### Scenario: OK test completion preserves results
- **GIVEN** тест завершился успешно (OK)
- **WHEN** вызывается HandleCycleExit(TestCompleted)
- **THEN** грид, время, рецепты и BoilerState очищены
- **AND** история ошибок и результаты сохранены для просмотра

#### Scenario: NOK test completion preserves results
- **GIVEN** тест завершился с ошибками (NOK)
- **WHEN** вызывается HandleCycleExit(TestCompleted)
- **THEN** грид, время, рецепты и BoilerState очищены
- **AND** история ошибок и результаты сохранены для просмотра

### Requirement: New Test Start Cleanup

При начале нового теста система ДОЛЖНА очистить результаты предыдущего теста.

Очищаемые данные:
- История ошибок (ErrorService.ClearHistory)
- Результаты измерений (TestResultsService.Clear)

Очистка происходит перед включением записи истории (IsHistoryEnabled = true).

#### Scenario: New test clears previous results
- **GIVEN** предыдущий тест завершён и результаты сохранены
- **WHEN** сканируется новый штрихкод и начинается ExecutePreExecutionPipelineAsync
- **THEN** история ошибок очищена
- **AND** результаты измерений очищены
- **THEN** включается IsHistoryEnabled = true

#### Scenario: OK Repeat clears previous results
- **GIVEN** оператор запросил OK повтор теста
- **WHEN** начинается ExecuteRepeatPipelineAsync
- **THEN** история ошибок очищена
- **AND** результаты измерений очищены
- **THEN** включается IsHistoryEnabled = true

#### Scenario: NOK Repeat clears previous results
- **GIVEN** оператор запросил NOK повтор теста
- **WHEN** начинается ExecuteNokRepeatPipelineAsync (внутри ExecutePreExecutionPipelineAsync)
- **THEN** история ошибок очищена
- **AND** результаты измерений очищены
- **THEN** включается IsHistoryEnabled = true

### Requirement: Scan Timer Reset

При готовности системы к приёму нового штрихкода таймер сканирования ДОЛЖЕН сброситься и запуститься заново.

#### Scenario: Scan timer resets on new cycle
- **GIVEN** предыдущий тест завершён
- **WHEN** система готова к приёму нового штрихкода (SetAcceptingInput = true)
- **THEN** таймер сканирования сбрасывается на 00:00
- **AND** таймер сканирования запускается заново
