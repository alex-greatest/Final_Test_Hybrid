# ScanMode Specification

## Overview

Управление режимом сканирования штрихкодов. Режим активируется когда оператор авторизован и система в AutoReady. Координирует сессию сканера, main loop и обработку сброса PLC.

## ADDED Requirements

### Requirement: Explicit State Machine

Система SHALL использовать явный state machine для управления фазами режима сканирования.

#### Scenario: Три фазы состояния
- **WHEN** система инициализируется
- **THEN** доступны три фазы: `Idle`, `Active`, `Resetting`
- **AND** начальная фаза — `Idle`

#### Scenario: Переход Idle → Active
- **WHEN** оператор авторизован
- **AND** система в AutoReady
- **AND** текущая фаза `Idle`
- **THEN** система переходит в фазу `Active`
- **AND** сканер активируется

#### Scenario: Переход Active → Idle
- **WHEN** оператор разлогинился ИЛИ AutoReady отключен
- **AND** нет активного теста (`!IsAnyActive`)
- **AND** текущая фаза `Active`
- **THEN** система переходит в фазу `Idle`
- **AND** main loop останавливается

#### Scenario: Переход Active → Resetting
- **WHEN** получен сигнал сброса PLC
- **AND** текущая фаза `Active`
- **THEN** система переходит в фазу `Resetting`
- **AND** сканер деактивируется
- **AND** возвращается `wasInScanPhase = true`

#### Scenario: Переход Resetting → Active
- **WHEN** сброс PLC завершён
- **AND** условия активации выполнены (оператор + AutoReady)
- **THEN** система переходит в фазу `Active`
- **AND** сканер реактивируется
- **AND** timing сбрасывается

#### Scenario: Переход Resetting → Idle
- **WHEN** сброс PLC завершён
- **AND** условия активации НЕ выполнены
- **THEN** система переходит в фазу `Idle`
- **AND** main loop останавливается

### Requirement: Phase Change Notifications

Система SHALL уведомлять подписчиков об изменении фазы.

#### Scenario: Событие OnPhaseChanged
- **WHEN** фаза изменяется
- **THEN** вызывается событие с предыдущей и новой фазой
- **AND** подписчики могут реагировать на переход

### Requirement: Thread-Safe State Transitions

Система SHALL обеспечивать потокобезопасность переходов состояний.

#### Scenario: Конкурентные переходы
- **WHEN** несколько потоков пытаются изменить состояние
- **THEN** только один переход выполняется
- **AND** остальные возвращают `false` без блокировки

### Requirement: Soft Deactivation During Activity

Система SHALL поддерживать мягкую деактивацию при активном тесте.

#### Scenario: AutoMode отключен во время теста
- **WHEN** AutoReady отключается
- **AND** тест выполняется (`IsAnyActive = true`)
- **THEN** сканер деактивируется (soft)
- **AND** timing приостанавливается
- **AND** фаза остаётся `Active`
- **AND** main loop продолжает работу

#### Scenario: Возврат AutoMode во время теста
- **WHEN** AutoReady возвращается
- **AND** фаза `Active`
- **THEN** сканер реактивируется
- **AND** timing возобновляется