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

## HandleStopSignal — очистка состояния

```csharp
private void HandleStopSignal(PreExecutionResolution resolution)
{
    if (TryCancelActiveOperation())
    {
        // Очистка произойдёт позже
    }
    else
    {
        // Нет активной операции — очищаем сразу
        ClearStateOnReset();
    }
    SignalResolution(resolution);
}
```

### TryCancelActiveOperation()

| Условие | Действие | Где очистка |
|---------|----------|-------------|
| `TestCoordinator.IsRunning` | `_resetRequested = true` | `HandlePostTestCompletion()` |
| `IsPreExecutionActive` | `_resetRequested = true`, `Cancel()` | catch блок в MainLoop |
| Иначе | — | Сразу в `HandleStopSignal` |

### ClearStateOnReset()

```csharp
private void ClearStateOnReset()
{
    state.BoilerState.Clear();
    state.PhaseState.Clear();
    ClearBarcode();
}
```

## AskEnd-блокировка (MainLoop)

Во время PLC reset цикл ввода НЕ должен продолжаться до получения AskEnd.
PreExecutionCoordinator:
- ждёт AskEnd перед стартом нового цикла;
- отменяет ожидание штрихкода при reset;
- для PLC-reset пути продолжает цикл только после AskEnd.
- для HardReset пути выходит немедленно, без ожидания AskEnd.

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
       HandlePostTestCompletion()
     }
   }
   catch (OperationCanceledException) when (reset signaled) {
     ClearStateOnReset()
   }
```

## Гарантии очистки BoilerState

| Состояние при сбросе | Путь очистки |
|---------------------|--------------|
| `WaitForBarcodeAsync` | `HandleStopSignal` → `ClearStateOnReset()` сразу |
| `ExecutePreExecutionPipelineAsync` | `Cancel()` → catch → `ClearStateOnReset()` |
| `WaitForTestCompletionAsync` | `Stop()` → `OnSequenceCompleted` → `HandlePostTestCompletion` → `ClearStateOnReset()` |

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
    _stepTimingService.ResetScanTiming();
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
