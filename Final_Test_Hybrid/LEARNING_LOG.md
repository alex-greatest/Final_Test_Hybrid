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

