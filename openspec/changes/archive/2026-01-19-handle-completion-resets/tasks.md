# Tasks: Handle Completion Resets

## 1. Выровнять очистку при сбросах и NOK повторе

- [x] 1.1 Добавить `infra.StepTimingService.Clear()` в `ClearStateOnReset()`
- [x] 1.2 Добавить `infra.RecipeProvider.Clear()` в `ClearStateOnReset()`
- [x] 1.3 Добавить `infra.RecipeProvider.Clear()` в `ClearForNokRepeat()`
- [x] 1.4 Добавить лог об очистке в `ClearStateOnReset()`

## 2. Подписки на сбросы для TestCompletionUiState

- [x] 2.1 Добавить зависимости в `TestCompletionUiState` (PlcResetCoordinator, IErrorCoordinator)
- [x] 2.2 Добавить подписки в конструкторе: `OnForceStop += HideImage`, `OnReset += HideImage`
- [x] 2.3 ~~Реализовать `IDisposable` для отписки~~ — не требуется (singleton→singleton, см. CLAUDE.md)
- [x] 2.4 ~~Обновить регистрацию в `StepsServiceExtensions.cs`~~ — не требуется (DI автоматически резолвит)

## 3. Исправить NOK Repeat Flow

- [x] 3.1 Убрать блок ReworkDialog из `HandleNokRepeatAsync` (TestCompletionCoordinator.Repeat.cs)
- [x] 3.2 Удалить event `OnReworkDialogRequested` из `TestCompletionCoordinator.cs`
- [x] 3.3 Удалить `HandleReworkForNokRepeatAsync` из `BoilerInfo.razor`
- [x] 3.4 Удалить подписку/отписку на `CompletionCoordinator.OnReworkDialogRequested` в `BoilerInfo.razor`

## 4. Тестирование

- [x] 4.1 Проверить очистку времени шагов при мягком сбросе
- [x] 4.2 Проверить очистку времени шагов при жёстком сбросе
- [x] 4.3 Проверить скрытие картинки при мягком сбросе (кнопка Reset на PLC во время показа результата)
- [x] 4.4 Проверить скрытие картинки при жёстком сбросе (потеря связи во время показа результата)
- [x] 4.5 Проверить что диалоги ошибок закрываются при сбросе (уже работает)
- [x] 4.6 Проверить NOK повтор: ReworkDialog НЕ показывается сразу
- [x] 4.7 Проверить NOK повтор: ReworkDialog показывается в ScanBarcodeMesStep если MES требует
