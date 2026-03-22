# ScanModeController Guide

## Назначение

`ScanModeController` управляет режимом сканирования: активация/деактивация на основе состояния оператора и автомата. Координирует управление сессией сканера, синхронизирует Scan-таймер с входной готовностью (AutoReady + PLC-связь) в фазе ожидания barcode и уведомляет подписчиков об изменении состояния.

С версии unified scanner ownership `ScanModeController` владеет только ordinary `PreExecution` owner. Scanner-диалоги переводятся в отдельный `dialog-mode` через `ScannerInputOwnershipService`.

## Условия активации

Режим сканирования активен когда:
- Оператор авторизован (`OperatorState.IsAuthenticated`)
- Автомат готов (`AutoReadySubscription.IsReady`)

```
IsScanModeEnabled = IsAuthenticated && IsReady
```

## Граница ответственности AutoReady для changeover

- `ScanModeController` использует `AutoReadySubscription.IsReady` только для управления scan-режимом (активация/деактивация, сессия сканера, Scan-таймер).
- Старт таймера переналадки выполняет `PreExecutionCoordinator`; `ScanModeController` не владеет этой логикой.
- Для старта changeover используется one-shot источник `AutoReadySubscription.OnFirstAutoReceived` через `ChangeoverStartGate`.
- В `ChangeoverStartGate` есть pending/replay для поздней подписки `PreExecutionCoordinator`, чтобы первый сигнал не терялся (`AutoReadyReplayConsumed`).
- После первого запуска последующие изменения `AutoReady` не должны перезапускать переналадку и не должны влиять на changeover во время теста.
- После `OnResetCompleted` во время активного post-AskEnd окна scanner-ready не возвращается немедленно: `ScanModeController` ждёт завершения terminal ветки.
- Deferred catch-up после post-AskEnd использует **latest** `AutoReadySubscription.IsReady` на момент закрытия окна, а не старый snapshot до reset.

## Состояния

| Флаг | Описание |
|------|----------|
| `_isActivated` | Режим сканирования активирован |
| `_isResetting` | Выполняется сброс PLC |
| `_scanPausedByInputReadiness` | Scan-таймер поставлен на паузу из-за неготовности входа (нет AutoReady или нет PLC-связи) во время ожидания barcode |

### IsInScanningPhase

```csharp
IsInScanningPhase = _isActivated && !_isResetting
```

Используется `PlcResetCoordinator` для определения типа сброса:
- `true` → мягкий сброс (ForceStop)
- `false` → жёсткий сброс (Reset)

### Deferred scanner-ready после post-AskEnd

- `OnResetCompleted` во время PLC soft reset больше не означает немедленный `TransitionToReadyInternal()`.
- Если `PreExecutionCoordinator.IsPostAskEndFlowActive()` ещё true, controller удерживает reset-state до финального PLC outcome.
- `full cleanup` возвращает scan timing/session после завершения post-AskEnd ветки.
- `repeat` не поднимает scan timing/session, потому что следующий цикл уходит в существующий repeat path с `_skipNextScan`.
- Если active `post-AskEnd` оборван non-PLC `HardReset`, `PreExecutionCoordinator` публикует для deferred transition тот же outcome `full cleanup`.
  Это нужно только для завершения зависшего catch-up от старого PLC reset; broad fallback "любой abort = ready" запрещён.

## Жизненный цикл

### Активация

```
IsScanModeEnabled = true
        ↓
TryActivateScanMode()
        ↓
    ┌───────────────────────────────┐
    │ _isResetting? → return        │
    │ _isActivated? → Refresh       │
    │ else → PerformInitialActivation│
    └───────────────────────────────┘
```

**PerformInitialActivation:**
1. `_isActivated = true`
2. `AcquireSession()` — поднятие `PreExecution` owner
3. `AddScanStepToGrid()` — добавление в UI
4. `StartScanTiming()` — запуск таймера
5. `StartMainLoop()` — запуск цикла обработки

### Деактивация

```
IsScanModeEnabled = false
        ↓
TryDeactivateScanMode()
        ↓
    ┌─────────────────────────────────┐
    │ ShouldUseSoftDeactivation()?    │
    │   true  → PerformSoftDeactivation│
    │   false → PerformFullDeactivation│
    └─────────────────────────────────┘
```

**Мягкая деактивация** (только освобождение сканера):
- AutoMode потерян, но оператор авторизован
- Выполняется тест (`IsAnyActive`)
- Ожидание сканирования (`IsAcceptingInput`)
- `PreExecution` owner снимается, но `dialog-mode` этим путём не поднимается и не пересчитывается

**Полная деактивация:**
- Отмена main loop
- `_isActivated = false`
- Освобождение сканера
- Очистка grid через `ClearAllExceptScan(SequenceClearMode.ClearOnly)`, если оператор не авторизован и PLC reset не активен

Эта ветка не создаёт completed test history, не сохраняет reset-history snapshot и не запускает Excel-экспорт. Она только возвращает sequence UI к scan-состоянию при logout/полной деактивации вне reset-цикла PLC.

## Обработка сброса PLC

### OnResetStarting

```csharp
private bool HandleResetStarting()
{
    var wasInScanPhase = IsInScanningPhaseUnsafe;
    _isResetting = true;
    _stepTimingService.PauseAllColumnsTiming();
    _scannerOwnership.ReleaseAllForReset();
    return wasInScanPhase;  // → тип сброса
}
```

Контракт:

- PLC reset снимает **весь** scanner ownership (`Dialog` + `PreExecution`);
- UI-close/reset hooks вне `ScanModeController` снимают только `Dialog` owner;
- `PreExecution` owner не должен возвращаться от одного факта закрытия диалога, если reset lifecycle ещё не завершён.

### OnResetCompleted

```csharp
private void HandleResetCompleted()
{
    TransitionToReadyInternal();
}
```

**TransitionToReadyInternal:**
1. `_isResetting = false`
2. Если `!IsScanModeEnabled` → полная остановка
3. Если `!_isActivated` → первичная активация
4. Иначе:
   - если активен `InterruptReasonDialog` в `PreExecutionCoordinator`, restart scan-таймера блокируется (`ScanTimingRestartBlockedByInterruptDialog`);
   - если диалог не активен — `ResetScanTiming()` + `AcquireSession()` + синхронизация Scan-таймера с текущей входной готовностью.

`AcquireSession()` здесь возвращает только ordinary `PreExecution` owner. Если активен scanner-dialog, `BoilerInfo` не должен считаться ordinary-ready.

Deferred completion contract:
- explicit outcome `full cleanup` снимает `_resetReadyTransitionPending` и завершает reset-state;
- ordinary scanner owner возвращается только если на момент catch-up актуальны `IsScanModeEnabled = true` и `OpcUaConnectionState.IsConnected = true`;
- при `AutoReady = false` или отсутствии PLC-связи deferred transition не делает `BoilerInfo` editable и не переводит raw scanner в ordinary-ready.
## Синхронизация Scan-таймера по входной готовности

`ScanModeController` синхронизирует тик Scan-таймера только для сценария ожидания barcode:

- При `IsAcceptingInput = true` и (`IsConnected = false` **или** `IsScanModeEnabled = false`) — Scan-таймер ставится на паузу.
- При активном `InterruptReasonDialog` Scan-таймер также принудительно остаётся на паузе, даже если `IsAcceptingInput = false`.
- Возобновление допускается только если одновременно выполнены условия: `IsConnected = true`, `IsScanModeEnabled = true`, reset не активен и контроллер уже активирован.
- Если система не находится в фазе ожидания barcode (`IsAcceptingInput = false`), внутренний флаг паузы для входной готовности сбрасывается.

## Диаграмма состояний

```
                    ┌──────────────┐
                    │   Inactive   │
                    │ _isActivated │
                    │   = false    │
                    └──────┬───────┘
                           │ IsScanModeEnabled
                           ▼
                    ┌──────────────┐
        ┌──────────►│    Active    │◄──────────┐
        │           │ _isActivated │           │
        │           │   = true     │           │
        │           └──────┬───────┘           │
        │                  │                   │
        │ ResetCompleted   │ ResetStarting     │ !IsScanModeEnabled
        │                  ▼                   │ (soft conditions)
        │           ┌──────────────┐           │
        │           │  Resetting   │           │
        │           │ _isResetting │           │
        └───────────│   = true     │───────────┘
                    └──────────────┘
```

## Потокобезопасность

- Все изменения состояния защищены `Lock _stateLock`
- `IsInScanningPhase` — thread-safe свойство для внешних вызовов
- `IsInScanningPhaseUnsafe` — только внутри lock

## Зависимости

| Сервис | Назначение |
|--------|------------|
| `ScanSessionManager` | Управление сессией сканера |
| `ScannerInputOwnershipService` | Единый ownership raw scanner между `PreExecution` и scanner-диалогами |
| `OperatorState` | Состояние авторизации оператора |
| `AutoReadySubscription` | Состояние готовности автомата |
| `OpcUaConnectionState` | Состояние PLC-связи для синхронизации Scan-таймера |
| `StepStatusReporter` | Отображение статуса в UI |
| `PreExecutionCoordinator` | Координация pre-execution фазы |
| `PlcResetCoordinator` | Координация сброса PLC |
| `IStepTimingService` | Управление таймерами шагов |
| `ExecutionActivityTracker` | Отслеживание активности выполнения |

## События

| Событие | Когда |
|---------|-------|
| `OnStateChanged` | Изменение состояния режима сканирования |
| `AutoReadySubscription.OnStateChanged` | Изменение AutoReady — пересинхронизация Scan-таймера в ожидании barcode |
| `OpcUaConnectionState.ConnectionStateChanged` | Потеря/восстановление PLC-связи — пересинхронизация Scan-таймера |
| `PreExecutionCoordinator.OnStateChanged` | Вход/выход из ожидания barcode — пересинхронизация Scan-таймера |

## Связанные Guide

- [PlcResetGuide.md](PlcResetGuide.md) — логика сброса PLC
- [StateManagementGuide.md](../execution/StateManagementGuide.md) — общее управление состоянием
- [ExecutionActivityTrackerGuide.md](../execution/ExecutionActivityTrackerGuide.md) — отслеживание активности
