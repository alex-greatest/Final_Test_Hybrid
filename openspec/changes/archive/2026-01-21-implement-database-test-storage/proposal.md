# Proposal: Implement Database Test Storage

## Summary

Реализация сохранения результатов теста в базу данных PostgreSQL при завершении теста.
Заменяет текущую заглушку `TestResultStorageStub` на полноценную реализацию.

## Motivation

Текущая реализация `ITestResultStorage` — заглушка, которая просто возвращает `Success()` без реального сохранения данных. Результаты тестов, ошибки и времена шагов не персистятся в БД.

## Scope

### In Scope
- Реализация `ITestResultStorage` для сохранения в PostgreSQL
- Сохранение `Operation` (обновление статуса и времени завершения)
- Сохранение `Result` (результаты измерений параметров)
- Сохранение `Error` (ошибки, зафиксированные при тестировании)
- Сохранение `StepTime` (времена выполнения шагов)
- Логирование "мягких" ошибок (настройка не найдена) без блокировки процесса

### Out of Scope
- Сохранение в MES (будет отдельный proposal)
- Изменение UI компонентов
- Новые диалоги ошибок (используется существующий `SaveErrorDialog`)

## Design

### Архитектура

```
ITestResultStorage (координатор)
├── IOperationStorageService   → Operation (update status, dateEnd)
├── IResultStorageService      → Result (из TestResultsService)
├── IErrorStorageService       → Error (из ErrorService)
└── IStepTimeStorageService    → StepTime (из StepTimingService)
```

### Зависимости

| Сервис | Зависимость | Назначение |
|--------|-------------|------------|
| DatabaseTestResultStorage | IDbContextFactory<AppDbContext> | Работа с БД |
| DatabaseTestResultStorage | BoilerState | Получение SerialNumber |
| DatabaseTestResultStorage | ITestResultsService | Получение результатов измерений |
| DatabaseTestResultStorage | IErrorService | Получение истории ошибок |
| DatabaseTestResultStorage | IStepTimingService | Получение времён шагов |
| DatabaseTestResultStorage | DualLogger | Логирование |

### Логика сохранения

1. **Найти Operation:**
   - Найти `Boiler` по `BoilerState.SerialNumber`
   - Найти `Operation` по `BoilerId` + `Status == InWork`

2. **Обновить Operation:**
   - `Status` = `Ok` (testResult=1) или `Nok` (testResult=2)
   - `DateEnd` = `DateTime.Now`

3. **Сохранить Results:**
   - Для каждого `TestResultItem` из `TestResultsService.GetResults()`:
     - Определить `AuditType`: `IsRanged` → `NumericWithRange`, иначе `SimpleStatus`
     - Найти `ResultSettingHistory` по `AddressValue == ParameterName` + `IsActive` + `AuditType`
     - Если не найдено → log warning, skip (не блокировать)
     - Создать `Result`

4. **Сохранить Errors:**
   - Для каждого `ErrorHistoryItem` из `ErrorService.GetHistory()`:
     - Найти `ErrorSettingsHistory` по `AddressError == Code` + `IsActive`
     - Если не найдено → log warning, skip
     - Создать `Error`

5. **Сохранить StepTimes:**
   - Для каждого `StepTimingRecord` из `StepTimingService.GetAll()`:
     - Найти `StepFinalTestHistory` по `Name` + `IsActive`
     - Если не найдено → log warning, skip
     - Создать `StepTime`

6. **SaveChangesAsync()**

### Обработка ошибок

| Тип ошибки | Поведение |
|------------|-----------|
| Настройка не найдена (ResultSettingHistory, ErrorSettingsHistory, StepFinalTestHistory) | `log.Warning()`, skip, продолжить |
| Критическая ошибка (DbException, TimeoutException) | `SaveResult.Fail(message)` → показ диалога через существующий механизм |

## Spec Deltas

- `specs/test-result-storage/spec.md` — новая capability

## Risks

| Риск | Митигация |
|------|-----------|
| Производительность при большом количестве результатов | Batch insert, AddRange |
| Блокировка UI при сохранении | Асинхронные операции, существующий retry loop |
| Несогласованность данных при частичном сохранении | Единая транзакция SaveChangesAsync |
