# ErrorCoordinator Specification

## Overview
Центральный координатор прерываний для системы тестирования. Обрабатывает потерю связи с PLC, отключение автоматического режима, таймауты и ожидание решения оператора.
## Requirements
### Requirement: WaitForResolution API
Система SHALL предоставлять единый метод ожидания решения оператора с опциональными параметрами через record.

#### Scenario: Ожидание без параметров (базовый случай)
- **WHEN** вызывается `WaitForResolutionAsync()` без параметров
- **THEN** система ожидает сигналы Retry и Skip (через BaseTags)
- **AND** возвращает `ErrorResolution` по первому полученному сигналу

#### Scenario: Ожидание с блоком и таймаутом
- **WHEN** вызывается `WaitForResolutionAsync(new WaitForResolutionOptions(BlockEndTag: "...", BlockErrorTag: "...", Timeout: TimeSpan.FromSeconds(30)))`
- **THEN** система ожидает сигналы блока для Skip
- **AND** применяет указанный таймаут
- **AND** возвращает `ErrorResolution.Timeout` при истечении времени

#### Scenario: Ожидание без возможности Skip
- **WHEN** вызывается `WaitForResolutionAsync(new WaitForResolutionOptions(EnableSkip: false))`
- **THEN** система ожидает только сигнал Retry
- **AND** игнорирует сигналы Skip

### Requirement: Interrupt Handling Synchronization
Система SHALL обрабатывать прерывания эксклюзивно с минимальной синхронизацией.

#### Scenario: Одно прерывание в момент времени
- **WHEN** прерывание уже обрабатывается
- **AND** приходит новое прерывание
- **THEN** новое прерывание логируется как проигнорированное
- **AND** не блокирует поток

#### Scenario: Graceful disposal
- **WHEN** вызывается `DisposeAsync()`
- **THEN** текущая обработка прерывания отменяется через CancellationToken
- **AND** disposal завершается без ожидания активных операций

### Requirement: Reset and Recovery
Система SHALL предоставлять методы Reset и ForceStop для управления состоянием.

#### Scenario: Полный сброс (Reset)
- **WHEN** вызывается `Reset()`
- **THEN** снимается пауза через `PauseTokenSource.Resume()`
- **AND** очищается `CurrentInterrupt`
- **AND** вызывается событие `OnReset`

#### Scenario: Мягкий сброс (ForceStop)
- **WHEN** вызывается `ForceStop()`
- **THEN** снимается пауза через `PauseTokenSource.Resume()`
- **AND** очищается `CurrentInterrupt`
- **AND** событие `OnReset` НЕ вызывается

### Requirement: Strategy Pattern for Interrupts
Система SHALL использовать Strategy Pattern для обработки различных типов прерываний.

#### Scenario: Регистрация behavior
- **WHEN** регистрируется `IInterruptBehavior` для `InterruptReason`
- **THEN** behavior доступен через `InterruptBehaviorRegistry`

#### Scenario: Выполнение behavior
- **WHEN** происходит прерывание с зарегистрированным `InterruptReason`
- **THEN** вызывается соответствующий `IInterruptBehavior.ExecuteAsync()`
- **AND** передаётся `IInterruptContext` для доступа к операциям координатора

### Requirement: File Organization
Код ErrorCoordinator SHALL быть организован в partial classes для соблюдения лимита 300 строк на файл.

#### Scenario: Partial class structure
- **WHEN** класс ErrorCoordinator превышает 300 строк
- **THEN** он разбивается на partial classes по логическим блокам
- **AND** каждый файл содержит не более 300 строк
- **AND** публичный API остаётся без изменений

#### Scenario: Logical grouping
- **WHEN** код разделяется на partial classes
- **THEN** группировка происходит по функциональным областям:
  - Основной файл: поля, конструктор, events, subscriptions, disposal
  - Interrupts: обработка прерываний и синхронизация
  - Resolution: ожидание решения оператора и recovery

