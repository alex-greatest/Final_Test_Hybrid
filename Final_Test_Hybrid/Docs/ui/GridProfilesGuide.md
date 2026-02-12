# Руководство по профилям DataGrid

## Цель

Описать профильный подход к стилизации таблиц в проекте и исключить неуправляемые глобальные override.

## Профили и назначение

| Профиль | Класс контейнера | Класс грида | Назначение |
|------|------|------|------|
| Unified | `grid-unified-host` | `grid-unified` | Базовый единый стиль таблиц на большинстве экранов |
| Main Legacy | нет отдельного host | `main-grid-legacy` | Исторический компактный вид таблицы шагов на главном экране |
| Overview IO | нет отдельного host | `overview-grid-io` | Таблицы калибровки/IO во вкладке «Обзор» |

## Карта использования профилей

### `grid-unified-host` + `grid-unified`

- `Components/Archive/ArchiveGrid.razor`
- `Components/Errors/ActiveErrorsGrid.razor`
- `Components/Errors/ErrorHistoryGrid.razor`
- `Components/Parameters/ParametersTab.razor`
- `Components/Results/TestResultsGrid.razor`
- `Components/Results/StepHistoryGrid.razor`
- `Components/Results/StepTimingsGrid.razor`
- `Components/Results/ActiveTimersGrid.razor`
- `Components/Engineer/StandDatabase/BoilerTypesGrid.razor`
- `Components/Engineer/StandDatabase/ErrorSettingsTemplatesGrid.razor`
- `Components/Engineer/StandDatabase/StepFinalTestsGrid.razor`
- `Components/Engineer/StandDatabase/Recipe/RecipesGrid.razor`
- `Components/Engineer/StandDatabase/ResultSettings/ResultSettingsSimpleGrid.razor`
- `Components/Engineer/StandDatabase/ResultSettings/ResultSettingsRangeGrid.razor`
- `Components/Engineer/StandDatabase/ResultSettings/ResultSettingsBoardGrid.razor`

### `main-grid-legacy`

- `Components/Main/TestSequenseGrid.razor`

### `overview-grid-io`

- `Components/Overview/AiCallCheck.razor`
- `Components/Overview/RtdCalCheck.razor`
- `Components/Overview/PidRegulatorCheck.razor`

## Unified-профиль: ключевые правила

Источник: `wwwroot/css/app.css`.

1. Контейнер `grid-unified-host` обязан быть flex-контейнером на всю высоту.
2. `grid-unified` задаёт единый масштаб типографики и границ.
3. Заголовок настраивается через `th` и внутренние блоки:
   `.rz-cell-data`, `.rz-column-title-content`, `.rz-sortable-column`, `.rz-column-title`.
4. В edit-режиме используются отдельные правила для `rz-textbox`, `rz-dropdown`, `rz-textarea`.
5. Кнопки действий в ячейках имеют фиксированный компактный размер.

## Main Legacy: ключевые правила

Источники: `Components/Main/TestSequenseGrid.razor`, `Components/Main/TestSequenseGrid.razor.css`.

1. Профиль используется только для главной таблицы последовательности теста.
2. Горизонтальный скролл скрыт; приоритет на компактность и читаемость статусов.
3. Статусы окрашиваются по состоянию шага (`error`, `success`, `running`).
4. Не расширять этот профиль для других экранов.

## Overview IO: ключевые правила

Источник: `wwwroot/css/app.css`.

1. Профиль ориентирован на 19px типографику и режим редактирования числовых/текстовых полей.
2. Настройки заголовка используют тот же анти-clipping подход, что и unified.
3. Профиль применяется только в IO/калибровочных таблицах вкладки «Обзор».

## Паттерн изменения шапки DataGrid

При любом профиле нужно править не только `th`, но и внутренние контейнеры:

- `.rz-cell-data`
- `.rz-column-title-content`
- `.rz-sortable-column`
- `.rz-column-title`

Использование только `.rz-grid-table thead th*` не считается устойчивым.

## Как добавить новый профиль (пошагово)

1. Создать новый opt-in класс в `wwwroot/css/app.css`:
   `new-grid-host` и `new-grid` (или иной парный нейминг).
2. Подключить классы точечно в нужном Razor-компоненте.
3. Разделить стили header/body/edit отдельными блоками.
4. Проверить, что существующие профили не изменились визуально.
5. Добавить профиль в этот документ.

## Do

- Делать профиль изолированным и предсказуемым.
- Проверять типографику шапки и тела отдельно.
- Держать `grid-unified` как основной стиль для типовых таблиц.

## Don't

- Не менять `grid-unified` под уникальный кейс одного экрана.
- Не переводить `TestSequenseGrid` на `grid-unified`.
- Не использовать глобальный broad override для всех `rz-data-grid`.

## Чек-лист ревью для DataGrid

1. Выбран существующий профиль или добавлен новый opt-in профиль.
2. У контейнера корректный full-height layout (`display:flex`, `min-height:0`, `height:100%`).
3. Заголовок не обрезается в узких колонках.
4. Редакторы в ячейках не ломают высоту строк.
5. Изменение не влияет на `main-grid-legacy`.
