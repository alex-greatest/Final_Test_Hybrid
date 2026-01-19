# Test Completion Specification

## ADDED Requirements

### Requirement: Reset Handling During Completion
Система SHALL скрывать изображение результата теста при получении сигнала сброса PLC.

#### Scenario: Soft reset during result display
- **WHEN** изображение результата отображается (`ShowResultImage = true`)
- **AND** поступает сигнал мягкого сброса (`PlcResetCoordinator.OnForceStop`)
- **THEN** изображение скрывается немедленно (`ShowResultImage = false`)
- **AND** UI переключается на отображение грида

#### Scenario: Hard reset during result display
- **WHEN** изображение результата отображается (`ShowResultImage = true`)
- **AND** поступает сигнал жёсткого сброса (`ErrorCoordinator.OnReset`)
- **THEN** изображение скрывается немедленно (`ShowResultImage = false`)
- **AND** UI переключается на отображение грида

#### Scenario: Reset during save/prepare dialogs
- **WHEN** показан диалог ошибки сохранения или подготовки
- **AND** поступает любой сигнал сброса
- **THEN** диалог закрывается с результатом `false` (отмена)
- **AND** операция сохранения/подготовки прерывается

### Requirement: TestCompletionUiState Disposal
Система SHALL корректно освобождать подписки при завершении работы.

#### Scenario: Dispose unsubscribes from events
- **WHEN** вызывается `Dispose()` на `TestCompletionUiState`
- **THEN** все подписки на `OnForceStop` и `OnReset` удаляются
- **AND** утечки памяти не происходит

### Requirement: Reset Cleanup Parity
Система SHALL очищать при сбросе те же данные, что и при завершении теста.

#### Scenario: Soft reset clears step timings
- **WHEN** поступает сигнал мягкого сброса
- **THEN** время шагов очищается (`StepTimingService.Clear()`)
- **AND** очистка происходит по сигналу AskEnd

#### Scenario: Hard reset clears step timings
- **WHEN** поступает сигнал жёсткого сброса
- **THEN** время шагов очищается немедленно (`StepTimingService.Clear()`)

#### Scenario: Reset clears recipe provider
- **WHEN** поступает любой сигнал сброса (мягкий или жёсткий)
- **THEN** рецепты очищаются (`RecipeProvider.Clear()`)

#### Scenario: Reset cleanup matches test completion
- **WHEN** сброс выполнен
- **THEN** очищены: BoilerState, CurrentBarcode, Grid, StepTimingService, RecipeProvider
- **AND** отключено: IsHistoryEnabled = false
- **AND** НЕ очищены: ErrorService.History, TestResultsService (чистятся при новом тесте)

#### Scenario: NOK repeat clears recipe provider
- **WHEN** запрошен NOK повтор теста
- **THEN** рецепты очищаются (`RecipeProvider.Clear()`)
- **AND** рецепты будут загружены заново в `ExecuteNokRepeatPipelineAsync()`
