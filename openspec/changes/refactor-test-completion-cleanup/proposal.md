# Change: Refactor Test Completion Cleanup Flow

## Why

Текущая логика очистки данных при завершении теста разбросана и неконсистентна:
- При завершении теста (OK/NOK) данные должны сохраняться для проверки оператором
- При начале нового теста старые результаты должны очищаться
- Сейчас очистка частично происходит в разных местах, что приводит к путанице

## What Changes

1. **TestCompleted** — добавить очистку:
   - Грид (ClearAllExceptScan)
   - Время шагов (StepTimingService.Clear)
   - Рецепты (RecipeProvider.Clear)
   - BoilerState (Clear)
   - История ошибок: **выключить** (IsHistoryEnabled = false), но **не чистить**
   - Результаты: **не чистить** — для проверки оператором

2. **Начало нового теста** (IsProcessing = true + включение истории):
   - Очистить историю ошибок (ClearHistory)
   - Очистить результаты (TestResultsService.Clear)

3. Унифицировать очистку для OK и NOK завершения

## Impact

- Affected specs: `test-lifecycle` (новый)
- Affected code:
  - `PreExecutionCoordinator.cs` — методы очистки
  - `PreExecutionCoordinator.MainLoop.cs` — HandleCycleExit, HandleTestCompletionAsync
  - `PreExecutionCoordinator.Pipeline.cs` — EnableHistory + очистка в начале теста
  - `PreExecutionInfrastructure` — добавить TestResultsService
