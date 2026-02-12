# UI-документация Final_Test_Hybrid

## Назначение

Папка `Docs/ui` фиксирует рабочие правила для интерфейса HMI: базовые принципы, профили DataGrid, паттерны кнопок и структуру главного экрана.

## Порядок чтения

1. `UiPrinciplesGuide.md` — общие инварианты и правила внесения UI-изменений.
2. `GridProfilesGuide.md` — профили таблиц (`grid-unified`, `main-grid-legacy`, `overview-grid-io`).
3. `ButtonPatternsGuide.md` — семантика кнопок и правила блокировок.
4. `MainScreenGuide.md` — композиция и логика вкладки «Главный экран».
5. `SettingsBlockingGuide.md` — отдельный гайд по блокировке галочек в настройках.

## Карта задач

| Задача | Документ |
|------|------|
| Понять UI-инварианты и границы изменений | [UiPrinciplesGuide.md](UiPrinciplesGuide.md) |
| Добавить/изменить DataGrid-профиль | [GridProfilesGuide.md](GridProfilesGuide.md) |
| Проверить корректность кнопок и блокировок | [ButtonPatternsGuide.md](ButtonPatternsGuide.md) |
| Разобрать структуру главного экрана | [MainScreenGuide.md](MainScreenGuide.md) |
| Изменить блокировку настроек | [SettingsBlockingGuide.md](SettingsBlockingGuide.md) |

## Источники истины в коде

- `Final_Test_Hybrid/wwwroot/css/app.css`
- `Final_Test_Hybrid/MyComponent.razor`
- `Final_Test_Hybrid/MyComponent.razor.css`
- `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor`
- `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor.css`
- `Final_Test_Hybrid/Components/Main/OperatorInfo.razor`
- `Final_Test_Hybrid/Components/Main/BoilerOrder.razor`
- `Final_Test_Hybrid/Components/Errors/ErrorResetButton.razor`

