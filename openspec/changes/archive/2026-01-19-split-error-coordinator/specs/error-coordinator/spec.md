## ADDED Requirements

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
