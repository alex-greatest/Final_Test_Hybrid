# ScanModeController Guide

## Назначение

`ScanModeController` управляет режимом сканирования: активация/деактивация на основе состояния оператора и автомата. Координирует управление сессией сканера, синхронизирует Scan-таймер с PLC-связью в фазе ожидания barcode и уведомляет подписчиков об изменении состояния.

## Условия активации

Режим сканирования активен когда:
- Оператор авторизован (`OperatorState.IsAuthenticated`)
- Автомат готов (`AutoReadySubscription.IsReady`)

```
IsScanModeEnabled = IsAuthenticated && IsReady
```

## Состояния

| Флаг | Описание |
|------|----------|
| `_isActivated` | Режим сканирования активирован |
| `_isResetting` | Выполняется сброс PLC |
| `_scanPausedByConnectionLoss` | Scan-таймер поставлен на паузу из-за потери PLC-связи во время ожидания barcode |

### IsInScanningPhase

```csharp
IsInScanningPhase = _isActivated && !_isResetting
```

Используется `PlcResetCoordinator` для определения типа сброса:
- `true` → мягкий сброс (ForceStop)
- `false` → жёсткий сброс (Reset)

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
2. `AcquireSession()` — захват сканера
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

**Полная деактивация:**
- Отмена main loop
- `_isActivated = false`
- Освобождение сканера
- Очистка grid (если оператор не авторизован)

## Обработка сброса PLC

### OnResetStarting

```csharp
private bool HandleResetStarting()
{
    var wasInScanPhase = IsInScanningPhaseUnsafe;
    _isResetting = true;
    _stepTimingService.PauseAllColumnsTiming();
    _sessionManager.ReleaseSession();
    return wasInScanPhase;  // → тип сброса
}
```

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
4. Иначе → `ResetScanTiming()` + `AcquireSession()` + синхронизация Scan-таймера с текущим состоянием PLC-связи

## Синхронизация Scan-таймера по PLC-связи

`ScanModeController` синхронизирует тик Scan-таймера только для сценария ожидания barcode:

- При `IsAcceptingInput = true` и `IsConnected = false` — Scan-таймер ставится на паузу.
- При восстановлении связи — таймер возобновляется только если scan-mode активен (`IsScanModeEnabled = true`), reset не активен и контроллер уже активирован.
- Если система не находится в фазе ожидания barcode (`IsAcceptingInput = false`), флаг паузы по connection-loss сбрасывается.

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
| `OpcUaConnectionState.ConnectionStateChanged` | Потеря/восстановление PLC-связи — пересинхронизация Scan-таймера |
| `PreExecutionCoordinator.OnStateChanged` | Вход/выход из ожидания barcode — пересинхронизация Scan-таймера |

## Связанные Guide

- [PlcResetGuide.md](PlcResetGuide.md) — логика сброса PLC
- [StateManagementGuide.md](../execution/StateManagementGuide.md) — общее управление состоянием
- [ExecutionActivityTrackerGuide.md](../execution/ExecutionActivityTrackerGuide.md) — отслеживание активности
