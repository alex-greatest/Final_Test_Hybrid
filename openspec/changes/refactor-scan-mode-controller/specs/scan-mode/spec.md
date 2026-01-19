# ScanMode Specification

## Overview

Управление режимом сканирования штрихкодов с явным state machine.

## ADDED Requirements

### Requirement: Explicit Phase Enum

Система SHALL использовать enum для состояний режима сканирования.

#### Scenario: Три фазы
- **WHEN** система работает
- **THEN** доступны только три фазы: `Idle`, `Active`, `Resetting`
- **AND** невозможны другие комбинации состояний

#### Scenario: Переход Idle → Active
- **WHEN** оператор авторизован И AutoReady
- **THEN** фаза становится `Active`

#### Scenario: Переход Active → Resetting
- **WHEN** получен сигнал сброса PLC
- **AND** фаза `Active`
- **THEN** фаза становится `Resetting`
- **AND** возвращается `wasInScanPhase = true`

#### Scenario: Переход Resetting → Active/Idle
- **WHEN** сброс завершён
- **THEN** фаза становится `Active` (если условия выполнены) или `Idle`

### Requirement: CTS Lifecycle

Система SHALL корректно управлять CancellationTokenSource.

#### Scenario: Dispose при замене
- **WHEN** создаётся новый CTS
- **THEN** старый CTS SHALL быть Cancel() и Dispose()

### Requirement: Thread-safe Dispose

Система SHALL обеспечивать потокобезопасный Dispose.

#### Scenario: Concurrent Dispose
- **WHEN** Dispose вызывается из нескольких потоков
- **THEN** только один вызов выполняется
- **AND** нет race condition на _loopCts
