# Change: Refactor ScanModeController — Fix Bugs + Extract State Machine

## Why

Анализ выявил **6 критических проблем** в текущем коде:

| Проблема | Строки | Риск |
|----------|--------|------|
| `OnStateChanged` вне lock | 142 | Обработчики видят несогласованное состояние |
| `_loopCts` не Dispose() | 179, 214 | Утечка CancellationTokenSource |
| `Dispose()` без lock | 236-249 | Double-dispose race condition |
| `_isActivated && _isResetting` возможны | 45, 111 | Невалидная комбинация состояний |
| Рассинхронизация CTS | 226 | Loop работает, pipeline отменён |
| `IsScanModeEnabled` TOCTOU | 133 | Race между проверкой и действием |

Рефакторинг — это **bugfix + structural improvement**.

## What Changes

### 1. Заменить boolean поля на enum (исключает невалидные состояния)

**Было:**
```csharp
private bool _isActivated;
private bool _isResetting;
// 4 комбинации, 1 невалидная
```

**Станет:**
```csharp
private ScanModePhase _phase; // Idle, Active, Resetting — только 3 валидных
```

### 2. Исправить утечку CTS

**Было:**
```csharp
_loopCts?.Cancel();
_loopCts = new CancellationTokenSource(); // Старый не Dispose!
```

**Станет:**
```csharp
var old = Interlocked.Exchange(ref _loopCts, new CancellationTokenSource());
old?.Cancel();
old?.Dispose();
```

### 3. Исправить Dispose()

**Было:**
```csharp
public void Dispose()
{
    if (_disposed) return;  // БЕЗ LOCK!
    _disposed = true;
    _loopCts?.Cancel();     // RACE!
```

**Станет:**
```csharp
public void Dispose()
{
    lock (_stateLock)
    {
        if (_disposed) return;
        _disposed = true;
        _loopCts?.Cancel();
        _loopCts?.Dispose();
    }
    // Unsubscribe events вне lock (safe)
```

### 4. Захватить IsScanModeEnabled в локальную переменную

**Было:**
```csharp
if (IsScanModeEnabled)  // Property call внутри lock
{
    TryActivateScanMode();
}
```

**Станет:**
```csharp
var enabled = _operatorState.IsAuthenticated && _autoReady.IsReady;
if (enabled)
{
    TryActivateScanMode();
}
```

### 5. Выделить ScanModeStateMachine (опционально)

State machine инкапсулирует:
- Enum `ScanModePhase`
- Валидные переходы
- Thread-safe методы `TryTransition()`

Это **изолирует** логику состояний от координации сервисов.

## Impact

- Affected code:
  - `ScanModeController.cs` — рефакторинг + bugfixes
  - Новый: `ScanModePhase.cs` (enum)
  - Опционально: `ScanModeStateMachine.cs`
- Breaking changes: Нет (публичный API сохраняется)
- Risk: Низкий — логика не меняется, только структура

## Design Decision

**Минимальный вариант (рекомендую):**
1. Заменить `_isActivated` + `_isResetting` на `_phase` enum
2. Исправить CTS lifecycle
3. Исправить Dispose()
4. Без выделения отдельного класса StateMachine

**Расширенный вариант:**
- Дополнительно выделить `ScanModeStateMachine` класс

Рекомендую минимальный — меньше изменений, те же гарантии безопасности.