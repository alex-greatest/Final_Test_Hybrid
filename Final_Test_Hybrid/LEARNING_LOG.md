# LEARNING LOG

Короткий активный журнал. Детали, длинные расследования и старые записи храним в `LEARNING_LOG_ARCHIVE.md`.

## Политика роста
- Активный файл: только оперативный индекс (последние 30 дней или до 40 записей).
- Формат записи фиксированный: `Что изменили` / `Почему` / `Риск/урок` / `Ссылки`.
- Всё подробное и устаревающее переносим в архив без потери фактов.

## Шаблон записи
### YYYY-MM-DD (тема)
- Что изменили:
- Почему:
- Риск/урок:
- Ссылки: `path1`, `path2`

## Активные записи

### 2026-02-10 (Overview: немедленная выдача cached-значений как в Hand Program)
- Что изменили: в `MyComponent.razor` для всех overview-панелей (`Gas/Heating/HotWater/Inputs/Outputs`) включили `EmitCachedValueImmediately="true"`; в `OutputsPanel2.razor` добавили параметр `EmitCachedValueImmediately` и пробросили его в оба `ValueIndicator`.
- Почему: в `Обзор` часть значений оставалась пустой до первого изменения тега, тогда как в `HandProgramDialog` те же панели показывали кэш сразу.
- Риск/урок: при смешанном использовании `EmitCachedValueImmediately` между экранами появляются ложные различия UI-состояния при одинаковых источниках OPC-подписок.
- Ссылки: `Final_Test_Hybrid/MyComponent.razor`, `Final_Test_Hybrid/Components/Overview/OutputsPanel2.razor`, `Final_Test_Hybrid/Components/Engineer/Modals/HandProgramDialog.razor`

### 2026-02-10 (ErrorHistoryGrid: реактивная шапка last boiler/дата)
- Что изменили: в `ErrorHistoryGrid` перевели подписку с `BoilerState.OnCleared` на `BoilerState.OnChanged`, синхронно обновили отписку и добавили guard по `_disposed` в обработчике изменения состояния.
- Почему: шапка с `LastSerialNumber`/`LastTestCompletedAt` должна появляться, обновляться и скрываться в те же моменты, что и в `TestResultsGrid` (включая `SaveLastSerialNumber` и `ClearLastTestInfo`).
- Риск/урок: подписка только на `OnCleared` теряет часть переходов `BoilerState`; для UI-индикаторов предыдущего теста нужен полный поток `OnChanged`.
- Ссылки: `Final_Test_Hybrid/Components/Errors/ErrorHistoryGrid.razor`, `Final_Test_Hybrid/Components/Results/TestResultsGrid.razor`, `Final_Test_Hybrid/Models/BoilerState.cs`

### 2026-02-10 (Overview: заголовки DataGrid по паттерну unified)
- Что изменили: в `overview-grid-io` заменили header-селекторы `.rz-grid-table thead th*` на паттерн из `grid-unified`: `.overview-grid-io th` + внутренние контейнеры (`th .rz-cell-data`, `th .rz-column-title-content`, `th .rz-sortable-column`, `.rz-column-title`); размер текста в body/edit оставили `19px`.
- Почему: в Overview заголовок обрезался по высоте; паттерн `grid-unified` уже показал более стабильное поведение в этом проекте.
- Риск/урок: для Radzen DataGrid устойчивость заголовка зависит от покрытия внутренних контейнеров и прицельных селекторов `th`, а не только от `thead`-селектора.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/LEARNING_LOG.md`

### 2026-02-09 (Overview: фиксация роста шрифта ячеек + anti-clipping заголовков)
- Что изменили: в профиле `overview-grid-io` добавили `thead th` и внутренние контейнеры заголовков по паттерну `grid-unified` (`overflow: visible`, `height: auto`, `text-overflow: clip`), а также усилили применение шрифта к содержимому через `.rz-grid-table td, td *` на `19px`.
- Почему: после увеличения шрифта заголовки начали обрезаться по высоте, а текст ячеек в templated-контенте визуально не увеличивался.
- Риск/урок: для Radzen DataGrid одного `.rz-cell-data` недостаточно; для стабильного результата нужны и `thead`-контейнеры, и покрытие содержимого `td`.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/Components/Overview/AiCallCheck.razor`, `Final_Test_Hybrid/Components/Overview/RtdCalCheck.razor`, `Final_Test_Hybrid/Components/Overview/PidRegulatorCheck.razor`

### 2026-02-09 (Overview: +3px к заголовкам и содержимому трёх calibration-гридов)
- Что изменили: для профиля `overview-grid-io` подняли размер шрифта с `16px` до `19px` для заголовков колонок, текста ячеек и элементов редактирования.
- Почему: требовалось увеличить читаемость трёх Overview-таблиц (`AiCallCheck`, `RtdCalCheck`, `PidRegulatorCheck`) ровно на `+3px`.
- Риск/урок: увеличение шрифта в DataGrid нужно делать одновременно для display и edit-состояний, иначе появляется визуальный «скачок» при редактировании.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/Components/Overview/AiCallCheck.razor`, `Final_Test_Hybrid/Components/Overview/RtdCalCheck.razor`, `Final_Test_Hybrid/Components/Overview/PidRegulatorCheck.razor`

### 2026-02-09 (Overview: единый источник типографики через overview-grid-io)
- Что изменили: убрали локальные `::deep`-дубли из `AiCallCheck.razor.css`, `RtdCalCheck.razor.css`, `PidRegulatorCheck.razor.css`; оставили типографику только через класс `overview-grid-io` в `app.css`.
- Почему: требовалось стабильно выровнять стиль таблиц Overview относительно `IoEditorDialog` без расхождения между глобальными и scoped-правилами.
- Риск/урок: когда один визуальный профиль задан в двух слоях (shared + component-scoped), результат становится недетерминированным; нужен single source of truth.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/Components/Overview/AiCallCheck.razor.css`, `Final_Test_Hybrid/Components/Overview/RtdCalCheck.razor.css`, `Final_Test_Hybrid/Components/Overview/PidRegulatorCheck.razor.css`

### 2026-02-09 (UI: заголовки unified 1.5rem + Overview в стиле IoEditorDialog)
- Что изменили: увеличили размер заголовков колонок unified-гридов до `1.5rem` (вес `700`) и добавили shared-класс `overview-grid-io` для таблиц `AiCallCheck`, `RtdCalCheck`, `PidRegulatorCheck` с типографикой `16px` для заголовков/ячеек/редактирования как в `IoEditorDialog`.
- Почему: требовалось сделать заголовки unified заметнее и выровнять визуальный профиль таблиц во вкладке Overview под эталонный стиль инженерного редактора.
- Риск/урок: для повторяемости UI лучше задавать профиль таблиц через явный class opt-in, а не через разрозненные локальные `::deep` правила.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/Components/Overview/AiCallCheck.razor`, `Final_Test_Hybrid/Components/Overview/RtdCalCheck.razor`, `Final_Test_Hybrid/Components/Overview/PidRegulatorCheck.razor`

### 2026-02-09 (UI: увеличен и утяжелён текст заголовков колонок)
- Что изменили: для unified-гридов подняли типографику заголовков колонок до `1.3rem` и `700`, с явной фиксацией на селекторах `thead th`.
- Почему: требовалось сделать именно текст заголовков колонок крупнее и жирнее, без изменения ячеек/вкладок.
- Риск/урок: для стабильного визуального результата заголовки DataGrid нужно задавать не только общим селектором, но и на уровне `thead th`.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/LEARNING_LOG.md`

### 2026-02-09 (UI: ячейки unified возвращены на 19px)
- Что изменили: в `grid-unified` вернули размер текста данных с `24px` на `19px` для `.rz-cell-data`, `.rz-grid-table td .rz-cell-data` и `.cell-text`; профиль заголовков оставили без изменений.
- Почему: требовалось сохранить рабочий размер/жирность заголовков и уменьшить только текст в ячейках.
- Риск/урок: при массовой стилизации таблиц заголовки и данные должны настраиваться раздельно, иначе сложно удерживать ожидаемый визуальный баланс.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/Components/Engineer/StandDatabase/Recipe/RecipesGrid.razor`

### 2026-02-09 (UI: возврат размера заголовков unified при сохранении ячеек)
- Что изменили: в `grid-unified` вернули типографику заголовков к прежнему виду (`1.1rem`, `600`, `uppercase`), не меняя текущий размер текста в ячейках.
- Почему: после фикса приоритета scoped CSS заголовки визуально стали больше ожидаемого.
- Риск/урок: заголовки и ячейки в shared-стиле нужно настраивать независимо; иначе правка одного слоя затрагивает другой.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/LEARNING_LOG.md`

### 2026-02-09 (UI: root-cause scoped override `[b-*]` в MyComponent)
- Что изменили: в `MyComponent.razor.css` ограничили широкие `::deep .rz-grid-table*` и `::deep .rz-data-grid*` селекторы контейнером `.tab-content-wrapper`; в `grid-unified` добавили усиленный селектор `.rz-grid-table td .rz-cell-data` с `24px`.
- Почему: scoped-правило вида `[b-*] .rz-grid-table td .rz-cell-data` перехватывало шрифт на вкладках и отменяло unified-увеличение.
- Риск/урок: для shared UI нельзя оставлять глобальные deep-правила в корневом компоненте; локальные legacy-правки должны быть строго контейнеризованы.
- Ссылки: `Final_Test_Hybrid/MyComponent.razor.css`, `Final_Test_Hybrid/wwwroot/css/app.css`

### 2026-02-09 (UI: +5px к шрифту ячеек unified-гридов)
- Что изменили: в `grid-unified` увеличили размер шрифта содержимого ячеек с `19px` до `24px` (`.rz-cell-data`, `.cell-text`).
- Почему: требовалось визуально укрупнить текст в таблицах, приведённых к единому стилю.
- Риск/урок: изменение внесено только в unified-контур; `main-grid-legacy` (`TestSequenseGrid`) оставлен без изменений как отдельное исключение.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor.css`

### 2026-02-09 (UI таблиц: единый стиль и границы применения)
- Что изменили: ввели единый стиль DataGrid через `grid-unified-host`/`grid-unified`; убрали конфликтующие parent/deep-override в табах, где они ломали единый вид.
- Почему: при локальных `::deep .rz-data-grid*` единый стиль становился недетерминированным и расходился между вкладками.
- Риск/урок: shared-стиль работает только при явном opt-in и отсутствии широких родительских переопределений.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/Components/Results/TestResultsTab.razor.css`, `Final_Test_Hybrid/Components/Errors/ErrorsTab.razor.css`

### 2026-02-09 (Главный экран: legacy-вид TestSequenseGrid)
- Что изменили: для `TestSequenseGrid` сделали точечный opt-out от `grid-unified` через `main-grid-legacy`; вернули компактную типографику и плотность строк главного экрана.
- Почему: главный экран должен сохранить исторический UX, несмотря на общую унификацию остальных гридов.
- Риск/урок: при массовой унификации нужны явно зафиксированные исключения, иначе глобальные `!important` ломают целевой локальный дизайн.
- Ссылки: `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor`, `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor.css`

### 2026-02-09 (LogViewer: внутренние вкладки и перенос времени шагов)
- Что изменили: в `LogViewerTab` добавили вкладки `Лог-файл` и `Время шагов`; `StepTimingsGrid` перенесли из `TestResultsTab` в `Лог`.
- Почему: нужен единый экран для текстового лога и таймингов шагов без изменения runtime-источников данных.
- Риск/урок: при переносе вкладок нельзя менять локальные стили содержимого; правки должны ограничиваться shell/layout контейнера.
- Ссылки: `Final_Test_Hybrid/Components/Logs/LogViewerTab.razor`, `Final_Test_Hybrid/Components/Logs/LogViewerTab.razor.css`, `Final_Test_Hybrid/Components/Results/TestResultsTab.razor`

### 2026-02-09 (Диагностика: ручной контекст и безопасность preset)
- Что изменили: в ручной диагностике закрепили контекст оператора (без фоновой автоматики), оставили только безопасные preset-операции, расширили понятность отображаемых чтений.
- Почему: ручной сценарий должен быть предсказуемым и не пересекаться с runtime-автоматикой.
- Риск/урок: любые «удобные» preset в инженерном UI быстро становятся источником скрытых side effect без жёсткого whitelist.
- Ссылки: `Final_Test_Hybrid/Components/Overview/ConnectionTestPanel.razor`, `Final_Test_Hybrid/Services/Diagnostic/Services/BoilerLockRuntimeService.cs`

### 2026-02-09 (Reconnect и подписки: детерминированный подход)
- Что изменили: закрепили инвариант полного rebuild runtime-подписок после reconnect и точечный opt-in для late-subscriber UI.
- Почему: это убирает рассинхрон экранов после потери соединения без глобальных изменений порядка событий.
- Риск/урок: частичный auto-rebind без границ создает скрытые регрессии и нестабильный UI-state.
- Ссылки: `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.Callbacks.cs`, `Final_Test_Hybrid/Services/OpcUa/PlcInitializationCoordinator.cs`

### 2026-02-09 (Контракт результатов теста)
- Что изменили: синхронизировали сохранение результатов по каноническим именам параметров и корректным метаданным (`isRanged`, границы, единицы), закрыли разрывы в списках.
- Почему: неконсистентный контракт ломал классификацию/выгрузку и давал ложные потери результатов.
- Риск/урок: метаданные результатов нельзя наследовать «по соседству»; каждый параметр проверяется по своему контракту.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Steps/ScanStepBase.cs`, `Final_Test_Hybrid/Services/Storage/MesTestResultStorage.cs`

### 2026-02-09 (Каталог ошибок и reseed БД)
- Что изменили: выровняли каталог ошибок программы и подготовили воспроизводимые SQL/скрипты для синхронизации `traceability_boiler` с fail-fast проверками.
- Почему: источник истины по кодам должен быть в программе, а перенос в БД должен быть детерминированным.
- Риск/урок: перед reseed обязательно проверять реальную схему, sequence и связность шагов, иначе высок риск PK/FK конфликтов.
- Ссылки: `Final_Test_Hybrid/Models/Errors/ErrorDefinitions*.cs`, `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program.sql`

### 2026-02-08 (Диагностический runtime: fail-fast и bounded retry)
- Что изменили: усилили bounded-fairness очереди и fail-fast обработку в критичных ветках диагностики.
- Почему: требовалось уменьшить зависания, starvation и ложные аварийные состояния.
- Риск/урок: устойчивость достигается детерминированными переходами и ограниченными retry, а не ростом количества проверок.
- Ссылки: `Final_Test_Hybrid/Services/Diagnostic/Protocol/CommandQueue/Internal/ModbusWorkerLoop.cs`, `Final_Test_Hybrid/Services/Diagnostic/Services/EcuErrorSyncService.cs`

### 2026-02-09 (Процесс и quality-gates)
- Что изменили: зафиксировали рабочий стандарт в `AGENTS.md`; обязательный финальный чек-лист: `build`, `format analyzers`, `format style`.
- Почему: стабильный процесс нужен для одинакового качества решений между задачами и ветками.
- Риск/урок: если чек-лист не формализован, регрессии в стиле/анализаторах начинают накапливаться незаметно.
- Ссылки: `AGENTS.md`, `Final_Test_Hybrid/LEARNING_LOG_ARCHIVE.md`

