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
