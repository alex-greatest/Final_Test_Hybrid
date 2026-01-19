# Change: Fix Scan Timing Loss on Step Transition

## Why

При уходе с шага сканирования на другие шаги теста время сканирования **теряется**. Оператор не видит, сколько времени занял scan step.

### Текущее поведение (БАГ)

```
1. StartScanTiming() → _scanState.Start() → IsRunning=true, Name="Scan"
2. StopScanTiming()  → _scanState.Stop()  → IsRunning=false, Name="Scan" (!!!)
3. GetAll() возвращает scan state (IsActive=true, т.к. Name!=null)
4. Clear() в ClearForTestCompletion → _records.Clear() (scan state остаётся!)
5. ResetScanTiming() или новый StartScanTiming() → перезаписывает данные
```

### Сравнение с Column Timing (работает правильно)

```csharp
// StopColumnStepTiming — ПРАВИЛЬНО сохраняет время
var duration = state.CalculateDuration();
_records.Add(new StepTimingRecord(...));  // ← Сохраняет!
state.Clear();                             // ← Очищает state
```

```csharp
// StopScanTiming — НЕ сохраняет время
_scanState.Stop();  // Только IsRunning=false, данные остаются но не в _records
```

## What Changes

### Исправить StopScanTiming — сохранять время в _records

**Было:**
```csharp
public void StopScanTiming()
{
    lock (_lock)
    {
        if (!_scanState.IsRunning) return;
        _scanState.Stop();
    }
    UpdateTimerState();
    OnChanged?.Invoke();
}
```

**Станет:**
```csharp
public void StopScanTiming()
{
    lock (_lock)
    {
        if (!_scanState.IsActive) return;

        var duration = _scanState.CalculateDuration();
        _records.Insert(0, new StepTimingRecord(
            _scanState.Id, _scanState.Name!, _scanState.Description!,
            FormatDuration(duration)));

        _scanState.Clear();
    }
    UpdateTimerState();
    OnChanged?.Invoke();
}
```

**Изменения:**
1. Проверка `IsActive` вместо `IsRunning` (шаг может быть на паузе)
2. Сохранение времени в `_records` (Insert(0, ...) — scan step всегда первый)
3. Вызов `Clear()` вместо `Stop()` — полная очистка state

## Impact

- **Файлы:** `StepTimingService.Scan.cs`
- **Breaking changes:** Нет
- **Risk:** Низкий — локальное изменение одного метода
- **Тестирование:** Запустить тест, проверить что время scan step сохраняется после перехода к BlockBoilerAdapterStep

## Verification

После исправления:
1. Scan step начинается → таймер показывает время
2. Scan step завершается → время сохраняется в гриде
3. Следующие шаги выполняются → время scan step остаётся видимым
4. Тест завершается → время scan step очищается вместе с остальными в `Clear()`
