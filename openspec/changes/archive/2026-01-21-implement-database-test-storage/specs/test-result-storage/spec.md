# Spec: Test Result Storage

## Overview

Сохранение результатов теста в базу данных PostgreSQL при завершении тестирования.

---

## ADDED Requirements

### Requirement: Save Operation Status

При завершении теста система должна обновить статус операции в БД.

#### Scenario: Успешное обновление Operation при OK результате

**Given** тест завершён с результатом OK (testResult = 1)
**And** существует Boiler с SerialNumber из BoilerState
**And** существует Operation со статусом InWork для этого Boiler
**When** вызывается SaveAsync(1, ct)
**Then** Operation.Status обновляется на Ok
**And** Operation.DateEnd устанавливается в текущее время
**And** возвращается SaveResult.Success()

#### Scenario: Успешное обновление Operation при NOK результате

**Given** тест завершён с результатом NOK (testResult = 2)
**And** существует Boiler с SerialNumber из BoilerState
**And** существует Operation со статусом InWork для этого Boiler
**When** вызывается SaveAsync(2, ct)
**Then** Operation.Status обновляется на Nok
**And** Operation.DateEnd устанавливается в текущее время
**And** возвращается SaveResult.Success()

#### Scenario: Operation не найдена

**Given** тест завершён
**And** не существует Operation со статусом InWork для данного Boiler
**When** вызывается SaveAsync(testResult, ct)
**Then** возвращается SaveResult.Fail("Operation not found")
**And** показывается диалог ошибки

---

### Requirement: Save Test Results

При завершении теста система должна сохранить результаты измерений параметров.

#### Scenario: Успешное сохранение Result

**Given** TestResultsService содержит TestResultItem с ParameterName = "P1"
**And** существует ResultSettingHistory с AddressValue = "P1", IsActive = true, AuditType = NumericWithRange
**When** вызывается SaveAsync
**Then** создаётся Result с Value, Min, Max, Status из TestResultItem
**And** Result.OperationId = ID текущей операции
**And** Result.ResultSettingHistoryId = ID найденной настройки

#### Scenario: ResultSettingHistory не найдена

**Given** TestResultsService содержит TestResultItem с ParameterName = "P2"
**And** не существует ResultSettingHistory с AddressValue = "P2" и IsActive = true
**When** вызывается SaveAsync
**Then** логируется Warning "ResultSettingHistory не найден: P2"
**And** Result для P2 не создаётся
**And** обработка других результатов продолжается
**And** SaveAsync возвращает Success (не Fail)

#### Scenario: Определение AuditType по IsRanged

**Given** TestResultItem с IsRanged = true
**When** ищется ResultSettingHistory
**Then** поиск выполняется с AuditType = NumericWithRange

**Given** TestResultItem с IsRanged = false
**When** ищется ResultSettingHistory
**Then** поиск выполняется с AuditType = SimpleStatus

---

### Requirement: Save Errors

При завершении теста система должна сохранить зафиксированные ошибки.

#### Scenario: Успешное сохранение Error

**Given** ErrorService.GetHistory() содержит ErrorHistoryItem с Code = "E001"
**And** существует ErrorSettingsHistory с AddressError = "E001", IsActive = true
**When** вызывается SaveAsync
**Then** создаётся Error с ErrorSettingsHistoryId и OperationId

#### Scenario: ErrorSettingsHistory не найдена

**Given** ErrorService.GetHistory() содержит ErrorHistoryItem с Code = "E002"
**And** не существует ErrorSettingsHistory с AddressError = "E002" и IsActive = true
**When** вызывается SaveAsync
**Then** логируется Warning "ErrorSettingsHistory не найден: E002"
**And** Error для E002 не создаётся
**And** обработка других ошибок продолжается
**And** SaveAsync возвращает Success

---

### Requirement: Save Step Times

При завершении теста система должна сохранить времена выполнения шагов.

#### Scenario: Успешное сохранение StepTime

**Given** StepTimingService.GetAll() содержит StepTimingRecord с Name = "Step1"
**And** существует StepFinalTestHistory с Name = "Step1", IsActive = true
**When** вызывается SaveAsync
**Then** создаётся StepTime с Duration из StepTimingRecord
**And** StepTime.OperationId = ID текущей операции
**And** StepTime.StepFinalTestHistoryId = ID найденной настройки

#### Scenario: StepFinalTestHistory не найдена

**Given** StepTimingService.GetAll() содержит StepTimingRecord с Name = "Step2"
**And** не существует StepFinalTestHistory с Name = "Step2" и IsActive = true
**When** вызывается SaveAsync
**Then** логируется Warning "StepFinalTestHistory не найден: Step2"
**And** StepTime для Step2 не создаётся
**And** обработка других шагов продолжается

---

### Requirement: Handle Critical Errors

Критические ошибки БД должны показывать диалог с возможностью повтора.

#### Scenario: Ошибка подключения к БД

**Given** база данных недоступна
**When** вызывается SaveAsync
**Then** возвращается SaveResult.Fail(errorMessage)
**And** существующий механизм показывает SaveErrorDialog
**And** пользователь может нажать "Повторить"

#### Scenario: Timeout при сохранении

**Given** операция сохранения превысила timeout
**When** происходит TimeoutException
**Then** возвращается SaveResult.Fail("Timeout...")
**And** показывается диалог ошибки

---

## Dependencies

- `BoilerState` — источник SerialNumber
- `ITestResultsService` — источник результатов измерений
- `IErrorService` — источник истории ошибок
- `IStepTimingService` — источник времён шагов
- `IDbContextFactory<AppDbContext>` — фабрика контекста БД

## Related Capabilities

- `error-coordinator` — обработка прерываний (не затрагивается)
- Существующий `SaveErrorDialog` — используется для показа ошибок
