# PlcResetGuide.md

## Обзор

Логика сброса по сигналу PLC управляется через `PlcResetCoordinator` и координируется с `PreExecutionCoordinator`.

## Типы сброса

| Тип | Когда | Метод ErrorCoordinator |
|-----|-------|------------------------|
| **Мягкий (SoftStop)** | `wasInScanPhase = true` | `ForceStop()` |
| **Жёсткий (HardReset)** | `wasInScanPhase = false` | `Reset()` |

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

## Три состояния MainLoop

```
RunSingleCycleAsync:
1. SetAcceptingInput(true)         // IsAcceptingInput = true
2. WaitForBarcodeAsync()           // Ожидание ввода
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
   catch (OperationCanceledException) when (_resetRequested) {
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
| ClearCurrentInterrupt() | ✗ | ✓ |
| OnReset event | ✗ | ✓ |
| Используется | Мягкий сброс | Жёсткий сброс |

## Подписчики событий

### OnForceStop
- `TestExecutionCoordinator.HandleForceStop()`
- `PreExecutionCoordinator.HandleSoftStop()`
- `ReworkDialogService.HandleForceStop()`
- `BoilerInfo.CloseDialogs()`

### OnReset (ErrorCoordinator)
- `TestExecutionCoordinator.HandleReset()`
- `PreExecutionCoordinator.HandleHardReset()`
- `ReworkDialogService.HandleReset()`
- `BoilerInfo.CloseDialogs()`

### OnResetCompleted
- `ScanModeController.TransitionToReadyInternal()`
