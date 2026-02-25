# PlcResetGuide.md

## Обзор

Логика сброса по сигналу PLC управляется через `PlcResetCoordinator` и координируется с `PreExecutionCoordinator`.

## Типы сброса

| Тип | Когда | Метод ErrorCoordinator |
|-----|-------|------------------------|
| **Мягкий (SoftStop)** | `wasInScanPhase = true` | `ForceStop()` |
| **Жёсткий (HardReset)** | `wasInScanPhase = false` | `Reset()` |

## Важно: PLC reset и HardReset — разные потоки

- **PLC reset flow (по тегу `Req_Reset`)**: стартует в `PlcResetCoordinator`, даёт `OnForceStop`, затем выполняет PLC-логику с ожиданием `AskEnd` (или timeout/cancel fallback).
- **HardReset flow (по `ErrorCoordinator.OnReset`)**: приходит как причина остановки (`ExecutionStopReason.PlcHardReset`) и должен обрабатываться сразу, без ожидания `AskEnd`.
- Следствие для `PreExecution`: нельзя трактовать любой `HardReset` как часть `AskEnd`-цепочки `PlcResetCoordinator`; это отдельный путь выхода.

### Маркер источника HardReset (`PlcHardResetPending`)

- В `PlcResetCoordinator` перед `Reset()` выставляется `PlcHardResetPending = 1`.
- Сброс маркера выполняется в `finally`, даже если `_errorCoordinator.Reset()` выбросит исключение.
- Это обязательная гарантия: маркер не должен оставаться в значении `1` после аварийного выхода из reset-flow.

### Инвариант reset-cycle в PreExecutionCoordinator

- Любой новый reset-cycle (PLC и non-PLC) обязан увеличивать `_resetSequence`.
- Старт reset-cycle выполняется через `BeginResetCycle(origin, ensureAskEndWindow)`:
  - PLC-путь: `BeginResetCycle(ResetOriginPlc, true)` — `seq++`, повторное вооружение one-shot cleanup, открытие нового `ResetAskEndWindow(seq)`.
  - non-PLC HardReset-путь: `BeginResetCycle(ResetOriginNonPlc, false)` — `seq++`, повторное вооружение one-shot cleanup, **без** открытия AskEnd-окна.
- Лог `Старт reset-цикла: seq=..., source=..., cleanupArmed=true` формируется только в `BeginResetCycle(...)`.
- AskEnd-window хранится в `_currentAskEndWindow` и всегда привязан к конкретному `seq`.
- `CompletePlcReset(seq)` завершает только окно того же `seq`; stale-окна не завершают текущий цикл.

### wasInScanPhase — определение

```csharp
// ScanModeController.cs
var wasInScanPhase = IsInScanningPhase;  // _isActivated && !_isResetting
```

**Важно:** НЕ зависит от того, выполняется ли тест. Если ScanMode активирован → `wasInScanPhase = true`.

## Цепочка событий

```
PlcResetCoordinator.HandleResetAsync()
  ↓
SignalForceStop() → OnForceStop.Invoke()
  ↓
┌─ TestExecutionCoordinator.HandleForceStop() → Stop()
├─ PreExecutionCoordinator.HandleSoftStop() → HandleStopSignal()
└─ ReworkDialogService.HandleForceStop() → Close()
  ↓
ExecuteSmartReset(wasInScanPhase)
  ↓
wasInScanPhase ? ForceStop() : Reset()
  ↓
OnResetCompleted.Invoke()
```

## HandleStopSignal — постановка stop-reason и маршрут очистки

```csharp
private void HandleStopSignal(PreExecutionResolution resolution)
{
    var exitReason = resolution == PreExecutionResolution.SoftStop
        ? CycleExitReason.SoftReset
        : CycleExitReason.HardReset;
    var stopReason = resolution == PreExecutionResolution.SoftStop
        ? ExecutionStopReason.PlcSoftReset
        : ExecutionStopReason.PlcHardReset;

    state.FlowState.RequestStop(stopReason, stopAsFailure: true);
    SignalReset(exitReason);
    if (TryCancelActiveOperation(exitReason))
    {
        // Очистка произойдёт после отмены активной операции через HandleCycleExit()
    }
    else
    {
        // Нет активной операции — обработка выхода сразу
        HandleCycleExit(exitReason);
    }
    SignalResolution(resolution);
}
```

- Rearm one-shot guard и `seq++` выполняются только при старте нового reset-cycle:
  - PLC reset через `BeginPlcReset()` → `BeginResetCycle(ResetOriginPlc, true)`;
  - non-PLC hard reset через `HandleHardReset()` → `BeginResetCycle(ResetOriginNonPlc, false)`.

### TryCancelActiveOperation()

| Условие | Действие | Где очистка |
|---------|----------|-------------|
| `TestCoordinator.IsRunning` или `IsPreExecutionActive` | `_pendingExitReason = exitReason`, `_currentCts.Cancel()` | В `HandleCycleExit()` после выхода из активной операции |
| Иначе | — | Сразу через `HandleCycleExit(exitReason)` |

### ClearStateOnReset()

```csharp
private void ClearStateOnReset()
{
    state.BoilerState.Clear();
    state.PhaseState.Clear();
    ClearBarcode();
    infra.ErrorService.IsHistoryEnabled = false;
    infra.StepTimingService.Clear();
    infra.RecipeProvider.Clear();
}
```

## AskEnd-блокировка (MainLoop)

Во время PLC reset цикл ввода НЕ должен продолжаться до получения AskEnd.
PreExecutionCoordinator:
- ждёт AskEnd перед стартом нового цикла;
- отменяет ожидание штрихкода при reset;
- для PLC-reset пути продолжает цикл только после AskEnd.
- для HardReset допускает fallback-очистку в `HandleHardResetExit`, если AskEnd путь не завершил cleanup.
- stale `AskEnd` игнорируется в `ExecuteGridClearAsync()` при отсутствии окна или несовпадении `window.Sequence != currentSeq`.

### Таймауты reset-flow (конфигурируемые)

- Таймауты задаются в `OpcUa:ResetFlowTimeouts`:
  - `AskEndTimeoutSec` — ожидание `Ask_End`;
  - `ReconnectWaitTimeoutSec` — параметр конфигурации reset-flow (сохранён для совместимости);
  - `ResetHardTimeoutSec` — общий дедлайн reset-flow.
- `ResetHardTimeoutSec` должен быть `>= AskEndTimeoutSec` и `>= ReconnectWaitTimeoutSec` (валидация в `ResetFlowTimeoutsSettings`).
- Значения по умолчанию в `appsettings.json`: `AskEnd=60`, `ReconnectWait=15`, `Hard=60` секунд.
- Если во время ожидания `Ask_End` пропадает связь с PLC, reset-flow срабатывает по fail-fast пути:
  `HandleInterruptAsync(PlcConnectionLost)` + `OnResetCompleted` (без ожидания reconnect до timeout).
- `HandleInterruptAsync(TagTimeout)` + `OnResetCompleted` используется для сценария, когда связь есть, но `Ask_End` не пришёл до дедлайна.

## Три состояния MainLoop

```
RunSingleCycleAsync:
0. WaitForAskEndIfNeededAsync()    // Блокировка цикла при PLC reset
1. SetAcceptingInput(true)         // IsAcceptingInput = true
2. WaitForBarcodeAsync()           // Ожидание ввода (отменяется reset'ом)
3. SetAcceptingInput(false)
4. _currentCts = Create...         // CTS создаётся ПОСЛЕ получения баркода
5. try {
     SetPreExecutionActive(true)   // IsPreExecutionActive = true
     ExecutePreExecutionPipelineAsync()
     if (TestStarted) {
       WaitForTestCompletionAsync() // TestCoordinator.IsRunning = true
     }
   }
   catch (OperationCanceledException) when (reset signaled) {
     // exitReason = _pendingExitReason (Soft/Hard reset)
     // cleanup выполняется в HandleCycleExit(exitReason)
   }
```

## Гарантии очистки BoilerState

| Состояние при сбросе | Путь очистки |
|---------------------|--------------|
| `WaitForBarcodeAsync` | `HandleCycleExit(Soft/HardReset)` (сразу) + общий one-shot guard |
| `ExecutePreExecutionPipelineAsync` | `Cancel()` → `HandleCycleExit(Soft/HardReset)` |
| `WaitForTestCompletionAsync` | reset-сигнал → выход из ожидания → `HandleCycleExit(Soft/HardReset)` |
| `AskEnd` пришёл после cleanup | `TryRunResetCleanupOnce()` блокирует повторную очистку |
| `AskEnd` не пришёл для HardReset | cleanup в `HandleHardResetExit` (fallback, без дубля) |

## Диалог причины прерывания при SoftReset

Если в момент soft reset был активный тест и включён `UseInterruptReason`, после `AskEnd` открывается диалог `Причина прерывания`.

- На одну серию reset разрешён максимум один показ диалога.
- Серийный latch ставится на первом `SoftReset` серии независимо от фактического показа диалога.
- Серия reset завершается только при запуске нового pre-exec pipeline (`InitializeTestRunningAsync`).
- Любой новый reset (Soft/Hard) немедленно закрывает активный диалог (`CancelActiveDialog`).
- На время активного диалога принудительно замораживаются step timers (`PauseAllColumnsTiming`), включая Scan.
- Попытка restart scan-таймера из `OnResetCompleted` в этом окне блокируется (`ScanTimingRestartBlockedByInterruptDialog`).

- Текущий UX: **без** окна `Авторизация администратора`, сразу ввод причины.
- Маршрут сохранения не меняется:
  - `UseMes=true` → MES;
  - `UseMes=false` → локальная БД.
- Обратимость зафиксирована в коде флагом `bypassAdminAuthInSoftResetInterrupt`:
  `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Subscriptions.cs`.
- Rework-flow (`ReworkDialogService`) не затрагивается и по-прежнему использует отдельную admin-авторизацию.

## Changeover ownership в reset-сценариях

- `ChangeoverStartGate` больше не слушает `OnAskEndReceived` и не делает deferred-старт.
- `ChangeoverStartGate` только принимает `RequestStartFromAutoReady()` и публикует `OnAutoReadyRequested`.
- Для защиты от late-subscribe в `ChangeoverStartGate` используется one-shot pending/replay:
  - при `RequestStartFromAutoReady()` выставляется pending-флаг;
  - при подписке `PreExecutionCoordinator` вызывает `TryConsumePendingAutoReadyRequest()` и, если нужно, выполняет catch-up (`AutoReadyReplayConsumed`).
- Источник сигнала для этого пути — `AutoReadySubscription.OnFirstAutoReceived` (one-shot): влияние `AutoReady` на старт changeover ограничено первым запуском.
- Единственный owner финального старта changeover в reset-сценариях — `PreExecutionCoordinator` (sequence-aware проверка).
- Дополнительные sequence-aware диагностики:
  - `ChangeoverStartDeferredBySeq` — AskEnd текущего seq ещё не получен, старт отложен;
  - `ChangeoverStartRejectedAsStale` — pending/AskEnd относятся к старому seq;
  - `ChangeoverStartSkippedDuplicateSeq` — защита от повторного старта для того же seq.

## Разница ForceStop vs Reset

| Аспект | ForceStop() | Reset() |
|--------|-------------|---------|
| PauseToken.Resume() | ✓ | ✓ |
| ClearCurrentInterrupt() | ✓ | ✓ |
| OnReset event | ✗ | ✓ |
| Используется | Мягкий сброс | Жёсткий сброс |

## Подписчики событий

### OnForceStop
- `TestExecutionCoordinator.HandleForceStop()`
- `PreExecutionCoordinator.HandleSoftStop()`
- `ReworkDialogService.HandleForceStop()`
- `BoilerInfo.CloseDialogs()`
- `IModbusDispatcher.StopAsync()` — прерывает диагностику

### OnReset (ErrorCoordinator)
- `TestExecutionCoordinator.HandleReset()`
- `PreExecutionCoordinator.HandleHardReset()`
- `ReworkDialogService.HandleReset()`
- `BoilerInfo.CloseDialogs()`
- `IModbusDispatcher.StopAsync()` — прерывает диагностику

### OnResetCompleted
- `ScanModeController.TransitionToReadyInternal()`
- Событие считается маркером завершения reset-процесса для scan-mode и вызывается в сценариях:
  - успешный reset;
  - fail-fast по `PlcConnectionLost` во время ожидания `Ask_End`;
  - таймаут `Ask_End`;
  - runtime-отмена reset до `Ask_End` (без disposal);
  - неожиданные ошибки с fallback в hard reset.
- Исключение: при отмене из `DisposeAsync` (`_disposed = true`) `OnResetCompleted` не вызывается.

## ScanModeController — поведение при reset

### OnResetStarting

```csharp
private bool HandleResetStarting()
{
    lock (_stateLock)
    {
        var wasInScanPhase = IsInScanningPhaseUnsafe;
        _isResetting = true;
        _stepTimingService.PauseAllColumnsTiming();  // Паузим все таймеры
        _sessionManager.ReleaseSession();
        return wasInScanPhase;
    }
}
```

### Блокировка активации во время reset

```csharp
private void TryActivateScanMode()
{
    if (_isResetting) return;  // Блокируем активацию пока reset активен
    // ...
}
```

### OnResetCompleted — catch-up активация

```csharp
private void TransitionToReadyInternal()
{
    _isResetting = false;
    if (!IsScanModeEnabled)
    {
        _loopCts?.Cancel();
        _isActivated = false;
        _stepTimingService.PauseAllColumnsTiming();
        return;
    }
    if (!_isActivated)
    {
        PerformInitialActivation();  // Catch-up если активация была заблокирована
        return;
    }
    if (!_preExecutionCoordinator.IsInterruptReasonDialogActive())
    {
        _stepTimingService.ResetScanTiming();
    }
    _sessionManager.AcquireSession(HandleBarcodeScanned);
}
```

### Гарантии при reset

| Сценарий | Поведение |
|----------|-----------|
| Reset + AutoReady выключен после | Таймеры на паузе, сессия не захватывается |
| Reset + AutoReady включён во время | Активация заблокирована, catch-up после завершения |
| Жёсткий reset (wasInScanPhase=false) | Таймеры паузятся, catch-up активация если нужна |
| Runtime-отмена reset (без disposal) | Через `OnResetCompleted` снимается `_isResetting` и выполняется catch-up |

## Диагностика (Modbus) при PLC Reset

При любом PLC reset (мягком или жёстком) диспетчер диагностики останавливается:

```
OnForceStop / OnReset
    ↓
StopDispatcherSafely(dispatcher)  // fire-and-forget с обработкой ошибок
    ↓
dispatcher.StopAsync()
    ↓
1. Завершаются каналы команд (TryComplete)
2. Отменяется CancellationToken
3. Закрывается COM-порт НЕМЕДЛЕННО (прерывает текущую Modbus команду)
4. Ожидается завершение worker loop (таймаут 5 сек)
5. Отменяются все pending команды
6. Очищается состояние (IsConnected = false, LastPingData = null)
```

### Поведение при активной операции

| Состояние команды | Что происходит |
|-------------------|----------------|
| В очереди (pending) | Отменяется мгновенно |
| Выполняется на порту | **Прерывается немедленно** через Close() → IOException |
| Ping keep-alive | Отменяется через CancellationToken |

### Защита от зависания

| Сценарий | Поведение |
|----------|-----------|
| Worker завершился < 5 сек | Рестарт разрешён |
| Worker таймаут > 5 сек | **Рестарт заблокирован**, CRITICAL лог |
| Ошибка в StopAsync | Логируется через ContinueWith |

### Восстановление после reset

После PLC reset диагностика **не запускается автоматически**. Для восстановления связи нужен явный вызов:

```csharp
await dispatcher.StartAsync();
```

**Важно:** Рестарт возможен только если предыдущий `StopAsync()` завершился успешно (без таймаута).

**Примечание:** В текущей реализации диагностика запускается при старте приложения в `Form1.ConfigureDiagnosticEvents()`. После PLC reset пользователь может вручную инициировать рестарт если требуется.
