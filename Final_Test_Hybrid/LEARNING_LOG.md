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
