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
