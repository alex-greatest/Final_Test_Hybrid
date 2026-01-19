# Step Timing Specification

## Overview
StepTimingService отслеживает время выполнения шагов теста: один таймер для scan step и 4 параллельных таймера для column steps.

## MODIFIED Requirements

### Requirement: Scan Step Timing Persistence
Система SHALL сохранять время scan step в историю при его завершении.

#### Scenario: Scan step завершается успешно
- **GIVEN** scan step запущен через `StartScanTiming()`
- **WHEN** вызывается `StopScanTiming()`
- **THEN** время scan step сохраняется в `_records` как первый элемент
- **AND** `_scanState` очищается (IsActive = false)
- **AND** время остаётся видимым в `GetAll()` через `_records`

#### Scenario: Scan step на паузе
- **GIVEN** scan step запущен и поставлен на паузу
- **WHEN** вызывается `StopScanTiming()`
- **THEN** накопленное время (AccumulatedDuration) сохраняется в `_records`
- **AND** `_scanState` очищается

#### Scenario: StopScanTiming на неактивном state
- **GIVEN** scan step не запущен (IsActive = false)
- **WHEN** вызывается `StopScanTiming()`
- **THEN** метод возвращает без действий (no-op)
- **AND** `_records` не изменяется

### Requirement: Timing Records Order
Система SHALL сохранять записи времени в порядке выполнения шагов.

#### Scenario: Scan step первый в списке
- **GIVEN** scan step завершён и сохранён в `_records`
- **WHEN** выполняются column steps
- **THEN** scan step отображается первым в `GetAll()`
- **AND** column steps добавляются после scan step в порядке завершения
