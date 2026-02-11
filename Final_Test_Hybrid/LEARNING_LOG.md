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

### 2026-02-11 (StepHistoryGrid: шапка и toolbar разделены для 1:1 отступа с Results/Errors/Timers)
- Что изменили: в `StepHistoryGrid` убрали объединённый `grid-header`; `last-test-header` вернули в отдельный потоковый блок по паттерну `TestResultsGrid`/`ErrorHistoryGrid`/`ActiveTimersGrid`, а кнопку `Сохранить в Excel` вынесли в отдельную строку `step-history-toolbar` ниже шапки.
- Почему: требовалось получить одинаковый вертикальный отступ шапки во всех вкладках результатов, а совместная строка «шапка + кнопка» давала отличающуюся геометрию.
- Риск/урок: для визуального паритета между вкладками важна не только типографика, но и идентичная DOM-структура (шапка отдельно от action-toolbar).
- Ссылки: `Final_Test_Hybrid/Components/Results/StepHistoryGrid.razor`, `Final_Test_Hybrid/Components/Results/StepHistoryGrid.razor.css`

### 2026-02-11 (StepHistoryGrid: шапка `last-test-header` переведена в flow-layout)
- Что изменили: в `StepHistoryGrid` убрали overlay-центрирование шапки (`position:absolute`) и перевели `grid-header` на сетку `1fr auto 1fr` с отдельным `header-spacer`; `last-test-header` оставили в normal flow и добавили вертикальную геометрию (`line-height`, `padding-block`).
- Почему: в табе истории шагов строки `Дата/время` и `Серийный номер` визуально «слипались» по вертикали и не имели стабильных верхнего/нижнего отступов.
- Риск/урок: absolute-позиционирование шапки в операторских DataGrid-панелях легко ломает вертикальные отступы; для предсказуемой геометрии безопаснее держать шапку в normal flow.
- Ссылки: `Final_Test_Hybrid/Components/Results/StepHistoryGrid.razor`, `Final_Test_Hybrid/Components/Results/StepHistoryGrid.razor.css`

### 2026-02-11 (Results: унификация `NOK=2` + явная трактовка статуса в UI/экспорте)
- Что изменили: в `Coms`-шагах и completion-flow заменили остатки `NOK=0` на `NOK=2` (`Final_result` теперь `1/2`), сохранили исключение для `Testing_date` (`status=1` всегда). В `TestResultsGrid`, `ArchiveResultsGrid`, `ArchiveExcelExportService` ввели явную схему `1=OK`, `2=NOK`, иначе `-`.
- Почему: требовался единый контракт статуса результатов без смешения `0/2` и без неявной интерпретации «всё, что не 1, это NOK».
- Риск/урок: при отказе от legacy `0` исторические записи со старым кодом могут отображаться как `-`; UI и экспорт должны быть синхронизированы с текущим контрактом `1/2`.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadEcuVersionStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadDhwPotiSetpointStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadChPotiSetpointStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Execution.cs`, `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.Flow.cs`, `Final_Test_Hybrid/Components/Results/TestResultsGrid.razor`, `Final_Test_Hybrid/Components/Archive/ArchiveResultsGrid.razor`, `Final_Test_Hybrid/Services/Archive/ArchiveExcelExportService.cs`

### 2026-02-11 (Completion: `Testing_date` всегда сохраняется с `status=1`)
- Что изменили: в `TestCompletionCoordinator.AddCompletionResults` для `Testing_date` зафиксировали `status=1` независимо от итогового результата теста; `Final_result` оставили без изменений.
- Почему: требовалось, чтобы временные информационные параметры (`Testing_date` и временные метрики) всегда имели статус OK.
- Риск/урок: для информационных полей нельзя наследовать итоговый статус теста, иначе в выгрузке они ошибочно маркируются как NOK.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.Flow.cs`, `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.MainLoop.cs`

### 2026-02-11 (SoftReset: interrupt reason без admin-auth)
- Что изменили: в soft-reset interrupt-flow разделили `useMes` (маршрут сохранения) и `requireAdminAuth` (UI-авторизация), добавили временный флаг `bypassAdminAuthInSoftResetInterrupt=true` в `PreExecutionCoordinator`, чтобы при `UseInterruptReason=true` сразу открывался диалог причины без окна авторизации; обновили сигнатуры `ScanDialogCoordinator`/`BoilerInfo`/`InterruptFlowExecutor`. Дополнительно для MES-сохранения причины прерывания переключили endpoint на `POST /api/operation/interrupt`.
- Почему: требовалось убрать лишний шаг admin-авторизации при soft reset и оставить быстрый обратимый откат.
- Риск/урок: флаговые временные изменения должны быть локализованы в одной точке принятия решения и явно документированы, иначе откат затронет несвязанные сценарии.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Subscriptions.cs`, `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanDialogCoordinator.cs`, `Final_Test_Hybrid/Components/Main/BoilerInfo.razor`, `Final_Test_Hybrid/Services/SpringBoot/Operation/Interrupt/InterruptFlowExecutor.cs`, `Final_Test_Hybrid/Services/SpringBoot/Operation/Interrupt/InterruptedOperationService.cs`, `Final_Test_Hybrid/Docs/PlcResetGuide.md`

### 2026-02-11 (OPC: автозапись `DB_Station.Test.Auto=true` на connect/reconnect)
- Что изменили: добавили тег `BaseTags.TestAuto` и новый `PlcAutoWriterService`, который слушает `OpcUaConnectionState.ConnectionStateChanged` и при каждом `connected=true` делает одноразовую запись `true` в `DB_Station.Test.Auto` с bounded retry (до 3 попыток, 300 мс, только transient-ошибки); сервис подключили в DI и инициализацию `Form1`.
- Почему: требовалось гарантировать установку автомата PLC сразу после подключения и после каждого reconnect.
- Риск/урок: запись `Auto=true` должна быть изолирована от reconnect-пайплайна и не ронять runtime при ошибках записи; безопасный режим — bounded retry + логирование + отсутствие throw наружу.
- Ссылки: `Final_Test_Hybrid/Models/Plc/Tags/BaseTags.cs`, `Final_Test_Hybrid/Services/OpcUa/Auto/PlcAutoWriterService.cs`, `Final_Test_Hybrid/Services/DependencyInjection/OpcUaServiceExtensions.cs`, `Final_Test_Hybrid/Form1.cs`

### 2026-02-11 (Archive: второй шаг увеличения status badge до 1.125rem)
- Что изменили: увеличили `font-size` класса `archive-status-badge` с `1rem` до `1.125rem` в `ArchiveGrid.razor.css` и `ArchiveResultsGrid.razor.css`.
- Почему: после первой правки требовалось сделать текст в badge ещё заметнее в архивных таблицах.
- Риск/урок: постепенное увеличение (step-by-step) безопаснее для плотных колонок `Статус` (100-120px), чем резкий переход на крупный размер.
- Ссылки: `Final_Test_Hybrid/Components/Archive/ArchiveGrid.razor.css`, `Final_Test_Hybrid/Components/Archive/ArchiveResultsGrid.razor.css`

### 2026-02-11 (Archive: увеличен текст status badge в архивных гридах)
- Что изменили: для статусных `RadzenBadge` в `ArchiveGrid` и `ArchiveResultsGrid` добавили opt-in класс `archive-status-badge`; в локальных `.razor.css` для него задали `font-size: 1rem` и `line-height: 1.2`.
- Почему: требовалось сделать текст внутри badge крупнее в архивных таблицах без влияния на остальные экраны.
- Риск/урок: безопаснее масштабировать badge через локальный opt-in класс в css-isolation, чем через глобальный селектор `::deep .rz-badge`.
- Ссылки: `Final_Test_Hybrid/Components/Archive/ArchiveGrid.razor`, `Final_Test_Hybrid/Components/Archive/ArchiveGrid.razor.css`, `Final_Test_Hybrid/Components/Archive/ArchiveResultsGrid.razor`, `Final_Test_Hybrid/Components/Archive/ArchiveResultsGrid.razor.css`

### 2026-02-11 (Completion: `Final_result` в верхнем регистре)
- Что изменили: в `TestCompletionCoordinator.AddCompletionResults` заменили значение `Final_result` с `ok/nok` на `OK/NOK`; `status` для `TestResultsService.Add(...)` оставили прежним (`1` для OK, `0` для NOK).
- Почему: требовалось сохранить итог теста в `Final_result` в верхнем регистре без изменения поведения UI.
- Риск/урок: UI и экспорт опираются на поле `Status`, поэтому безопасно менять формат `Value` точечно только для `Final_result`.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.Flow.cs`

### 2026-02-10 (ActiveTimersGrid: хедер последнего теста как в TestResultsGrid)
- Что изменили: в `ActiveTimersGrid` добавили верхний блок `last-test-header` с `Серийный номер` и `Дата/время` из `BoilerState` в формате `dd.MM.yyyy HH:mm:ss`, без добавления дублирующих колонок в DataGrid.
- Почему: требовалось сделать отображение в табе `Таймеры` «один в один» с `TestResultsGrid` для контекста предыдущего теста.
- Риск/урок: источник данных хедера — `BoilerState`, а не `ITimerService`; синхронизация UI опирается на существующий периодический refresh компонента.
- Ссылки: `Final_Test_Hybrid/Components/Results/ActiveTimersGrid.razor`, `Final_Test_Hybrid/Components/Results/ActiveTimersGrid.razor.css`, `Final_Test_Hybrid/Components/Results/TestResultsGrid.razor`

### 2026-02-10 (StepHistoryGrid: полная заливка пустой колонки «Результаты»)
- Что изменили: в `StepHistoryGrid` для колонки `Результаты` добавили fallback `\u00A0` при пустом `data.Result`, а для `.cell-container` усилили растяжение (`min-width: 100%`, `height: 100%`).
- Почему: при пустом значении в ячейке оставалась незакрашенная область, и цвет статуса визуально обрывался.
- Риск/урок: для стабильной заливки в Radzen DataGrid нужно фиксировать и шаблон пустого значения, и геометрию контейнера.
- Ссылки: `Final_Test_Hybrid/Components/Results/StepHistoryGrid.razor`, `Final_Test_Hybrid/Components/Results/StepHistoryGrid.razor.css`

### 2026-02-10 (ErrorDefinitions: унификация ActivatesResetButton для всех ошибок)
- Что изменили: в `ErrorDefinitions*.cs` добавили `ActivatesResetButton: true` для 122 определений, где флаг отсутствовал; итоговый контракт — `190/190` ошибок имеют `ActivatesResetButton=true`.
- Почему: требовалось, чтобы состояние кнопки `Сброс ошибки` активировалось единообразно для любых активных ошибок.
- Риск/урок: массовое включение reset-флага расширяет область активации UI-кнопки на программные и step-ошибки; при изменении политики нужен явный аудит `ErrorDefinitions*` и повторная проверка операторского сценария.
- Ссылки: `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.GlobalApp.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Coms.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Ch.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Gas.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Other.cs`, `Final_Test_Hybrid/Docs/ErrorSystemGuide.md`

### 2026-02-10 (ArchiveGrid: дефолт диапазона дат = текущие сутки)
- Что изменили: в `ArchiveGrid` заменили стартовый диапазон фильтра с `-7/+1` дней на локальные границы текущих суток: `От = DateTime.Today`, `До = DateTime.Today.AddDays(1).AddTicks(-1)`.
- Почему: требовалось, чтобы при первом показе вкладки `Архив` поля диапазона сразу соответствовали «сегодня с начала до конца дня».
- Риск/урок: при отображении формата `dd.MM.yyyy HH:mm` конец суток визуально виден как `23:59`, но фактически фильтр включает весь день до последнего тика.
- Ссылки: `Final_Test_Hybrid/Components/Archive/ArchiveGrid.razor`

### 2026-02-10 (SettingsAccess: marker-фиксация scan-шага + синхронизация Block boiler adapter)
- Что изменили: в `TestSequenseService` заменили определение scan-шага по `Module`-строкам на внутренний marker `scanStepId`; на marker перевели `IsOnActiveScanStep`, `ClearAllExceptScan`, `ResetScanStepToRunning`, `UpdateScanStep`, `MutateScanStep`, `EnsureScanStepExists`, `ClearAll`, а уведомление `OnDataChanged` в `EnsureScanStepExists` вынесли из `lock`. Дополнительно выровняли `RelatedStepName` в `ErrorDefinitions.Steps.Other` до канонического `Block boiler adapter`.
- Почему: после переименования scan-шага блокировки инженерных настроек перестали работать из-за жёсткой привязки к старым текстовым именам модуля.
- Риск/урок: runtime-критерии фазы сканирования нельзя строить на display-name; нужен стабильный внутренний идентификатор и единый канонический `RelatedStepName`.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/TestSequenseService.cs`, `Final_Test_Hybrid/Services/Main/SettingsAccessState.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Other.cs`, `Final_Test_Hybrid/Docs/SettingsBlockingGuide.md`

### 2026-02-10 (Form1: старт приложения в развернутом окне)
- Что изменили: в `Form1` после `InitializeComponent()` добавили `StartPosition = FormStartPosition.Manual` и `WindowState = FormWindowState.Maximized`, чтобы главное окно сразу открывалось на весь экран в режиме `Maximized`.
- Почему: требовалось запускать операторское приложение сразу в полноразмерном режиме без ручного разворачивания.
- Риск/урок: `Maximized` сохраняет системную рамку и кнопки окна; это безопаснее kiosk-режима (`FormBorderStyle.None`) и не меняет жизненный цикл сервисов.
- Ссылки: `Final_Test_Hybrid/Form1.cs`

### 2026-02-10 (Steps: синхронизация Name/Description с NewFile2 + единый ScanBarcode)
- Что изменили: синхронизировали `Name/Description` в шагах `Services/Steps/Steps/*` по `Final_Test_Hybrid/NewFile2.txt` для согласованного списка; для `ScanBarcodeStep` и `ScanBarcodeMesStep` установили одинаковые значения `Name = ScanBarcode` и `Description = Сканирование штрих-кода котла`; в целевых шагах убрали точку в конце описания.
- Почему: требовалось привести отображаемые названия/описания шагов к единому источнику и зафиксировать единый контракт для MES/Non-MES scan-шага.
- Риск/урок: `Name` участвует в резолве шагов из последовательности, поэтому менять можно только по согласованному источнику и без изменения `Id`; legacy-файлы `*Old.cs` должны оставаться неизменными.
- Ссылки: `Final_Test_Hybrid/NewFile2.txt`, `Final_Test_Hybrid/Services/Steps/Steps/ScanBarcodeStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/ScanBarcodeMesStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/CH/CheckWaterFlowStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/CheckTestByteOffStep.cs`

### 2026-02-10 (BoilerOrder: выравнивание ширины поля счётчика в UseMes=false)
- Что изменили: в `Final_Test_Hybrid/Components/Main/BoilerOrder.razor` для строки `Успеш. тестов` оставили однострочный layout с кнопками справа, но зафиксировали ширину блока поля как одну колонку (`flex-basis/max-width = calc((100% - 13px) / 2)`), чтобы поле совпадало по ширине с полями выше.
- Почему: при `UseMes=false` поле счётчика было заметно шире соседних полей из-за растяжения на всю строку.
- Риск/урок: при привязке к `13px` (gap двухколоночного блока) изменение сетки в `header-block-left` требует синхронной корректировки формулы ширины.
- Ссылки: `Final_Test_Hybrid/Components/Main/BoilerOrder.razor`, `Final_Test_Hybrid/LEARNING_LOG.md`, `Final_Test_Hybrid/LEARNING_LOG_ARCHIVE.md`

### 2026-02-10 (BoilerOrder: пароль для Изменить/Сброс + переименование кнопки)
- Что изменили: в `Final_Test_Hybrid/Components/Main/BoilerOrder.razor` для кнопок `Изменить` и `Сброс` добавили парольный gate через `PasswordDialog`; кнопку `Обнулить` переименовали в `Сброс`; для сброса оставили безопасный поток `пароль -> подтверждение -> ResetCountAsync`.
- Почему: требовалось защитить изменение и сброс счётчика успешных тестов тем же паролем, что и инженерные действия.
- Риск/урок: для потенциально разрушительных действий сначала нужен контроль доступа, затем явное подтверждение действия.
- Ссылки: `Final_Test_Hybrid/Components/Main/BoilerOrder.razor`, `Final_Test_Hybrid/LEARNING_LOG.md`, `Final_Test_Hybrid/LEARNING_LOG_ARCHIVE.md`

### 2026-02-10 (LogViewerTab: кнопки Обновить/Очистить скрыты через комментарий)
- Что изменили: во вкладке `Лог-файл` в `LogViewerTab.razor` закомментировали кнопки `Обновить` и `Очистить` (код оставлен в файле), а в `LogViewerTab.razor.css` закомментировали стиль `.log-toolbar ::deep .rz-button`.
- Почему: требовалось убрать кнопки из UI, но сохранить быстрый возврат без восстановления кода.
- Риск/урок: при отключении через комментарий логика `ClearLog()` остаётся неиспользуемой до обратного включения; это осознанный временный компромисс для оперативного отката.
- Ссылки: `Final_Test_Hybrid/Components/Logs/LogViewerTab.razor`, `Final_Test_Hybrid/Components/Logs/LogViewerTab.razor.css`

### 2026-02-10 (Main/Parameter: убраны единицы измерения из значений)
- Что изменили: в `Components/Main/Parameter/CH.razor`, `DHW.razor`, `Delta.razor`, `Emissions.razor`, `Gas.razor`, `GasP.razor` убрали добавление суффиксов единиц (`°C`, `бар`, `мбар`, `ppm`, `%`, `м³/ч`, `л/мин`) из `FormatValue`; форматтеры оставили с `F3` и `CultureInfo.CurrentCulture`, fallback `N/A`. Для Modbus-полей `tCH котла` и `tDHW котла` также убраны суффиксы.
- Почему: требовалось отображать на главном экране только числовое значение без единиц измерения.
- Риск/урок: после удаления суффиксов единицы теперь определяются только контекстом названия поля; при похожих обозначениях (`P`, `T`, `Q`) важно сохранять однозначные лейблы.
- Ссылки: `Final_Test_Hybrid/Components/Main/Parameter/CH.razor`, `Final_Test_Hybrid/Components/Main/Parameter/DHW.razor`, `Final_Test_Hybrid/Components/Main/Parameter/Delta.razor`, `Final_Test_Hybrid/Components/Main/Parameter/Emissions.razor`, `Final_Test_Hybrid/Components/Main/Parameter/Gas.razor`, `Final_Test_Hybrid/Components/Main/Parameter/GasP.razor`

### 2026-02-10 (Main/Parameter: немедленная выдача cached-value + формат F3)
- Что изменили: в `Components/Main/Parameter/*` добавили параметр `EmitCachedValueImmediately`, пробросили его в `SubscribeAsync(..., emitCachedValueImmediately: ...)`, а в `MyComponent.razor` для `Gas/Delta/DHW/CH/Emissions/GasP` включили `EmitCachedValueImmediately="true"` как в `HandProgramDialog`. Также унифицировали формат отображения чисел до `F3` с `CultureInfo.CurrentCulture`, включая Modbus-поля `tCH котла` и `tDHW котла`.
- Почему: на главном экране значения оставались пустыми до первого изменения PLC; требовалось поведение как в hand-program и единая точность отображения (3 знака после запятой).
- Риск/урок: при смешанном использовании `emitCachedValueImmediately` одинаковые источники OPC выглядят по-разному на разных экранах; формат чисел нужно задавать централизованно по единому контракту UI.
- Ссылки: `Final_Test_Hybrid/MyComponent.razor`, `Final_Test_Hybrid/Components/Main/Parameter/CH.razor`, `Final_Test_Hybrid/Components/Main/Parameter/DHW.razor`, `Final_Test_Hybrid/Components/Main/Parameter/Delta.razor`, `Final_Test_Hybrid/Components/Main/Parameter/Emissions.razor`, `Final_Test_Hybrid/Components/Main/Parameter/Gas.razor`, `Final_Test_Hybrid/Components/Main/Parameter/GasP.razor`

### 2026-02-10 (MyComponent: перестановка вкладок верхнего уровня)
- Что изменили: в `MyComponent.razor` переставили верхние вкладки в порядок `Главный экран -> Лог -> Параметры -> Ошибки -> Результаты -> Обзор -> Архив -> Настройки`; содержимое вкладок и их внутренние компоненты не меняли.
- Почему: требовался новый рабочий порядок навигации по экрану без изменения runtime-логики.
- Риск/урок: при перестановке вкладок нельзя менять индексные привязки и контент блоков; иначе легко получить скрытую регрессию маршрута по UI.
- Ссылки: `Final_Test_Hybrid/MyComponent.razor`

### 2026-02-10 (FloatingErrorBadgeHost: выравнивание размера крестика через ::deep)
- Что изменили: для иконки закрытия в `floating-active-errors-close` заменили прямой селектор на `::deep .floating-active-errors-close-icon` и добавили фикс `min-width/padding` кнопки, чтобы размер крестика совпадал со стилем `stand-database-dialog`.
- Почему: без `::deep` scoped CSS не применялся к `RadzenIcon`, из-за чего крестик визуально расходился с эталонным диалогом.
- Риск/урок: при CSS isolation стили `RadzenIcon` нужно задавать через `::deep`, иначе точные размеры/масштаб не гарантируются.
- Ссылки: `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.css`, `Final_Test_Hybrid/wwwroot/css/app.css`

### 2026-02-10 (FloatingErrorBadgeHost: окно ошибок 70x70, центр и titlebar как StandDatabase)
- Что изменили: окно `floating-active-errors-window` увеличили до `70vw x 70vh` (с новыми min/max), перенесли стартовое положение в центр экрана и выровняли titlebar/заголовок/кнопку `close` под стиль `stand-database-dialog`.
- Почему: требовалось более крупное окно для анализа активных ошибок и единый визуальный стандарт шапки/крестика с инженерными диалогами.
- Риск/урок: центрирование через `transform` требует отдельной обработки в drag-start; иначе при первом перемещении появляется скачок позиции.
- Ссылки: `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.css`, `Final_Test_Hybrid/wwwroot/index.html`, `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs`, `Final_Test_Hybrid/wwwroot/css/app.css`

### 2026-02-10 (FloatingErrorBadge: мигающий attention-режим)
- Что изменили: добавили анимацию мигания `floating-error-badge-blink` для `floating-error-badge` с уменьшением opacity и тени в цикле; для `prefers-reduced-motion` мигание отключается.
- Почему: требовалось усилить визуальное привлечение внимания к активным resettable-ошибкам.
- Риск/урок: attention-анимацию в операторском UI нужно делать заметной, но управляемой по частоте и с fallback для сниженной анимации.
- Ссылки: `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.css`

### 2026-02-10 (FloatingErrorBadge: непрозрачный фон под warning-иконкой)
- Что изменили: у `floating-error-badge` задали непрозрачный тёмный фон, рамку и скругление контейнера вместо прозрачного фона.
- Почему: по обратной связи warning-иконка на прозрачном фоне читалась недостаточно стабильно.
- Риск/урок: для плавающих предупреждающих индикаторов контрастный непрозрачный подложечный слой повышает предсказуемость восприятия на разных экранах.
- Ссылки: `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.css`

### 2026-02-10 (FloatingErrorBadge: фикс размера/цвета warning-иконки через ::deep)
- Что изменили: для `FloatingErrorBadgeHost` увеличили контейнер бейджа и перевели стили `RadzenIcon` на `::deep .floating-error-badge-icon` с принудительным жёлтым цветом и крупным размером.
- Почему: при CSS isolation стиль на `RadzenIcon` не применялся стабильно, из-за чего warning-треугольник оставался маленьким и не жёлтым.
- Риск/урок: для иконок из дочерних компонентов в Blazor scoped CSS нужны `::deep`-селекторы, иначе локальные классы не пробиваются к итоговому DOM.
- Ссылки: `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.css`

### 2026-02-10 (FloatingErrorBadge: переход на готовый warning-значок Radzen)
- Что изменили: убрали кастомную форму `floating-error-badge-triangle` на `clip-path` и оставили готовый `RadzenIcon Icon="warning"` как основной визуал бейджа; под него скорректировали размеры контейнера и позицию счётчика.
- Почему: требовался «готовый жёлтый треугольник с восклицательным знаком» без эффекта «треугольник в треугольнике».
- Риск/урок: если нужен стандартный warning-символ, готовая иконка стабильнее и дешевле в поддержке, чем ручная геометрия псевдоэлементами.
- Ссылки: `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor`, `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.css`

### 2026-02-10 (FloatingErrorBadge: полноценный треугольник в стиле WinCC)
- Что изменили: перевели `FloatingErrorBadgeHost` с круглой формы на полноценный треугольный warning-бейдж (`floating-error-badge-triangle`) с палитрой `жёлтый + чёрный`; счётчик оставили отдельным бейджем в правом верхнем углу.
- Почему: операторский запрос на визуал «как в WinCC» требовал не иконку в круге, а целиком треугольный предупреждающий индикатор.
- Риск/урок: при переходе на `clip-path`-геометрию важно сохранять единый клик/drag-контур контейнера и отдельно контролировать z-index псевдоэлементов/счётчика.
- Ссылки: `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor`, `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.css`

### 2026-02-10 (FloatingErrorBadge: triangle + top-right + click suppression after drag)
- Что изменили: в `FloatingErrorBadgeHost` заменили иконку на треугольник `warning`, перенесли стартовую позицию бейджа в правый верхний угол и добавили подавление `TogglePanel` после фактического drag через JS-флаг `floatingPanel.consumeRecentDrag`.
- Почему: после перетаскивания бейджа на `mouseup` срабатывал лишний click и окно ошибок открывалось/закрывалось не по намерению оператора.
- Риск/урок: для элементов с совмещёнными `drag + click` нужен явный порог движения и одноразовый TTL-флаг «был drag», иначе UX остаётся недетерминированным.
- Ссылки: `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor`, `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs`, `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.css`, `Final_Test_Hybrid/wwwroot/index.html`

### 2026-02-10 (Очистка временной диагностики Alarm4/О-001-00 после фикса)
- Что изменили: удалили временные трассировочные логи и сравнения для `О-001-00` в `PlcErrorMonitorService`, `ErrorService` и `OpcUaSubscription*`; оставили рабочую обработку PLC ошибок и базовые предупреждения только для невалидного payload.
- Почему: проблема с индексом `Alarm4` подтверждена и исправлена, детальная телеметрия больше не нужна и засоряет runtime-логи.
- Риск/урок: временную диагностику в критичных потоках нужно удалять сразу после подтверждения причины, иначе затрудняется эксплуатационный анализ.
- Ссылки: `Final_Test_Hybrid/Services/Errors/PlcErrorMonitorService.cs`, `Final_Test_Hybrid/Services/Errors/ErrorService.cs`, `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.cs`, `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.Callbacks.cs`

### 2026-02-10 (DB_Message.Alarm4: сдвиг индексов О-001-xx на 1..8)
- Что изменили: в `ErrorDefinitions.GlobalPlc.cs` сместили PLC-теги `О-001-00..О-001-07` с `Alarm4[2..9]` на `Alarm4[1..8]`; в `PlcErrorMonitorService` синхронизировали `ControlNotEnabledTag`, `ControlNotEnabledArrayIndex`, диагностическое окно `Alarm4` и маппинг `GetAlarmCodeByIndex`.
- Почему: фактическая адресация сигналов в PLC для `DB_Message.Alarm4` использует ожидаемые аварии в диапазоне `[1..8]`; из-за смещения `О-001-00` слушала не тот индекс и не активировалась.
- Риск/урок: при битовых массивах PLC критично зафиксировать единый контракт индексации (0-based/1-based) между PLC/HMI и приложением, иначе ошибка выглядит как «нет сигнала при true».
- Ссылки: `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.GlobalPlc.cs`, `Final_Test_Hybrid/Services/Errors/PlcErrorMonitorService.cs`

### 2026-02-10 (Alarm4: delta-диагностика индексов О-001-00..07)
- Что изменили: в `PlcErrorMonitorService` для массива `ns=3;s="DB_Message"."Alarm4"` добавили snapshot-диагностику: лог `Alarm4 window init`, дельты только по изменениям `Alarm4[2..9]` с привязкой индекса к коду `О-001-00..О-001-07`, и агрегированный `Alarm4 window` при любом изменении.
- Почему: требовалось понять, почему не срабатывает только `О-001-00`, при том что остальные сигналы системы работают; нужен был фактологический ответ, какой именно индекс массива реально меняется в runtime.
- Риск/урок: для групповых PLC-массивов бизнес-ошибка часто в несоответствии индекса/полярности, а не в подписке; delta-лог по окну индексов локализует причину быстрее, чем точечный лог одного бита.
- Ссылки: `Final_Test_Hybrid/Services/Errors/PlcErrorMonitorService.cs`, `Final_Test_Hybrid/CommonErrorTags.md`

### 2026-02-10 (О-001-00: двойной мониторинг IndexRange vs базовый Alarm4)
- Что изменили: в `PlcErrorMonitorService` добавили дополнительную диагностическую подписку на `ns=3;s="DB_Message"."Alarm4"` (базовый массив) с извлечением `index=2`, хранением последних значений двух каналов (`IndexRange` и `Array`) и сравнительным логом `compare match/mismatch`.
- Почему: после фикса `bool[]` callback работал, но `О-001-00` всё равно не поднималась; нужно было доказуемо отделить проблему маршрута `IndexRange` от факта, что PLC не переключает сам бит.
- Риск/урок: для индексных OPC тегов нельзя опираться на один канал наблюдения при расследовании; сравнение `IndexRange` и полного массива быстро локализует источник расхождения.
- Ссылки: `Final_Test_Hybrid/Services/Errors/PlcErrorMonitorService.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.GlobalPlc.cs`

### 2026-02-10 (PLC error callback: нормализация bool[] после IndexRange)
- Что изменили: в `PlcErrorMonitorService.OnTagChanged` добавили нормализацию `object?` в `bool`: поддержали `bool`, `bool[]`, `IEnumerable<bool>`; для пустых коллекций — skip с warning, для длины > 1 — warning и fail-safe `first` элемент. Добавили целевые логи `normalized`/`normalized with warning`.
- Почему: после перевода `Alarm4[2]` на `IndexRange` OPC начал отдавать `System.Boolean[]`, из-за чего callback трактовался как `не bool` и не вызывал `RaisePlc/ClearPlc`.
- Риск/урок: при OPC range-access тип данных в callback может быть массивом даже для одного индекса; PLC error-flow обязан нормализовать payload до доменного `bool`.
- Ссылки: `Final_Test_Hybrid/Services/Errors/PlcErrorMonitorService.cs`, `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.Callbacks.cs`, `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.cs`

### 2026-02-10 (OPC массивы: подписка индексов через IndexRange)
- Что изменили: в `OpcUaSubscription` для тегов вида `...[i]` добавили разбор на `StartNodeId=<base array node>` + `MonitoredItem.IndexRange=<i>`; callback/кэш продолжают использовать исходный ключ `DisplayName` (`...[i]`), чтобы не ломать `PlcErrorMonitorService`. Для целевого `Alarm4[2]` расширили диагностику route (`startNode/indexRange`) на add/notification.
- Почему: по логу `111.txt` скалярные теги работают, а индексный `DB_Message.Alarm4[2]` не поднимал `True`; требовался spec-compliant путь подписки для массивов OPC UA.
- Риск/урок: для OPC UA массивов индекс должен задаваться через `IndexRange`; индекс в строке NodeId может выглядеть корректно, но не гарантирует ожидаемые data-change уведомления.
- Ссылки: `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.Callbacks.cs`, `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.GlobalPlc.cs`

### 2026-02-10 (OPC AddTag: null-safe обработка статуса monitored item)
- Что изменили: в `OpcUaSubscription.ProcessAddResult` убрали прямой доступ к `item.Status.Error.StatusCode`; добавили null-safe чтение `item.Status?.Error`, вычисление `hasBadStatus` и fallback `StatusCodes.BadUnexpectedError` только для error-ветки.
- Почему: при инициализации PLC-подписок словили `NullReferenceException` в `ProcessAddResult` на диагностическом логе статуса.
- Риск/урок: в OPC SDK статус monitored item может быть временно `null`; даже диагностические логи должны быть null-safe в критическом startup-пути.
- Ссылки: `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.cs`

### 2026-02-10 (PLC error-flow: расширенная трассировка О-001-00 до точки dispatch)
- Что изменили: для `О-001-00` добавили стартовый аудит в `PlcErrorMonitorService` (наличие definition в `PlcErrors` + ключевые поля), диагностику регистрации callback в `SubscribeAsync`, детальные логи результата `ProcessAddResult`, предупреждение для unexpected payload и явный лог `NoCallbacks` в `InvokeCallbacks`; в `ErrorService` добавили фазовые логи `Raise/Clear/Duplicate` для кода `О-001-00`.
- Почему: по логам в рантайме отсутствовали любые упоминания `О-001-00`, нужно было сузить точку разрыва в цепочке `definition -> monitored item -> notification -> callback -> raise`.
- Риск/урок: при диагностике PLC-ошибок нужна телеметрия не только на `OnNotification`, но и в точках регистрации callback/dispatch, иначе легко получить ложный вывод «сигнал не приходит».
- Ссылки: `Final_Test_Hybrid/Services/Errors/PlcErrorMonitorService.cs`, `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.cs`, `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.Callbacks.cs`, `Final_Test_Hybrid/Services/Errors/ErrorService.cs`

### 2026-02-10 (PLC диагностика: трассировка О-001-00 от OPC callback до Raise/Clear)
- Что изменили: добавили прицельные runtime-логи для `ns=3;s="DB_Message"."Alarm4"[2]` в `PlcErrorMonitorService` (полученное значение/тип + действие `RaisePlc`/`ClearPlc`) и в `OpcUaSubscription.OnNotification` (status/value/type + отдельный лог bad-quality).
- Почему: при ручном переключении сигнала для `О-001-00` не наблюдалась реакция в `ErrorService`; требовалась сквозная трассировка цепочки `OPC notification -> callback -> Raise/Clear`.
- Риск/урок: без сквозной телеметрии в PLC error-flow невозможно отличить проблему адресации/quality от проблемы бизнес-обработчика.
- Ссылки: `Final_Test_Hybrid/Services/Errors/PlcErrorMonitorService.cs`, `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.Callbacks.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.GlobalPlc.cs`

### 2026-02-10 (ErrorService: runtime-диагностика reset-кнопки для PLC ошибок)
- Что изменили: в `ErrorService` добавили `DualLogger<ErrorService>` и диагностические логи при `Raise`/`NotifyChanges`; лог фиксирует `ActivatesResetButton` у активной `О-002-01`, итоговый `HasResettableErrors` и snapshot активных ошибок вида `Код:Флаг`.
- Почему: при активной `О-002-01` в `ActiveErrorsGrid` кнопка `Сброс ошибки` не переходила в красный режим, требовалось подтвердить фактические runtime-флаги, а не только definition.
- Риск/урок: в диагностике ошибок недостаточно проверять `ErrorDefinition`; решение о состоянии reset-кнопки принимается по фактическому `ActiveError.ActivatesResetButton` в моменте.
- Ссылки: `Final_Test_Hybrid/Services/Errors/ErrorService.cs`, `Final_Test_Hybrid/Components/Errors/ErrorResetButton.razor`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.GlobalPlc.cs`

### 2026-02-10 (UI: плавающий индикатор resettable-ошибок с немодальным окном)
- Что изменили: добавили opt-in флаг `Settings.UseFloatingErrorBadge` в `AppSettings`/`AppSettingsService` и реализовали `FloatingErrorBadgeHost` (плавающая иконка с числом resettable-ошибок + немодальное перетаскиваемое окно `Активные ошибки` на ~50% экрана); подключили host в `MyComponent`.
- Почему: требовался быстрый визуальный индикатор ошибок, синхронизированный с активностью кнопки `Сброс ошибки`, без блокировки основного экрана.
- Риск/урок: при сочетании click+drag на одной иконке важно держать поведение простым и предсказуемым; фильтрацию по `ActivatesResetButton` нужно считать единым источником для видимости и счётчика.
- Ссылки: `Final_Test_Hybrid/Settings/App/AppSettings.cs`, `Final_Test_Hybrid/Services/Common/Settings/AppSettingsService.cs`, `Final_Test_Hybrid/appsettings.json`, `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor`, `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs`, `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.css`, `Final_Test_Hybrid/MyComponent.razor`

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

