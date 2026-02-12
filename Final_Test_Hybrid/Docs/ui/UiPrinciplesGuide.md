# Руководство по UI-принципам

## Цель

Зафиксировать устойчивые правила UI-разработки в `Final_Test_Hybrid`, чтобы изменения интерфейса не ломали runtime-поведение и не создавали побочные эффекты в критичных сценариях.

## Область действия

- Основные вкладки приложения (`MyComponent.razor`).
- Профили DataGrid и общие стили (`wwwroot/css/app.css`).
- Главные кнопки управления и индикации на экране оператора.

## Базовые принципы

1. Сначала безопасность, потом визуал.
2. Визуальная блокировка без runtime-gating недопустима.
3. Новый визуальный режим = новый opt-in класс, а не глобальная подкрутка существующего профиля.
4. Изменения шапки DataGrid делаются через устойчивый набор селекторов: `th` + внутренние контейнеры.
5. Табличные и вкладочные контейнеры должны жить в full-height layout (`display:flex`, `flex-direction:column`, `min-height:0`, `height:100%`).

## Зафиксированные UI-инварианты

| Инвариант | Где применяется | Источник |
|------|------|------|
| Единый табличный стиль включается только через `grid-unified-host` + `grid-unified` | Экраны с унифицированным DataGrid | `wwwroot/css/app.css` |
| Главный грид последовательности работает в отдельном профиле `main-grid-legacy` | Только `TestSequenseGrid` | `Components/Main/TestSequenseGrid.razor` |
| Для overview-редакторов используется `overview-grid-io` | `AiCallCheck`, `RtdCalCheck`, `PidRegulatorCheck` | `Components/Overview/*.razor` |
| Широкие override вида `::deep .rz-data-grid*` запрещены в родительских вкладках | Все вкладки | `AGENTS.md` |
| В `LogViewerTab` структура фиксирована: две вкладки `Лог-файл` и `Время шагов` | Вкладка `Лог` | `Components/Logs/LogViewerTab.razor` |

## Слойность стилей

| Слой | Назначение | Пример |
|------|------|------|
| `wwwroot/css/app.css` | Глобальные токены и opt-in профили (layout, grid, dialog) | `.grid-unified`, `.overview-grid-io`, `.app-tabs-90` |
| `*.razor.css` компонента | Локальная геометрия и внешний вид конкретного экрана | `MyComponent.razor.css`, `TestSequenseGrid.razor.css` |
| Inline `Style=` | Только точечные частные случаи | отдельные отступы/ширины контейнера |

## Runtime-гейтинг для UI-блокировок

UI-компонент, который блокирует действия оператора, должен опираться на runtime-состояние сервиса, а не только на локальный флаг интерфейса.

Обязательный пример: блокировка настроек (`Docs/ui/SettingsBlockingGuide.md`) использует одновременно:

- `PreExecutionCoordinator.IsProcessing`
- `SettingsAccessStateManager.CanInteract`
- `PlcResetCoordinator.IsActive`
- `ErrorCoordinator.CurrentInterrupt`

## Do

- Делать opt-in стиль через class на контейнере и class на DataGrid.
- Разделять стили header/body/edit в таблицах.
- Проверять поведение UI на `PlcReset`, `Interrupt`, `PreExecution`.
- Поддерживать читаемую типографику и контраст для HMI.

## Don't

- Не использовать UI как единственный барьер для небезопасной операции.
- Не переиспользовать профиль таблицы для другого визуального режима.
- Не расширять глобальные селекторы так, чтобы они меняли все таблицы сразу.
- Не менять поведение `main-grid-legacy` под нужды других экранов.

## Чек-лист перед merge UI-изменения

1. Новый визуальный режим оформлен отдельным opt-in классом.
2. Для DataGrid-поправок учтены селекторы `th` и внутренние контейнеры (`.rz-cell-data`, `.rz-column-title-content`, `.rz-sortable-column`, `.rz-column-title`).
3. Вкладка/контейнер не теряет высоту и скролл (`min-height:0`, `height:100%`).
4. Для блокирующих кнопок/контролов есть runtime-проверка в сервисе/координаторе.
5. Изменения сверены с `AGENTS.md` и `Docs/ui/SettingsBlockingGuide.md`.
