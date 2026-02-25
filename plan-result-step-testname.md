# План: Привязка Result к StepFinalTestHistory + колонка "Название теста"

## Краткое резюме

Добавляется связь `Result -> StepFinalTestHistory` через nullable FK в локальной БД.
Если шаг по имени не найден при сохранении результата, конкретная запись `Result` пропускается, пишется warning, общий `SaveAsync` продолжается.
Старые записи остаются валидными (FK может быть `null`).

Колонка в UI и Excel называется строго: **Название теста**.

## Публичные изменения моделей/контрактов

1. `Result` (модель БД):
- добавить `long? StepFinalTestHistoryId`
- добавить navigation `StepFinalTestHistory? StepFinalTestHistory`

2. `TB_RESULT` (EF + БД):
- новая колонка `STEP_FINAL_TEST_HISTORY_ID` nullable
- FK на `TB_STEP_FINAL_TEST_HISTORY(ID)`
- индекс по `STEP_FINAL_TEST_HISTORY_ID`

3. `ArchiveResultItem` (DTO UI/экспорт):
- добавить `string? TestName`

## Фаза 1. Схема БД и доменная модель

1. Обновить `Final_Test_Hybrid/Models/Database/Result.cs`:
- добавить FK/navigation свойства

2. Обновить `Final_Test_Hybrid/Services/Database/Config/AppDbContext.cs` (ConfigureResult):
- mapping `STEP_FINAL_TEST_HISTORY_ID` (nullable)
- `HasOne(...StepFinalTestHistory...).WithMany().HasForeignKey(...).OnDelete(DeleteBehavior.SetNull)`
- индекс по FK

3. Создать EF migration в `Final_Test_Hybrid/Migrations`:
- `AddColumn` + `CreateIndex` + `AddForeignKey`
- без backfill и без `NOT NULL`

4. Проверка фазы:
- миграция применяется на существующей БД без потери данных
- старые записи `TB_RESULT` читаются как раньше

## Фаза 2. Сохранение Result с привязкой к шагу

1. Обновить `Final_Test_Hybrid/Services/Storage/ResultStorageService.cs`:
- дополнительно загружать словарь активных `StepFinalTestHistory` по `Name`
- при создании `Result` искать `item.Test` в словаре
- если найдено: заполнять `StepFinalTestHistoryId`
- если не найдено: warning-лог с `ParameterName + Test`, `continue` (результат не добавлять)

2. Поведение при ошибках:
- не менять текущий fail/retry flow `DatabaseTestResultStorage`
- пропуск отдельных `Result` не должен ронять `SaveAsync`

3. Защита от дублей имени шага:
- при возможных дублях `Name` среди `IsActive=true` не падать на `ToDictionary`
- логировать warning и выбирать детерминированно одну запись

4. Проверка фазы:
- для параметров с валидным `item.Test` связь сохраняется
- для отсутствующих шагов есть warning в логе, операция сохраняется дальше

## Фаза 3. Чтение и UI архива

1. Обновить `Final_Test_Hybrid/Models/Archive/ArchiveResultItem.cs`:
- добавить `TestName`

2. Обновить `Final_Test_Hybrid/Services/Archive/OperationDetailsService.cs` (`GetResultsAsync`):
- в `Select` добавить `TestName = r.StepFinalTestHistory != null ? r.StepFinalTestHistory.Name : null`
- сортировку оставить существующую (по `ParameterName`)

3. Обновить `Final_Test_Hybrid/Components/Archive/ArchiveResultsGrid.razor`:
- добавить колонку **Название теста** (перед `Параметр`)
- для `null` отображать `-`

4. Проверка фазы:
- в архивном диалоге появляется колонка **Название теста**
- для старых/непривязанных строк отображается `-`

## Фаза 4. Экспорт архива в Excel

1. Обновить `Final_Test_Hybrid/Services/Archive/ArchiveExcelExportService.cs` (`AddResultsSheet`):
- добавить колонку **Название теста** в результаты (перед `Параметр`)
- заполнение через `SafeValue(item.TestName)`

2. Сохранить порядок/формат остальных колонок и защиту formula injection

3. Проверка фазы:
- экспорт из `ArchiveGrid.razor` и `OperationDetailsDialog.razor` содержит колонку **Название теста**

## Тест-кейсы и сценарии

1. Migration:
- существующая БД обновляется успешно
- `TB_RESULT.STEP_FINAL_TEST_HISTORY_ID` nullable
- FK и индекс созданы

2. Save path:
- `item.Test` есть в активных `StepFinalTestHistory` -> `Result` сохраняется с FK
- `item.Test` отсутствует -> `Result` не сохраняется, warning есть, `SaveAsync` успешен при прочих успешных частях

3. Archive read:
- `GetResultsAsync` возвращает `TestName` при наличии FK
- для null FK возвращает null, UI показывает `-`

4. Excel:
- в листах результатов есть колонка **Название теста**
- значения экранируются через `SafeValue`

5. Regression:
- ошибки/step-times/статусы операций не меняются
- текущая логика фильтрации и вкладок архива не ломается

## Предположения и значения по умолчанию

1. Каноническое сопоставление шага: `TestResultItem.Test` ↔ `StepFinalTestHistory.Name`, точное строковое сравнение
2. Используются только активные записи `StepFinalTestHistory` (`IsActive = true`) для резолва
3. FK в `Result` остаётся nullable долгосрочно
4. Отсутствующий шаг в словаре не считается критической ошибкой сохранения операции: только warning + skip конкретного `Result`
5. Изменения выполняются по фазам в порядке: БД -> сохранение -> UI -> Excel

## Quality gates

1. `dotnet build Final_Test_Hybrid.slnx`
2. `dotnet format analyzers --verify-no-changes`
3. `dotnet format style --verify-no-changes`
4. `jb inspectcode Final_Test_Hybrid.slnx "--include=<changed.cs;...>" --no-build --format=Text "--output=<path>" -e=WARNING`
