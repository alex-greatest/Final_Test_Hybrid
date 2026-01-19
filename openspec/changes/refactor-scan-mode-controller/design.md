# Design: ScanModeController Refactoring

## Context

Анализ субагентов выявил 6 критических проблем. Рефакторинг исправляет баги и улучшает структуру.

## Goals / Non-Goals

**Goals:**
- Исправить утечку CancellationTokenSource
- Исправить race condition в Dispose()
- Исключить невалидные комбинации состояний через enum
- Сохранить существующее поведение

**Non-Goals:**
- Выделение отдельного класса StateMachine (оверинжиниринг)
- Изменение публичного API
- Рефакторинг связанных сервисов

## Decisions

### Decision 1: Enum вместо двух boolean

```csharp
// БЫЛО: 4 комбинации, 1 невалидная
private bool _isActivated;
private bool _isResetting;

// СТАНЕТ: 3 валидных состояния
public enum ScanModePhase { Idle, Active, Resetting }
private ScanModePhase _phase = ScanModePhase.Idle;
```

**Маппинг:**
| _isActivated | _isResetting | → | _phase |
|--------------|--------------|---|--------|
| false | false | → | Idle |
| true | false | → | Active |
| true | true | → | Resetting |
| false | true | → | ❌ невозможно |

### Decision 2: CTS Lifecycle Fix

```csharp
private void StartMainLoop()
{
    // БЫЛО: утечка
    _loopCts?.Cancel();
    _loopCts = new CancellationTokenSource();

    // СТАНЕТ: правильный lifecycle
    var old = _loopCts;
    _loopCts = new CancellationTokenSource();
    old?.Cancel();
    old?.Dispose();

    _ = _preExecutionCoordinator.StartMainLoopAsync(_loopCts.Token)...
}
```

### Decision 3: Thread-safe Dispose

```csharp
public void Dispose()
{
    CancellationTokenSource? cts;
    lock (_stateLock)
    {
        if (_disposed) return;
        _disposed = true;
        cts = _loopCts;
        _loopCts = null;
    }

    cts?.Cancel();
    cts?.Dispose();

    // Unsubscribe вне lock — безопасно
    _operatorState.OnStateChanged -= UpdateScanModeState;
    _autoReady.OnStateChanged -= UpdateScanModeState;
    _plcResetCoordinator.OnResetStarting -= HandleResetStarting;
    _plcResetCoordinator.OnResetCompleted -= HandleResetCompleted;
}
```

### Decision 4: TOCTOU Fix

```csharp
private void UpdateScanModeState()
{
    lock (_stateLock)
    {
        if (_disposed) return;

        // БЫЛО: property call может race
        // if (IsScanModeEnabled) ...

        // СТАНЕТ: захват в локальную переменную
        var enabled = _operatorState.IsAuthenticated && _autoReady.IsReady;
        if (enabled)
        {
            TryActivateScanMode();
        }
        else
        {
            TryDeactivateScanMode();
        }
    }
    OnStateChanged?.Invoke();
}
```

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Ошибка в маппинге состояний | Таблица маппинга выше + тесты |
| Регрессия в event handling | Порядок вызовов не меняется |
| CTS race при Dispose | Захват в локальную переменную |

## Migration Strategy

**Пошаговый подход:**

1. Сначала добавить `ScanModePhase` enum (не ломает код)
2. Добавить `_phase` поле рядом с `_isActivated`/`_isResetting`
3. Синхронизировать оба подхода (assert что совпадают)
4. Убедиться что всё работает
5. Удалить старые поля

Это позволяет откатиться на любом шаге.