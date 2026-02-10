# LEARNING LOG ARCHIVE

Полный архив развернутых записей из LEARNING_LOG.md.

Правило: в активном LEARNING_LOG.md храним только короткие и актуальные записи, подробности переносим сюда без потери контекста.

---

# LEARNING LOG

### 2026-02-08 (Диагностический runtime: fail-fast и bounded retry)
- Что изменили: усилили bounded-fairness очереди и fail-fast обработку в критичных ветках диагностики.
- Почему: требовалось уменьшить зависания, starvation и ложные аварийные состояния.
- Риск/урок: устойчивость достигается детерминированными переходами и ограниченными retry, а не ростом количества проверок.
- Ссылки: `Final_Test_Hybrid/Services/Diagnostic/Protocol/CommandQueue/Internal/ModbusWorkerLoop.cs`, `Final_Test_Hybrid/Services/Diagnostic/Services/EcuErrorSyncService.cs`

## 2026-02-07

### Контекст
- Проверили, покрываются ли ошибки из `111.txt` в цепочке диагностики Modbus.
- Разобрали роли `PingCommand`, `EcuErrorSyncService` и `ErrorDefinitions.DiagnosticEcu`.

### Что сделали
- Подтвердили, что `PingCommand` читает данные (`LastErrorId`, `ChTemperature`) и не выполняет полную причинно-следственную валидацию условий аварий.
- Подтвердили, что маппинг ошибок ЭБУ идёт по `LastErrorId` через `GetEcuErrorById(1..26)`.
- Подтвердили отдельную спец-обработку для `E9` по температуре CH (вариант `E9-STB`).

### Решение
- Отказались от внедрения дополнительного диагностического контекста на данном этапе.

### Причина решения
- Источник истины для ошибок в проде: сам котёл/ЭБУ (`LastErrorId`).
- Локальные эвристики без полного автомата ЭБУ могут давать ложные интерпретации.

### Урок
- Не приписывать `PingCommand` проверки, которых в нём нет: это слой чтения, а не слой классификации причин.

### Статус
- Тема отложена до нового инцидента или отдельного требования.

## 2026-02-07 (реализация BoilerLock runtime)

### Контекст
- Реализована новая логика обработки блокировок котла по `Ping` на основе `1005` и ошибок из `111.txt`.
- Требование: управлять поведением через `appsettings`, не останавливать ping и не добавлять новые ошибки в `ErrorService`.

### Что сделали
- Добавлены флаги в `appsettings.json`:
  - `Diagnostic:BoilerLock:Enabled`
  - `Diagnostic:BoilerLock:PauseOnStatus1Enabled`
  - `Diagnostic:BoilerLock:PlcSignalOnStatus2Enabled`
  - дефолты: все `false`.
- Добавлен `BoilerLockRuntimeService`:
  - слушает `IModbusDispatcher.PingDataUpdated`;
  - фильтрует только коды из `111.txt`;
  - ветка `1005 == 1`: pause через `InterruptReason.BoilerLock`, ожидание `WriteVerifyDelayMs`, запись `1153=0`, повторное чтение `1005`;
  - ветка `1005 == 2`: только PLC signal stub (лог + TODO), без паузы.
- Добавлен новый interrupt:
  - `InterruptReason.BoilerLock`;
  - `BoilerLockBehavior` (pause + уведомление, без `AssociatedError`).
- Добавлено сообщение в `MessageService` и `MessageServiceDescription.md`:
  - приоритет `125`, ниже reset/auto-ready.

### Критические решения
- Для новой логики зафиксировано `1153 = 0` (по согласованному требованию), несмотря на старый путь в `BoilerSettingsService`, где используется `1`.
- Для `status=2` пауза не применяется.

### Защита от зависания
- Добавлен recovery-check на каждом ping:
  - если активен `BoilerLock`, но условие паузы (`status=1 + ошибка из whitelist + флаги + активный test execution`) больше не выполняется, вызывается `ForceStop()`.
- Это закрывает риск вечной паузы при уходе блокировки между циклами ping.

### Урок
- Для pause-механики важно разделять:
  - условие постановки на паузу;
  - условие снятия паузы.
- Если эти условия несимметричны и нет recovery-check, система почти гарантированно рано или поздно зависнет в paused-состоянии.

## 2026-02-07 (документация BoilerLock)

### Что сделали
- Создали отдельный источник истины: `Docs/BoilerLockGuide.md`.
- Добавили краткие ссылки/сводки в:
  - `Docs/DiagnosticGuide.md`
  - `Docs/ErrorCoordinatorGuide.md`
  - `MessageServiceDescription.md`

### Почему так
- Полный дубль в нескольких файлах быстро расходится с кодом.
- Один подробный guide + короткие ссылки в соседних документах снижает риск противоречий.

### Урок
- Для новой runtime-логики сначала фиксировать «где главный документ», иначе через 1-2 итерации появляется неуправляемый рассинхрон описаний.

## 2026-02-07 (спиннер PLC-подписок только при реальной подписке)

### Контекст
- На старте без PLC показывался спиннер `Выполняется подписка...`, хотя реальная подписка ещё не выполнялась.
- Требование проекта: показывать спиннер только после готовности соединения и только на фактической фазе подписок.

### Что сделали
- Изменили `PlcSubscriptionState`:
  - дефолтное состояние теперь не включает `IsInitializing`;
  - `IsInitializing` больше не вычисляется как `!IsCompleted`;
  - `SetInitializing/SetCompleted` теперь явно управляют только фазой показа спиннера.
- Перенесли управление спиннером в `PlcInitializationCoordinator`:
  - `SetInitializing()` включается перед реальной фазой runtime-подписок;
  - `SetCompleted()` выполняется в `finally`;
  - ожидание подключения (`WaitForConnectionAsync`) и pre-execution валидации идут без спиннера.
- Синхронизировали описание в `ARCHITECTURE.md`.

### Причина решения
- Предыдущая семантика state-модели смешивала «инициализация не завершена» и «идёт реальная подписка».
- Из-за этого UI давал ложный сигнал оператору в фазе reconnect/retry при недоступном PLC.

### Урок
- Для UI-индикаторов нельзя использовать инверсный флаг «ещё не завершено» как прокси «операция выполняется сейчас».
- В критичных сценариях SCADA индикатор должен быть привязан к конкретной фазе исполнения, а не к косвенному состоянию.


## 2026-02-08 (перенос кратких записей из активного LEARNING_LOG)

Источник: свёртка активного `LEARNING_LOG.md` для уменьшения объёма оперативного контекста без потери фактов.

### 2026-02-07 (оптимизация LEARNING_LOG)
- Что изменили: ввели ротацию и компактный формат активного лога; развернутую историю вынесли в архив.
- Почему: ограничили рост файла и снизили стоимость чтения контекста.
- Риск/урок: без лимитов и шаблона журнал быстро превращается в несопровождаемый narrative.
- Ссылки: `Final_Test_Hybrid/LEARNING_LOG.md`, `Final_Test_Hybrid/LEARNING_LOG_ARCHIVE.md`

### 2026-02-07 (фикс правил в AGENTS)
- Что изменили: добавили в `AGENTS.md` фиксацию про оптимизацию лога и обязательный контроль размера `LEARNING_LOG.md`.
- Почему: закрепили правило рядом с базовыми рабочими принципами, чтобы не терялось между задачами.
- Риск/урок: если правило не зафиксировано в core-инструкции, команда быстро возвращается к бесконтрольному росту файла.
- Ссылки: `AGENTS.md`, `Final_Test_Hybrid/LEARNING_LOG.md`

### 2026-02-07 (BoilerLock ping-flow: auto-stand + bounded retry)
- Что изменили: в `BoilerLockRuntimeService` добавили ping-flow с обязательной проверкой режима, авто-переходом в `Stand`, retry/cooldown/suppress для `1153=0` и расширенным логом `Doc/Modbus` адресов.
- Почему: убрать ложные/бесконечные попытки сброса в неподходящем режиме и остановить лог-шторм на `IllegalDataAddress`.
- Риск/урок: reset по ping должен быть stateful; без cooldown/suppress сервис в 500ms ping-интервале быстро превращается в генератор повторных ошибок.
- Ссылки: `Final_Test_Hybrid/Services/Diagnostic/Services/BoilerLockRuntimeService.cs`, `Final_Test_Hybrid/Services/Diagnostic/Connection/DiagnosticSettings.cs`, `Final_Test_Hybrid/appsettings.json`

### 2026-02-07 (ECU error flow: lock-only активация по ping)
- Что изменили: `EcuErrorSyncService` больше не трактует `1047` как всегда активную ошибку; взводит ECU-ошибку только при lock-контексте (`1005 in {1,2}` + whitelist `111.txt`) и очищает её вне lock.
- Почему: `1047` — последняя сохранённая ошибка, а не гарантированно активная; прежняя логика давала ложные активные ошибки в UI.
- Риск/урок: слой синхронизации ошибок обязан учитывать физический контекст статуса, иначе `ActiveErrorsGrid` показывает историю как текущую аварийность.
- Ссылки: `Final_Test_Hybrid/Services/Diagnostic/Services/EcuErrorSyncService.cs`, `Final_Test_Hybrid/Services/Diagnostic/Services/BoilerLockCriteria.cs`, `Final_Test_Hybrid/Docs/DiagnosticGuide.md`
## 2026-02-09 (snapshot активного LEARNING_LOG перед полной оптимизацией)
- Источник: Final_Test_Hybrid/LEARNING_LOG.md (секция Активные записи до консолидации).
- Причина переноса: активный лог должен оставаться коротким оперативным индексом, а не хранилищем развернутых расследований.
- Примечание: факты сохранены без удаления; в активном файле оставлена сжатая версия по темам.

### Перенесённые записи

### 2026-02-09 (AGENTS: консолидация правил в стиль и анти-паттерны)
- Что изменили: полностью уплотнили `AGENTS.md` до операционного стандарта: принципы принятия решений, анти-паттерны, инварианты, контракт параметров результатов и quality-gates.
- Почему: длинные частные кейсы ухудшали применимость правил; нужен короткий нормативный документ, который помогает принимать одинаковые решения в похожих ситуациях.
- Риск/урок: если правила формулируются через примеры, команда копирует форму, а не смысл; фиксировать нужно поведение и запреты, а не отдельные истории.
- Ссылки: `AGENTS.md`, `Final_Test_Hybrid/LEARNING_LOG.md`

### 2026-02-08 (HandProgramDialog: lifecycle диагностики привязан к вкладке)
- Что изменили: в `HandProgramDialog` добавили `@bind-SelectedIndex` и условный рендер `ConnectionTestPanel` только при активной вкладке `Тест связи` (индекс `8`), вместо постоянного рендера при открытии модалки.
- Почему: запуск/остановка `ModbusDispatcher` должны происходить при входе/выходе из вкладки теста связи, а не при открытии/закрытии `HandProgramDialog` целиком.
- Риск/урок: при `RadzenTabs` с `RenderMode="Client"` все вкладки рендерятся сразу, поэтому тяжёлые компоненты с собственным lifecycle нужно явно ограничивать условным рендером.
- Ссылки: `Final_Test_Hybrid/Components/Engineer/Modals/HandProgramDialog.razor`, `Final_Test_Hybrid/Components/Overview/ConnectionTestPanel.razor`

### 2026-02-08 (ConnectionTestPanel: безопасные готовые записи)
- Что изменили: в `WritePresets` удалили готовые записи `1060` (выбег насоса), `1013` (уставка ГВС) и `1043` (макс. температура ГВС); оставили `1048` (подсветка дисплея) и `Вручную...`. Стартовый `_writeAddress` перевели на `1048`.
- Почему: в тесте связи готовые записи должны быть максимально безобидными; тепловые уставки и выбег насоса дают лишний операционный риск.
- Риск/урок: даже «удобные» preset’ы в инженерном UI быстро становятся источником скрытых изменений поведения котла, если не ограничивать их безопасным подмножеством.
- Ссылки: `Final_Test_Hybrid/Components/Overview/ConnectionTestPanel.razor`, `Final_Test_Hybrid/Диагностический_протокол_1_8_10.md`

### 2026-02-08 (ConnectionTestPanel: отдельная запись режима + контроль подсветки)
- Что изменили: в `ConnectionTestPanel` добавили отдельный блок `Режим котла` с выпадающим списком (`Стенд/Инженерный/Обычный`) и отдельной кнопкой записи через `AccessLevelManager`; после записи добавили bounded-верификацию по `LastPingData.ModeKey`. В общий блок записи добавили preset `1048` с валидацией диапазона `10..100` и парсинг `UInt32` в форматах `dec` и `0xHEX`.
- Почему: ручная запись ключей режима через общее поле создаёт риск операторской ошибки и путаницу между `1000/1001` и другими регистрами; подсветка дисплея требует жёсткого диапазона по протоколу.
- Риск/урок: подтверждение режима только по ping зависит от интервала `PingIntervalMs`; нужно ограниченное окно ожидания, иначе легко получить ложный fail или бесконечное ожидание.
- Ссылки: `Final_Test_Hybrid/Components/Overview/ConnectionTestPanel.razor`, `Final_Test_Hybrid/Components/Overview/ConnectionTestPanel.razor.css`, `Final_Test_Hybrid/Services/Diagnostic/Access/AccessLevelManager.cs`

### 2026-02-08 (MainSettingsDialog: центрирование и chrome как StandDatabase)
- Что изменили: для `main-settings-dialog` добавили глобальные стили titlebar/close в `app.css` по шаблону `stand-database-dialog`; в `MainSettingsDialog` ввели внутренний stack и локальную нормализацию отступов (`margin-top/margin-bottom`) для `Switch*`/`*AuthorizationQr`; размер окна уменьшили до `760x520`.
- Почему: без глобальных правил у диалога ломались заголовок/крестик, а без локального контейнера строки галочек не центрировались и визуально «плыли».
- Риск/урок: стили Radzen-диалога нужно задавать только глобально (`wwwroot/css/app.css`), а выравнивание дочерних компонентов — локально в `.razor.css` контейнера, иначе появляется конфликт scoped/global правил.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/Components/Engineer/MainEngineering.razor.cs`, `Final_Test_Hybrid/Components/Engineer/Modals/MainSettingsDialog.razor`, `Final_Test_Hybrid/Components/Engineer/Modals/MainSettingsDialog.razor.css`

### 2026-02-08 (Engineer settings: убран пароль с клика галочек)
- Что изменили: удалили шаг `PasswordDialog` из обработчиков клика в `SwitchMes`, `SwitchExcelExport`, `SwitchInterruptReason`, `OperatorAuthorizationQr`, `AdminAuthorizationQr`; переключение теперь выполняется сразу после проверки `IsDisabled`.
- Почему: требование UX — пароль нужен только на входе в `основные настройки`, а не на каждом переключателе.
- Риск/урок: важно отделять security-gate экрана от бизнес-логики компонентов; при этом блокировки и ветвления (`IsDisabled`, logout в MES) должны оставаться неизменными.
- Ссылки: `Final_Test_Hybrid/Components/Engineer/SwitchMes.razor.cs`, `Final_Test_Hybrid/Components/Engineer/SwitchExcelExport.razor.cs`, `Final_Test_Hybrid/Components/Engineer/SwitchInterruptReason.razor.cs`, `Final_Test_Hybrid/Components/Engineer/OperatorAuthorizationQr.razor.cs`, `Final_Test_Hybrid/Components/Engineer/AdminAuthorizationQr.razor.cs`

### 2026-02-08 (Engineer: основные настройки вынесены в модальный диалог)
- Что изменили: в `MainEngineering` добавили первую кнопку `Основные настройки` (под паролем), перенесли `SwitchMes/SwitchExcelExport/SwitchInterruptReason/OperatorAuthorizationQr/AdminAuthorizationQr` в новый `MainSettingsDialog` и убрали их с основной страницы.
- Почему: панель с галочками перегружала экран Engineer; отдельный крупный диалог делает настройки компактными и управляемыми.
- Риск/урок: блокировка кнопки должна совпадать с `SwitchMes` (4 условия), при этом блокировка самих галочек внутри компонентов не должна ослабляться.
- Ссылки: `Final_Test_Hybrid/Components/Engineer/MainEngineering.razor`, `Final_Test_Hybrid/Components/Engineer/MainEngineering.razor.cs`, `Final_Test_Hybrid/Components/Engineer/Modals/MainSettingsDialog.razor`

### 2026-02-08 (IoEditorDialog: автоперезагрузка после reconnect)
- Что изменили: в `IoEditorDialog` объединили загрузку в `ReloadAllDataAsync`; при `ConnectionStateChanged(true)` теперь выполняется полная перезагрузка секций `AI/RTD/PID/AO` с пересозданием snapshot и сбросом inline-edit состояния.
- Почему: `IoEditorDialog` не использует runtime-подписки как схема, поэтому без явного reload после reconnect показывал устаревшие значения.
- Риск/урок: по выбранному правилу «всегда перезагружать» несохранённые локальные правки теряются; это теперь явно логируется warning-ом при reconnect.
- Ссылки: `Final_Test_Hybrid/Components/Engineer/Modals/IoEditorDialog.razor.cs`, `Final_Test_Hybrid/LEARNING_LOG.md`

### 2026-02-08 (AGENTS: inspectcode как checkpoint, не как микрошаг)
- Что изменили: уточнили правило запуска `jb inspectcode` — не после каждой косметической правки, а по завершении логического блока; в примере добавили `--no-build --format=Text --output=$env:TEMP\jb-inspectcode.txt`.
- Почему: дефолтный SARIF даёт тяжёлые отчёты и лишний шум, а повторная сборка внутри inspectcode замедляет цикл без пользы при уже выполненном `dotnet build`.
- Риск/урок: «часто» не равно «качественно»; полезен только сигнал по новому коду и в понятном, компактном формате.
- Ссылки: `AGENTS.md`, `Final_Test_Hybrid/LEARNING_LOG.md`

### 2026-02-08 (AGENTS: `jb inspectcode` по новому коду)
- Что изменили: в корневой `AGENTS.md` добавили обязательное правило запускать `jb inspectcode` после значимых правок только по изменённым `*.cs`, с явным PowerShell-примером формирования `--include`.
- Почему: нужно повысить качество нового кода без постоянной нагрузки/шума от legacy-замечаний по всей solution.
- Риск/урок: без жёсткой привязки к diff локальный цикл разработки быстро деградирует из-за большого фонового долга; полный прогон лучше оставлять для CI/крупных merge.
- Ссылки: `AGENTS.md`, `Final_Test_Hybrid/LEARNING_LOG.md`

### 2026-02-08 (HandProgram: late-subscriber UI получает cache сразу)
- Что изменили: добавили параметр `EmitCachedValueImmediately` в схемы (`Gas/Heating/HotWater`), индикаторы (`LampIndicator`, `ValueIndicator`, `InteractiveLampOutput`) и промежуточные панели; в `HandProgramDialog` включили его (`true`) для всех OPC-подписываемых вкладок.
- Почему: `HandProgramDialog` открывается позже основного экрана, и без immediate emit UI оставался в старом/пустом состоянии до первого нового события.
- Риск/урок: instant emit должен быть только opt-in на точках late-subscribe, иначе можно непреднамеренно поменять порядок событий в существующих экранах.
- Ссылки: `Final_Test_Hybrid/Components/Engineer/Modals/HandProgramDialog.razor`, `Final_Test_Hybrid/Components/Schemes/GasScheme.razor`, `Final_Test_Hybrid/Components/Overview/LampIndicator.razor`

### 2026-02-08 (merge `history -> master`: безопасное разрешение конфликтов)
- Что изменили: выполнили merge `history` в `master`, конфликтные файлы (`OpcUaSubscription`, `TagWaiter`, `CH/DHW` параметры, служебные txt и docs) разрешили в пользу `master` для сохранения текущего стабильного поведения; остальные не конфликтующие изменения из `history` оставили.
- Почему: цель была интегрировать ветку без регресса критичных reset/reconnect/error-path и без изменения рабочих инвариантов runtime-логики.
- Риск/урок: при больших расхождениях веток безопаснее фиксировать стратегию конфликта заранее и подтверждать результат обязательным чек-листом, иначе высок риск скрытых поведенческих регрессий.
- Ссылки: `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.cs`, `Final_Test_Hybrid/Services/OpcUa/TagWaiter.cs`, `Final_Test_Hybrid/LEARNING_LOG.md`

### 2026-02-08 (OpcUaSubscription: opt-in выдача cache при Subscribe)
- Что изменили: в `OpcUaSubscription.SubscribeAsync` добавили параметр `emitCachedValueImmediately` (по умолчанию `false`) и точечный helper, который при `true` сразу отправляет подписчику текущее cached значение через безопасный `SafeFireAndForget`.
- Почему: закрыть кейс late-subscriber UI (диалог/окно открылось после основного экрана), не меняя глобально семантику подписок.
- Риск/урок: безопасный режим по умолчанию обязателен; глобальное мгновенное emit может менять порядок событий в runtime-потоке и давать скрытые регрессии.
- Ссылки: `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.Callbacks.cs`, `Final_Test_Hybrid/LEARNING_LOG.md`

### 2026-02-08 (CH_Start_Max/Min_Heatout: упрощение через статус 1005)
- Что изменили: в `ChStartMaxHeatoutStep` и `ChStartMinHeatoutStep` убрали проверку токов/лимитов (`IProvideLimits`, чтение 1014) и заменили ветку `Ready_2` на чтение регистра `1005` с проверкой значения `6`; при несовпадении/ошибке чтения записывается `Fault=true`, флаг retry сохраняется в `context.Variables`, ожидание идёт по `Error`; на retry сначала выполняется `SetStandModeAsync`, затем шаг запускается с начала.
- Почему: для этих шагов требуется контроль статуса котла, а не ионизационного тока; упрощение убирает лишнюю логику и делает fail/retry поведение предсказуемым.
- Риск/урок: если fault-flow завязан на `Error`, нельзя ждать `End` после `Fault=true`; иначе получаем зависание ожидания.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChStartMaxHeatoutStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChStartMinHeatoutStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChStartMaxHeatoutStepOld.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChStartMinHeatoutStepOld.cs`

### 2026-02-08 (Modbus queue: fairness для ping без повышения приоритета)
- Что изменили: в `ModbusWorkerLoop` добавили правило `HighBurstBeforeLow` — после `8` подряд выполненных `High` делается попытка взять одну `Low` команду.
- Почему: при длинной серии High-команд ping (`Low`) мог голодать и обновляться с большой задержкой.
- Риск/урок: даже когда все бизнес-операции важнее ping, нужна bounded fairness, иначе low-канал может застрять навсегда под постоянной high-нагрузкой.
- Ссылки: `Final_Test_Hybrid/Services/Diagnostic/Protocol/CommandQueue/Internal/ModbusWorkerLoop.cs`, `Final_Test_Hybrid/Services/Diagnostic/Protocol/CommandQueue/ModbusDispatcherOptions.cs`, `Final_Test_Hybrid/appsettings.json`

### 2026-02-08 (SoftCodePlug: корректная трактовка NumberOfContours)
- Что изменили: в `WriteSoftCodePlugStep` и `ReadSoftCodePlugStep` перевели `IsDualCircuit` с условия `== 2` на enum-проверку `ConnectionType.DualCircuit` (`== 1` по 1054); после правки прошли `dotnet build Final_Test_Hybrid.slnx`, `dotnet format analyzers --verify-no-changes` и `dotnet format style --verify-no-changes`.
- Почему: устранён конфликт между рецептом `NumberOfContours` и картой регистра 1054 (`0/1/2/3`), из-за которого двухконтурный (`1`) ошибочно считался одноконтурным.
- Риск/урок: нельзя смешивать «тип подключения» и «количество контуров» как свободные числа; в критичных ветвлениях использовать именованные enum-значения.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Steps/Coms/WriteSoftCodePlugStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.cs`, `Final_Test_Hybrid/Services/Diagnostic/Models/Enums/ConnectionType.cs`

### 2026-02-08 (оптимизация LEARNING_LOG)
- Что изменили: сжали активный журнал через консолидацию однотипных записей за 2026-02-07; детали перенесены в архивный файл.
- Почему: снизили стоимость чтения контекста и риск дублирования фактов в активном логе.
- Риск/урок: активный лог должен оставаться оперативным индексом, а не подробным narrative.
- Ссылки: `Final_Test_Hybrid/LEARNING_LOG.md`, `Final_Test_Hybrid/LEARNING_LOG_ARCHIVE.md`

### 2026-02-08 (Engineer: одинаковая блокировка Hand Program и IO Editor)
- Что изменили: в `MainEngineering` добавили `Disabled="@IsMainSettingsDisabled"` для кнопок `Hand Program` и `IO Editor`; в `OnHandProgram/OnIoEditor` добавили fail-safe early-return при активной блокировке.
- Почему: кнопки инженерных модалок должны блокироваться теми же 4 условиями, что и `Основные настройки`, чтобы исключить запуск диалогов в недопустимых фазах.
- Риск/урок: UI-блокировки недостаточно без runtime guard; обработчики открытия тоже обязаны валидировать текущее состояние.
- Ссылки: `Final_Test_Hybrid/Components/Engineer/MainEngineering.razor`, `Final_Test_Hybrid/Components/Engineer/MainEngineering.razor.cs`

### 2026-02-07 (диагностика: Ping/BoilerLock/ECU)
- Что изменили: зафиксировали `PingCommand` как read-only слой; добавили runtime `BoilerLock` и fail-safe/recovery в ping-flow; для ECU ошибок перевели активацию в lock-контекст (`1005 in {1,2}` + whitelist `111.txt`).
- Почему: нужен предсказуемый error-flow без ложных аварий и без бесконечных retry-петель.
- Риск/урок: в диагностике критично разделять источник факта (данные ЭБУ) и производные правила (когда поднимать/снимать ошибку).
- Ссылки: `Final_Test_Hybrid/Services/Diagnostic/Services/BoilerLockRuntimeService.cs`, `Final_Test_Hybrid/Services/Diagnostic/Services/EcuErrorSyncService.cs`, `Final_Test_Hybrid/Docs/DiagnosticGuide.md`

### 2026-02-07 (документация и процесс)
- Что изменили: выделили единый `BoilerLockGuide`, привязали показ спиннера PLC-подписок к реальной фазе подписки, закрепили ротацию `LEARNING_LOG` и контроль его размера в `AGENTS.md`.
- Почему: уменьшили рассинхрон документации, убрали ложные UX-сигналы и стабилизировали сопровождение контекста.
- Риск/урок: без единого source-of-truth и лимитов журнал/доки быстро деградируют в противоречивый шум.
- Ссылки: `Final_Test_Hybrid/Docs/BoilerLockGuide.md`, `Final_Test_Hybrid/Services/OpcUa/PlcInitializationCoordinator.cs`, `Final_Test_Hybrid/LEARNING_LOG_ARCHIVE.md`, `AGENTS.md`

### 2026-02-09 (BoilerLock: запрет auto-stand в ручном тесте связи)
- Что изменили: добавили singleton-контекст `DiagnosticManualSessionState`; `ConnectionTestPanel` помечает вход/выход из вкладки `Тест связи`; `BoilerLockRuntimeService` теперь отключает ветки `pause/status2/auto-stand/reset`, когда активен ручной тест связи.
- Почему: авто-перевод в `Stand` по ping при ручной диагностике конфликтует с осознанными действиями инженера и может менять режим котла без явного запроса.
- Риск/урок: runtime-автоматика должна уважать операторский контекст; без отдельного флага контекста фоновые recovery-потоки вмешиваются в ручные сценарии.
- Ссылки: `Final_Test_Hybrid/Services/Diagnostic/Services/DiagnosticManualSessionState.cs`, `Final_Test_Hybrid/Components/Overview/ConnectionTestPanel.razor`, `Final_Test_Hybrid/Services/Diagnostic/Services/BoilerLockRuntimeService.cs`, `Final_Test_Hybrid/Services/DependencyInjection/DiagnosticServiceExtensions.cs`

### 2026-02-09 (HandProgram: авто-закрытие при выходе из scan-phase)
- Что изменили: в `HandProgramDialog` добавили подписку на `PreExecution.OnStateChanged`; при `PreExecution.IsAcceptingInput = false` диалог закрывается автоматически через `DialogService.Close()`.
- Почему: иначе можно остаться на вкладке `Тест связи`, отсканировать этикетку и получить старт подготовки/теста при открытом инженерном окне.
- Риск/урок: блокировка только по кнопке открытия недостаточна; нужен runtime-guard для уже открытого окна при смене системной фазы.
- Ссылки: `Final_Test_Hybrid/Components/Engineer/Modals/HandProgramDialog.razor`, `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.cs`

### 2026-02-09 (ConnectionTestPanel: preset версии прошивки + человекочитаемое чтение)
- Что изменили: в `ConnectionTestPanel` добавили готовый preset `Версия прошивки` (чтение `1055/1056`) с выводом `Версия прошивки: vMajor.Minor`; для подготовленных preset-ов чтения добавили человекочитаемую расшифровку (`ModeKey`, `BoilerStatus`, `типы/состояния` по enum-ам).
- Почему: оператору в ручной диагностике нужен быстрый понятный результат без ручной интерпретации сырых чисел.
- Риск/урок: человекочитаемые подписи должны быть привязаны к документированным адресам/enum-ам; ручной режим `Вручную...` оставлять raw, чтобы не скрывать фактические значения.
- Ссылки: `Final_Test_Hybrid/Components/Overview/ConnectionTestPanel.razor`, `Final_Test_Hybrid/Диагностический_протокол_1_8_10.md`

### 2026-02-09 (lost tags: внедрены недостающие параметры результата)
- Что изменили: в `ScanBarcode` (MES/non-MES) добавили сохранение `Plant_ID` (из рецепта), `Shift_No`, `Tester_No` и чтение `Pres_atmosph.`/`Pres_in_gas` из `DB_Measure.Sensor.Gas_Pa/Gas_P` в `TestResultsService`; при ошибке чтения давления scan завершаетcя `Fail`. В completion-flow добавили `Final_result` + `Testing_date` при каждом сохранении результата. В `ReadSoftCodePlugStep` добавили отдельную запись `Soft_Code_Plug` из `BoilerState.Article`.
- Почему: закрыли подтверждённые разрывы между `report_lost_tags.md`/`NewFile2.txt` и фактическим набором параметров, передаваемых в storage/MES.
- Риск/урок: pressure-поля в pre-execution теперь fail-fast и могут чаще останавливать scan при нестабильной OPC-связи; fallback-метаданные (`Plant_ID=""`, `Shift_No="0"`, `Tester_No="Unknown"`) не должны маскировать системные проблемы, поэтому ошибки чтения PLC оставлены критичными.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Steps/ScanStepBase.cs`, `Final_Test_Hybrid/Services/Steps/Steps/ScanBarcodeStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/ScanBarcodeMesStep.cs`, `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.Flow.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Execution.cs`

### 2026-02-09 (Soft_Code_Plug: переведён в non-ranged)
- Что изменили: для `Soft_Code_Plug` в `ReadSoftCodePlugStep` изменили флаг `isRanged` с `true` на `false`.
- Почему: параметр должен сохраняться как обычное значение (без диапазона), чтобы попадать в non-ranged набор результатов.
- Риск/урок: флаг `IsRanged` напрямую влияет на маршрутизацию данных в MES (`Items` vs `ItemsLimited`), поэтому его нельзя выбирать «по аналогии» с соседними полями.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Execution.cs`, `Final_Test_Hybrid/Services/Storage/MesTestResultStorage.cs`

### 2026-02-09 (Soft_Code_Plug: унификация очистки и правило IsRanged)
- Что изменили: `SoftCodePlugResultName` включили в `ResultNamesInternal` в `BuildResultNames(...)`; в `ClearPreviousResults()` оставили единый цикл `foreach` без отдельного `testResultsService.Remove(SoftCodePlugResultName)`.
- Почему: единая точка списка результатов снижает шанс забыть отдельный `Remove` при следующих изменениях.
- Риск/урок: `IsRanged` нужно согласовывать по каждому параметру отдельно (контракт хранения/выгрузки), а не выставлять «как у похожих» полей.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.cs`, `Final_Test_Hybrid/Services/Storage/MesTestResultStorage.cs`

### 2026-02-09 (HandProgram: переключение вкладок на Server render)
- Что изменили: в `HandProgramDialog` переключили `RadzenTabs RenderMode` с `TabRenderMode.Client` на `TabRenderMode.Server` для стабильной отрисовки условного контента вкладки `Тест связи` (`@if (_selectedTabIndex == 8)`).
- Почему: при `Client` в связке с условным рендером по индексу вкладки наблюдалась пустая вкладка `Тест связи`.
- Риск/урок: если содержимое вкладки зависит от серверного состояния (`@if` по `SelectedIndex`), `Client`-режим может давать расхождение между визуальным активным табом и фактическим серверным рендером.
- Ссылки: `Final_Test_Hybrid/Components/Engineer/Modals/HandProgramDialog.razor`

### 2026-02-09 (lost tags: диапазоны 4 параметров)
- Что изменили: закрыли неполные диапазоны для `CH_Flow_Press` (`max=2.700`, `isRanged=true`), `Tank_DHW_Mode` (`max=60.000`, `isRanged=true`), `Supplier_Code` (`0..99999999`) и `Counter_Number` (`0..999999` через `ReadOnlyUInt32Action`).
- Почему: устранили расхождение с `report_lost_tags.md` по ranged-контракту.
- Риск/урок: `IsRanged/min/max` фиксировать для каждого параметра явно; не полагаться на общий путь сохранения без per-parameter лимитов.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Steps/CH/SlowFillCircuitStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/DHW/SetTankModeStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Execution.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Table.Part2.cs`

### 2026-02-09 (результаты: Error_ID не обязателен, канон имени Safety_Time)
- Что изменили: зафиксировали проектное правило для результатов — `Error_ID` не сохраняем в `TestResultsService`; корректное каноническое имя параметра времени безопасности: `Safety_Time`.
- Почему: по итогам сверки `NewFile2.txt` и текущего контура сохранения нужна единая договорённость по обязательным тегам и именованию, чтобы исключить дальнейшие расхождения в аудите.
- Риск/урок: несогласованные имена (`Safety time`/`Safety_time`/`Safety_Time`) ломают сопоставление в отчётах и создают ложные «пропажи» параметров; имя параметра должно быть единым source of truth.
- Ссылки: `Final_Test_Hybrid/NewFile2.txt`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/SafetyTimeStep.cs`, `Final_Test_Hybrid/LEARNING_LOG.md`

### 2026-02-09 (аудит пределов: выровнены `isRanged/min/max`)
- Что изменили: исправили 3 расхождения в сохранении результатов. `CH_Flw_Temp_Cold` оставили `isRanged=false` и убрали ложный `max`. В `ReadSoftCodePlugStep` для строковых параметров (`VerifyStringAction`, `ReadOnlyStringAction`) сменили `isRanged` на `false` при пустых `min/max`.
- Почему: были внутренние противоречия контракта результата (`non-ranged` с заполненным пределом и `ranged` без пределов), из-за чего ломалась корректная классификация параметров в хранилище/выгрузке.
- Риск/урок: `IsRanged` нельзя выставлять «по умолчанию для шага»; его нужно задавать строго по типу конкретного параметра (строка без пределов, число с пределами).
- Ссылки: `Final_Test_Hybrid/Services/Steps/Steps/CH/GetChwFlowNtcColdStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Execution.cs`, `Final_Test_Hybrid/NewFile2.txt`

### 2026-02-09 (DHW: сохранение `DHW_Flow_Hot_Rate` в Check_Flow_Temperature_Rise)
- Что изменили: в `CheckFlowTemperatureRiseStep` добавили чтение `DB_Measure.Sensor.DHW_FS` на завершении шага и сохранение результата `DHW_Flow_Hot_Rate` с пределами из рецептов `DB_Recipe.DHW.Flow_Hot_Rate.Min/Max`; расширили `RequiredPlcTags`/`RequiredRecipeAddresses` и добавили `Remove("DHW_Flow_Hot_Rate")` для Retry.
- Почему: закрыли фактический разрыв между `NewFile2.txt` и контуром сохранения результатов — параметр `DHW_Flow_Hot_Rate` был ожидаем, но не сохранялся.
- Риск/урок: при добавлении параметров в существующий шаг нужно обновлять не только `Add(...)`, но и pre-validation (`RequiredPlcTags`/`RequiredRecipeAddresses`), иначе легко получить runtime-провалы на старте карты.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Steps/DHW/CheckFlowTemperatureRiseStep.cs`, `Final_Test_Hybrid/NewFile2.txt`
---

## 2026-02-09 (snapshot активного LEARNING_LOG перед сжатием UI/index v2)
- Причина переноса: повторное сжатие активного журнала до короткого индекса.
- Источник: Final_Test_Hybrid/LEARNING_LOG.md до сжатия.

# LEARNING LOG

Короткий активный журнал проекта. Подробные расследования и длинные отчёты выносятся в `LEARNING_LOG_ARCHIVE.md`.

## Политика роста (обязательно)
- Активный файл хранит только последние `30` дней или максимум `40` записей (что наступит раньше).
- Одна запись: максимум `6` пунктов + строка `Ссылки`.
- Подробные логи, альтернативы и длинные обсуждения сразу переносить в `LEARNING_LOG_ARCHIVE.md`.
- При превышении лимита переносить самые старые записи в архив без удаления фактов.
- После каждого значимого изменения фиксировать минимум: `Что изменили`, `Почему`, `Риск/урок`, `Ссылки`.

## Шаблон записи
### YYYY-MM-DD (тема)
- Что изменили:
- Почему:
- Риск/урок:
- Ссылки: `path1`, `path2`

## Активные записи

### 2026-02-09 (Точечный rollback стиля главного грида TestSequenseGrid)
- Что изменили: для `TestSequenseGrid` отключили участие в `grid-unified` (класс `main-grid-legacy` вместо `grid-unified`) и вернули прежнюю плотность/типографику строк и заголовков в локальном `TestSequenseGrid.razor.css`.
- Почему: на главном экране требовалось сохранить исторический, более компактный вид основного грида, не меняя unified-стиль остальных вкладок.
- Риск/урок: при массовой унификации нужен явный opt-out для исключений (main grid), иначе глобальные `!important` перебивают целевой локальный UX.
- Ссылки: `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor`, `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor.css`, `Final_Test_Hybrid/wwwroot/css/app.css`

### 2026-02-09 (Доводка типографики DataGrid до эталона RecipesGrid)
- Что изменили: в `grid-unified` зафиксировали единые токены таблицы (`header 20/700`, `cell 19/400`, `line-height 1.4`), а в `StepHistory/TestSequense/StepTimings` убрали локальную `bold`-типографику и жёсткую высоту строк, оставив только правила подсветки.
- Почему: визуальные расхождения сохранялись из-за локальных scoped-override, несмотря на общий класс `grid-unified`.
- Риск/урок: для детерминированного вида таблиц локальные `.cell-text`/`td`-override должны задавать только поведение (подсветка, layout), а не базовую типографику.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor.css`, `Final_Test_Hybrid/Components/Results/StepHistoryGrid.razor.css`, `Final_Test_Hybrid/Components/Results/StepTimingsGrid.razor.css`

### 2026-02-09 (Root-cause: parent Tab CSS перехватывал grid-unified)
- Что изменили: в `TestResultsTab.razor.css` и `ErrorsTab.razor.css` удалили parent-правила `::deep .rz-data-grid*`, которые задавали размеры/геометрию таблиц поверх дочерних гридов; оставили только стили shell/layout вкладок.
- Почему: даже при подключенном `grid-unified` визуальная унификация не срабатывала из-за более специфичных родительских селекторов с `!important`.
- Риск/урок: общий стиль не будет детерминированным, пока родительские контейнеры содержат собственные deep-правила DataGrid.
- Ссылки: `Final_Test_Hybrid/Components/Results/TestResultsTab.razor.css`, `Final_Test_Hybrid/Components/Errors/ErrorsTab.razor.css`, `Final_Test_Hybrid/wwwroot/css/app.css`

### 2026-02-09 (Grid-unified: зачистка локальных CSS-конфликтов)
- Что изменили: во всех основных grid-компонентах удалили локальные дубли правил `rz-column-title/ rz-cell-data/ th/ grid-table`, оставили только уникальные layout-стили; расширили `grid-unified` в `app.css` правилами для `textbox/dropdown/textarea` и кнопок в ячейках.
- Почему: часть экранов продолжала переопределять единый стиль через scoped CSS, из-за чего унификация визуально расходилась между вкладками.
- Риск/урок: при переходе на общий utility-класс локальные `::deep`-правила таблицы должны быть сведены к минимуму, иначе “единый” стиль становится недетерминированным.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/Components/Archive/ArchiveGrid.razor.css`, `Final_Test_Hybrid/Components/Parameters/ParametersTab.razor.css`, `Final_Test_Hybrid/Components/Results/StepTimingsGrid.razor.css`

### 2026-02-09 (Унификация размеров DataGrid по эталону RecipesGrid)
- Что изменили: добавили глобальные utility-классы `.grid-unified-host`/`.grid-unified` в `wwwroot/css/app.css` и подключили их в основных grid-компонентах (`Archive`, `StandDatabase`, `Errors`, `Main`, `Parameters`, `Results`); для кастомных строк подняли `.cell-text` до `19px`, в `ArchiveGrid` выровняли размер содержимого до `19px`.
- Почему: требовалось единообразие размеров заголовков и содержимого гридов (ориентир `RecipesGrid`) и устранение локальных расхождений.
- Риск/урок: глобальный стиль должен включаться только явным class-opt-in; иначе scoped-правила разных компонентов начинают конфликтовать по специфичности.
- Ссылки: `Final_Test_Hybrid/wwwroot/css/app.css`, `Final_Test_Hybrid/Components/Archive/ArchiveGrid.razor`, `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor.css`, `Final_Test_Hybrid/Components/Results/StepHistoryGrid.razor.css`

### 2026-02-09 (StepTimingsGrid: исправление обрезания заголовков колонок)
- Что изменили: в `StepTimingsGrid.razor.css` добавили правила для контейнеров заголовков (`.rz-cell-data`, `.rz-column-title-content`, `.rz-sortable-column`, `.rz-column-title`) с `overflow: visible` и `text-overflow: clip` по паттерну `RecipesGrid`.
- Почему: после переноса `StepTimingsGrid` в `LogViewerTab` заголовки колонок визуально обрезались.
- Риск/урок: для Radzen DataGrid недостаточно править только `th`; нужно отдельно раскрывать внутренние обёртки заголовка.
- Ссылки: `Final_Test_Hybrid/Components/Results/StepTimingsGrid.razor.css`, `Final_Test_Hybrid/Components/Engineer/StandDatabase/Recipe/RecipesGrid.razor.css`

### 2026-02-09 (LogViewer: внутренние вкладки и перенос времени шагов)
- Что изменили: в `LogViewerTab` добавили внутренние вкладки в стиле `MyComponent` (`Лог-файл`, `Время шагов`), перенесли `StepTimingsGrid` из `TestResultsTab` во вкладку `Лог`, контейнер вкладок растянули на всю высоту компонента.
- Почему: требовалась единая точка просмотра лог-файла и времени шагов с сохранением текущих стилей контента и без изменения runtime-источников данных.
- Риск/урок: при переносе UI-вкладок критично сохранять локальные `.razor.css` дочерних компонентов; изменения должны ограничиваться контейнерным layout и shell-вкладок.
- Ссылки: `Final_Test_Hybrid/Components/Logs/LogViewerTab.razor`, `Final_Test_Hybrid/Components/Logs/LogViewerTab.razor.css`, `Final_Test_Hybrid/Components/Results/TestResultsTab.razor`

### 2026-02-09 (LEARNING_LOG: полная консолидация)
- Что изменили: полную развёрнутую активную секцию перенесли в архив как snapshot; активный файл сжали до короткого индекса по решениям и урокам.
- Почему: активный лог должен ускорять навигацию по текущим рискам/инвариантам, а не дублировать длинные расследования.
- Риск/урок: если держать детали в активном логе, контекст деградирует в шум и теряется приоритет фактов.
- Ссылки: `Final_Test_Hybrid/LEARNING_LOG.md`, `Final_Test_Hybrid/LEARNING_LOG_ARCHIVE.md`

### 2026-02-09 (Процесс и quality-gates)
- Что изменили: стандартизировали правила работы через сжатый `AGENTS.md` (стиль решений, анти-паттерны, инварианты, quality-gates).
- Почему: нужен единый операционный стандарт, чтобы решения повторялись консистентно между задачами.
- Риск/урок: “примеры вместо правил” копируют форму, но не удерживают качество; фиксировать нужно принципы и запреты.
- Ссылки: `AGENTS.md`, `Final_Test_Hybrid/LEARNING_LOG.md`

### 2026-02-09 (Ручная диагностика: приоритет операторского контекста)
- Что изменили: для ручного теста связи ввели явный контекст с отключением фоновой авто-автоматики; в панель добавили безопасные preset-сценарии и человекочитаемую расшифровку чтений.
- Почему: диагностика должна подчиняться осознанным действиям инженера, а не фоновым recovery-веткам.
- Риск/урок: без runtime-контекста ручной режим конфликтует с автоматикой и даёт неуправляемые побочные изменения.
- Ссылки: `Final_Test_Hybrid/Components/Overview/ConnectionTestPanel.razor`, `Final_Test_Hybrid/Services/Diagnostic/Services/BoilerLockRuntimeService.cs`, `Final_Test_Hybrid/Services/Diagnostic/Services/DiagnosticManualSessionState.cs`

### 2026-02-09 (Lifecycle инженерных UI-окон)
- Что изменили: выровняли lifecycle инженерных модалок (вкладочный рендер, авто-закрытие при выходе из scan-phase, единая блокировка запуска).
- Почему: UI-гейтинг без runtime-guard не защищает от запуска действий в недопустимой фазе.
- Риск/урок: состояние окна должно зависеть от состояния системы, иначе оператор может случайно пересечь критичные фазы выполнения.
- Ссылки: `Final_Test_Hybrid/Components/Engineer/Modals/HandProgramDialog.razor`, `Final_Test_Hybrid/Components/Engineer/MainEngineering.razor`, `Final_Test_Hybrid/Components/Engineer/MainEngineering.razor.cs`

### 2026-02-09 (Reconnect и подписки: детерминированное поведение)
- Что изменили: закрепили практику полного rebuild runtime-подписок, opt-in выдачу cache для late-subscriber UI и перезагрузку экранов, которые не живут на подписках.
- Почему: это устраняет рассинхрон UI после reconnect без глобальной смены порядка событий.
- Риск/урок: глобальные auto-emit/auto-rebind без границ создают скрытые регрессии в runtime-потоке.
- Ссылки: `Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.Callbacks.cs`, `Final_Test_Hybrid/Components/Engineer/Modals/IoEditorDialog.razor.cs`, `Final_Test_Hybrid/Services/OpcUa/PlcInitializationCoordinator.cs`

### 2026-02-09 (Контракт результатов теста)
- Что изменили: закрыли пропуски по сохранению результатов (`lost tags`), выровняли `isRanged/min/max` по контракту параметров и закрепили единый источник списков результатов.
- Почему: разрывы в контракте результатов ломают классификацию/выгрузку и создают ложные “потери” данных.
- Риск/урок: `isRanged`, границы и имя параметра нельзя выбирать “по аналогии” — только по явному контракту конкретного поля.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Steps/ScanStepBase.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Execution.cs`, `Final_Test_Hybrid/Services/Storage/MesTestResultStorage.cs`

### 2026-02-09 (DHW_Flow_Hot_Rate: закрытие разрыва покрытия)
- Что изменили: в `DHW/Check_Flow_Temperature_Rise` добавили сохранение `DHW_Flow_Hot_Rate` из `DB_Measure.Sensor.DHW_FS` с пределами из рецепта.
- Почему: параметр присутствовал в целевом списке, но отсутствовал в фактическом контуре сохранения.
- Риск/урок: при добавлении нового результата нужно обновлять не только `Add(...)`, но и `RequiredPlcTags/RequiredRecipeAddresses` + `Remove(...)` для Retry.
- Ссылки: `Final_Test_Hybrid/Services/Steps/Steps/DHW/CheckFlowTemperatureRiseStep.cs`, `Final_Test_Hybrid/NewFile2.txt`

### 2026-02-08 (Диагностический runtime: устойчивость и fail-fast)
- Что изменили: в критичных ветках усилили fail-fast и bounded fairness (очередь Modbus), упростили шаги `CH_Start_*` до проверяемого статуса и привязали ECU-ошибки к lock-контексту.
- Почему: это снижает риск зависаний, starvation и ложной аварийности.
- Риск/урок: стабильность достигается не количеством проверок, а детерминированными переходами состояний и ограниченными retry.
- Ссылки: `Final_Test_Hybrid/Services/Diagnostic/Protocol/CommandQueue/Internal/ModbusWorkerLoop.cs`, `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChStartMaxHeatoutStep.cs`, `Final_Test_Hybrid/Services/Diagnostic/Services/EcuErrorSyncService.cs`

### 2026-02-09 (Аудит кодов ошибок vs `traceability_boiler`)
- Что изменили: добавили reproducible read-only скрипт сверки `Final_Test_Hybrid/tools/error-audit/Compare-ErrorCodes.ps1` (сравнение кодов из `ErrorDefinitions*.cs` и `tb_error_settings_template`, отчёт `missing/extra`).
- Почему: разовая ручная сверка неустойчива; нужен повторяемый инструмент с одинаковым результатом на одной БД.
- Ошибка/урок: изначально использовали конструкции не совместимые с Windows PowerShell 5.1 (`??`, ранний `PSScriptRoot` в param default, некорректная интерполяция с `:`). Исправили на совместимый синтаксис.
- Компромисс: для SQL-доступа скрипт поднимает временный `net10.0` раннер с `Npgsql` в `%TEMP%`; это медленнее первого запуска, но не требует установленного `psql`.
- Дополнительно: по запросу пользователя сохранили фактический отчёт сверки в репозитории (`Final_Test_Hybrid/tools/error-audit/reports/error-code-diff-20260209-153105.txt`) для ревью/истории изменений.
- Ссылки: `Final_Test_Hybrid/tools/error-audit/Compare-ErrorCodes.ps1`

### 2026-02-09 (Синхронизация ошибок: БД -> программа)
- Что изменили: добавили в `ErrorDefinitions.Steps1.cs` 19 отсутствующих step-ошибок из `tb_error_settings_template` (коды: `П-015-*`, `П-038-*`, `П-049-*`, `П-051-*`, `П-061-*`, `П-066-00`, `П-070-00`, `П-085-*`) с `PlcTag` и корректными `RelatedStepId/RelatedStepName` по текущим шагам программы.
- Почему: программа должна стать source of truth для последующей нормализации и замены записей в БД.
- Риск/урок: ошибки с несуществующим `RelatedStepId` ломают запуск через `PlcInitializationCoordinator.ValidateErrorStepBindings`; нельзя переносить коды «как есть» из БД без проверки реального `step.Id` в коде.
- Компромисс: 8 конфликтных кодов (`П-028-*`, `П-040-*`, `П-057-*`) не переносили на этом этапе, так как их шаги отсутствуют в текущем runtime-каталоге программы.
- Ссылки: `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps1.cs`, `Final_Test_Hybrid/Services/OpcUa/PlcInitializationCoordinator.cs`

### 2026-02-09 (Global PLC: добавлен блок DB_Elec)
- Что изменили: в `ErrorDefinitions.GlobalPlc.cs` добавили 6 глобальных PLC-ошибок `DB_Elec` с кодами `О-005-00..О-005-05` (`Al_6K1`, `Al_6K2`, `Al_Isometer`, `Al_VoltageMin`, `Al_VoltageMax`, `Al_AdapterNotIn`) и включили их в `GlobalPlcErrors`.
- Почему: закрыли разрыв между фактическими глобальными PLC-сигналами и программным каталогом ошибок перед дальнейшей синхронизацией в БД.
- Риск/урок: при добавлении глобальных ошибок нельзя смешивать их со step-привязками; источник истины — PLC-тег + уникальный код.
- Ссылки: `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.GlobalPlc.cs`

### 2026-02-09 (Рефакторинг step-ошибок по контурам/шагам)
- Что изменили: разнесли объявления из `ErrorDefinitions.Steps.cs` по новым partial-файлам `ErrorDefinitions.Steps.Coms.cs`, `ErrorDefinitions.Steps.Dhw.cs`, `ErrorDefinitions.Steps.Ch.cs`, `ErrorDefinitions.Steps.Gas.cs`, `ErrorDefinitions.Steps.Other.cs`; внутри файлов добавили `#region` по шагам.
- Почему: упростили навигацию и сопровождение каталога ошибок без изменения runtime-поведения.
- Риск/урок: при авто-генерации групп в PowerShell одиночные элементы могут теряться из-за scalar/array-ловушки; после генерации обязателен контроль `StepErrors` list vs declarations 1:1.
- Компромисс: добавили отдельный файл `Other` для `Block Boiler Adapter` и `Elec/*`, чтобы не смешивать их искусственно с `Coms/DHW/CH/Gas`.
- Ссылки: `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Coms.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Ch.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Gas.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Other.cs`

### 2026-02-09 (Перенумерация step-кодов по схеме контур->шаг->индекс)
- Что изменили: перенумеровали все `П-*` в `ErrorDefinitions.Steps*.cs` и `ErrorDefinitions.Steps1.cs` в новую схему: `Coms=П-100..`, `DHW=П-200..`, `CH=П-300..`, `Gas=П-400..`, `Other=П-500..`; внутри каждого шага суффиксы `-00..`.
- Почему: старая нумерация была исторической и слабо читаемой; после пересоздания БД программа должна стать логичным source of truth по кодам.
- Риск/урок: нельзя менять `PlcTag`/`RelatedStepId` вместе с кодами; безопасная миграция — только `Code`, затем обязательная проверка уникальности и компиляции.
- Ссылки: `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Coms.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Ch.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Gas.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Other.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps1.cs`

### 2026-02-09 (SQL-скрипт очистки шаблонов ошибок для prod)
- Что изменили: добавили скрипт `Final_Test_Hybrid/tools/db-maintenance/clear_error_templates_and_deactivate_history.sql` для транзакционного обновления `tb_error_settings_history.is_active=false` и удаления всех записей из `tb_error_settings_template`.
- Почему: нужен воспроизводимый и безопасный артефакт для ручного запуска в production без изменения `tb_error`.
- Риск/урок: удалять `tb_error_settings_history` нельзя, иначе по FK-каскаду потеряются данные в `tb_error`; корректный сценарий — только deactivate history + delete template.
- Ссылки: `Final_Test_Hybrid/tools/db-maintenance/clear_error_templates_and_deactivate_history.sql`

### 2026-02-09 (Завершение рефакторинга step-ошибок: удалён `Steps1`)
- Что изменили: перенесли 38 step-ошибок из `ErrorDefinitions.Steps1.cs` в контурные файлы `ErrorDefinitions.Steps.Coms.cs`, `ErrorDefinitions.Steps.Ch.cs`, `ErrorDefinitions.Steps.Dhw.cs`, `ErrorDefinitions.Steps.Gas.cs`; удалили `ErrorDefinitions.Steps1.cs` и исключили `Steps1Errors` из `ErrorDefinitions.All`.
- Почему: устранили второй источник step-ошибок и закрепили единую структуру по контурам/шагам.
- Риск/урок: при переносе критично сохранять `Code/PlcTag/RelatedStepId/RelatedStepName` 1:1; проверка по счётчикам обязательна (`STEP_TOTAL=140`, `STEP_DUP_CODES=0`).
- Верификация: `dotnet build`, `dotnet format analyzers --verify-no-changes`, `dotnet format style --verify-no-changes`, `jb inspectcode` по изменённым `*.cs` (report без WARNING/ERROR).
- Ссылки: `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Coms.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Ch.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Dhw.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.Gas.cs`, `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs`

### 2026-02-09 (Prod SQL для `traceability_boiler`: полная перезаливка ошибок из программы)
- Что изменили: добавили скрипт `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program.sql` для `traceability_boiler` — деактивация active history по `station_type_id=7`, удаление шаблонов этого station, вставка всех ошибок из `ErrorDefinitions*.cs` (`190` кодов) в `tb_error_settings_template`, затем создание active записей в `tb_error_settings_history`.
- Почему: нужен воспроизводимый прод-артефакт «программа -> БД» без ручного копирования и с контролем целостности.
- Риск/урок: fail-fast обязателен — скрипт прерывается при несопоставленных `RelatedStepName` в `tb_step_final_test` или отсутствии active `tb_step_final_test_history` для шаговых ошибок.
- Компромисс: параметры `station_type_id` и `version` заданы в скрипте как переменные (`7` и `2`) для простого ручного запуска/правки в проде.
- Ссылки: `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program.sql`

### 2026-02-09 (Исправление SQL под реальную схему `traceability_boiler`)
- Что изменили: в `reseed_traceability_boiler_errors_from_program.sql` исправили вставку history под фактические поля БД (`error_settings_id`, `step_final_test_id`, `version`, `station_type_id`) и убрали зависимость от несуществующей таблицы `tb_step_final_test_history`.
- Почему: схема `traceability_boiler` отличается от локальной модели Final_Test, старый вариант скрипта не запускался бы в проде.
- Риск/урок: для шага `Block Boiler Adapter` в коде и `Block boiler adapter` в БД добавлен case-insensitive матчинг (`lower(btrim(...))`), плюс fail-fast на неоднозначные совпадения.
- Верификация: read-only аудит подтвердил отсутствие дублей по нормализованным именам шагов (`DUP_NORM_COUNT=0`) в `tb_step_final_test`.
- Ссылки: `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program.sql`

### 2026-02-09 (Фикс fail-fast проверки неоднозначных шагов в reseed SQL)
- Что изменили: в блоке проверки `v_ambiguous_steps` заменили `COUNT(st.id)` на `COUNT(DISTINCT st.id)` и считаем только по `DISTINCT related_step_name` из `src_errors`.
- Почему: прежняя версия ложнопозитивно считала один и тот же шаг «неоднозначным», если к шагу привязано несколько кодов ошибок.
- Риск/урок: проверки целостности в SQL нужно строить на уровне сущностей (уникальных шагов), а не на уровне строк ошибок, иначе fail-fast блокирует валидный прогон.
- Ссылки: `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program.sql`

### 2026-02-09 (Фикс ID-вставки для `traceability_boiler`)
- Что изменили: в `reseed_traceability_boiler_errors_from_program.sql` перевели вставку в `tb_error_settings_template` и `tb_error_settings_history` на явное задание `id` через `COALESCE(MAX(id),0)+ROW_NUMBER()`.
- Почему: в целевой БД `traceability_boiler` у полей `id` нет `DEFAULT nextval(...)`; вставка без `id` падала с `NOT NULL violation`.
- Риск/урок: нельзя переносить предположение об identity/sequence между контурами БД; перед прод-скриптом обязателен inspect `information_schema.columns.column_default`.
- Ссылки: `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program.sql`

### 2026-02-09 (Синхронизация sequence для Jmix после reseed)
- Что изменили: в `reseed_traceability_boiler_errors_from_program.sql` добавили `setval` для `public.tb_error_settings_template_id_seq` и `public.tb_error_settings_history_id_seq` (через `to_regclass` с безопасной проверкой существования).
- Почему: после ручной вставки `id` Jmix должен продолжать генерацию без конфликтов PK.
- Риск/урок: sequence синхронизируем по глобальному `MAX(id)` таблицы (`setval(..., max+1, false)`), а не по конкретному `station_type_id`, иначе возможны коллизии в другом station.
- Верификация: повторный запуск скрипта успешен (`TEMPLATE_ST7=190`, `ACTIVE_HISTORY_ST7=190`).
- Ссылки: `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program.sql`

### 2026-02-09 (Фикс sequence-нейминга под Jmix в `traceability_boiler`)
- Что изменили: в `reseed_traceability_boiler_errors_from_program.sql` добавили приоритетную синхронизацию реальных Jmix sequence `public.seq_id_tb_errorsettingstemplate` и `public.seq_id_tb_errorsettingshistory` с fallback на legacy-имена `tb_error_settings_*_id_seq`.
- Почему: в прод-контуре Jmix использует `seq_id_tb_*`; из-за несинхронизированной `seq_id_tb_errorsettingshistory` возникал PK-конфликт (`id=519` уже существовал).
- Риск/урок: не полагаться на одно имя sequence между контурами; корректный паттерн — `COALESCE(to_regclass(new), to_regclass(legacy))`.
- Ссылки: `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program.sql`


## 2026-02-10 (перенос из активного LEARNING_LOG)
- Что перенесли: 4 самых старых записей из Final_Test_Hybrid/LEARNING_LOG.md для соблюдения лимита активного индекса.
- Почему: активный лог должен содержать максимум 40 записей.
- Риск/урок: при переносе в архив важен перенос целых секций без потери полей `Что изменили/Почему/Риск/урок/Ссылки`.
- Ссылки: `Final_Test_Hybrid/LEARNING_LOG.md`, `Final_Test_Hybrid/LEARNING_LOG_ARCHIVE.md`

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

### 2026-02-09 (Процесс и quality-gates)
- Что изменили: зафиксировали рабочий стандарт в `AGENTS.md`; обязательный финальный чек-лист: `build`, `format analyzers`, `format style`.
- Почему: стабильный процесс нужен для одинакового качества решений между задачами и ветками.
- Риск/урок: если чек-лист не формализован, регрессии в стиле/анализаторах начинают накапливаться незаметно.
- Ссылки: `AGENTS.md`, `Final_Test_Hybrid/LEARNING_LOG_ARCHIVE.md`


## 2026-02-10 (перенос из активного LEARNING_LOG)
- Что перенесли: 1 самых старых записей из Final_Test_Hybrid/LEARNING_LOG.md для соблюдения лимита активного индекса.
- Почему: активный лог должен содержать максимум 40 записей.
- Риск/урок: при переносе в архив важен перенос целых секций без потери полей `Что изменили/Почему/Риск/урок/Ссылки`.
- Ссылки: `Final_Test_Hybrid/LEARNING_LOG.md`, `Final_Test_Hybrid/LEARNING_LOG_ARCHIVE.md`

### 2026-02-09 (Диагностика: ручной контекст и безопасность preset)
- Что изменили: в ручной диагностике закрепили контекст оператора (без фоновой автоматики), оставили только безопасные preset-операции, расширили понятность отображаемых чтений.
- Почему: ручной сценарий должен быть предсказуемым и не пересекаться с runtime-автоматикой.
- Риск/урок: любые «удобные» preset в инженерном UI быстро становятся источником скрытых side effect без жёсткого whitelist.
- Ссылки: `Final_Test_Hybrid/Components/Overview/ConnectionTestPanel.razor`, `Final_Test_Hybrid/Services/Diagnostic/Services/BoilerLockRuntimeService.cs`

