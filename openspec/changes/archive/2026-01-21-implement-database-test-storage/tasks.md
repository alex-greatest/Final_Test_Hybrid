# Tasks: Implement Database Test Storage

## Overview

Задачи для реализации сохранения результатов теста в PostgreSQL.

---

## Tasks

### 1. Создать интерфейсы для отдельных storage сервисов

**Файл:** `Services/Storage/Interfaces/`

- [x] `IOperationStorageService.cs` — обновление Operation
- [x] `IResultStorageService.cs` — сохранение Result
- [x] `IErrorStorageService.cs` — сохранение Error
- [x] `IStepTimeStorageService.cs` — сохранение StepTime

**Валидация:** Компиляция проходит ✓

---

### 2. Реализовать OperationStorageService

**Файл:** `Services/Storage/OperationStorageService.cs`

- [x] Найти Boiler по SerialNumber
- [x] Найти Operation по BoilerId + Status == InWork
- [x] Обновить Status (Ok/Nok) и DateEnd
- [x] Вернуть Operation для использования в других сервисах

**Зависимости:** IDbContextFactory, BoilerState, DualLogger

**Валидация:** Unit test или ручная проверка с БД

---

### 3. Реализовать ResultStorageService

**Файл:** `Services/Storage/ResultStorageService.cs`

- [x] Получить результаты из ITestResultsService.GetResults()
- [x] Для каждого TestResultItem:
  - [x] Определить AuditType (IsRanged → NumericWithRange, иначе SimpleStatus)
  - [x] Найти ResultSettingHistory по AddressValue + IsActive + AuditType
  - [x] Если не найдено → log warning, skip
  - [x] Создать Result entity
- [x] Вернуть список Result для batch insert

**Зависимости:** IDbContextFactory, ITestResultsService, DualLogger

**Валидация:** Проверить логирование при отсутствии настройки

---

### 4. Реализовать ErrorStorageService

**Файл:** `Services/Storage/ErrorStorageService.cs`

- [x] Получить историю из IErrorService.GetHistory()
- [x] Для каждого ErrorHistoryItem:
  - [x] Найти ErrorSettingsHistory по AddressError == Code + IsActive
  - [x] Если не найдено → log warning, skip
  - [x] Создать Error entity
- [x] Вернуть список Error для batch insert

**Зависимости:** IDbContextFactory, IErrorService, DualLogger

**Валидация:** Проверить логирование при отсутствии настройки

---

### 5. Реализовать StepTimeStorageService

**Файл:** `Services/Storage/StepTimeStorageService.cs`

- [x] Получить времена из IStepTimingService.GetAll()
- [x] Для каждого StepTimingRecord:
  - [x] Найти StepFinalTestHistory по Name + IsActive
  - [x] Если не найдено → log warning, skip
  - [x] Создать StepTime entity
- [x] Вернуть список StepTime для batch insert

**Зависимости:** IDbContextFactory, IStepTimingService, DualLogger

**Валидация:** Проверить логирование при отсутствии настройки

---

### 6. Реализовать DatabaseTestResultStorage

**Файл:** `Services/Storage/DatabaseTestResultStorage.cs`

- [x] Реализовать ITestResultStorage.SaveAsync()
- [x] Координировать вызовы всех sub-сервисов
- [x] Использовать единую транзакцию (один DbContext)
- [x] Обрабатывать критические ошибки → SaveResult.Fail()
- [x] Логировать успешное сохранение

**Зависимости:** Все storage сервисы, DualLogger

**Валидация:** Интеграционный тест с реальной БД

---

### 7. Обновить DI регистрацию

**Файл:** `Services/DependencyInjection/StepsServiceExtensions.cs`

- [x] Заменить `TestResultStorageStub` на `DatabaseTestResultStorage`
- [x] Зарегистрировать все sub-сервисы как Singleton

**Валидация:** Приложение запускается, DI работает ✓

---

### 8. Функциональное тестирование

- [ ] Запустить тест с OK результатом
- [ ] Проверить записи в БД: Operation, Result, StepTime
- [ ] Запустить тест с NOK результатом (с ошибками)
- [ ] Проверить записи Error в БД
- [ ] Проверить поведение при отсутствии настроек (warning в логах, продолжение работы)
- [ ] Проверить retry при ошибке БД (диалог показывается)

---

## Dependencies

```
Task 2, 3, 4, 5 → могут выполняться параллельно
Task 6 → зависит от 2, 3, 4, 5
Task 7 → зависит от 6
Task 8 → зависит от 7
```

## Files to Create

| Файл | Описание |
|------|----------|
| `Services/Storage/Interfaces/IOperationStorageService.cs` | Интерфейс ✓ |
| `Services/Storage/Interfaces/IResultStorageService.cs` | Интерфейс ✓ |
| `Services/Storage/Interfaces/IErrorStorageService.cs` | Интерфейс ✓ |
| `Services/Storage/Interfaces/IStepTimeStorageService.cs` | Интерфейс ✓ |
| `Services/Storage/OperationStorageService.cs` | Реализация ✓ |
| `Services/Storage/ResultStorageService.cs` | Реализация ✓ |
| `Services/Storage/ErrorStorageService.cs` | Реализация ✓ |
| `Services/Storage/StepTimeStorageService.cs` | Реализация ✓ |
| `Services/Storage/DatabaseTestResultStorage.cs` | Координатор ✓ |

## Files to Modify

| Файл | Изменение |
|------|-----------|
| `Services/DependencyInjection/StepsServiceExtensions.cs` | Заменить Stub на реализацию ✓ |
