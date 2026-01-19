# Proposal: Fix CurrentBarcode Not Cleared on Certain Exit Paths

## Summary

Исследование выявило **5 сценариев**, при которых `CurrentBarcode` может остаться установленным после завершения теста, что приведёт к использованию старого штрихкода при следующем цикле.

## Problem Statement

Пользователь наблюдал ситуацию, когда `CurrentBarcode` не сбрасывался при завершении. Анализ кода выявил несколько путей, где сброс не происходит.

## Root Cause Analysis

### Таблица путей очистки CurrentBarcode

| CycleExitReason | CurrentBarcode сбрасывается? | Метод |
|-----------------|------------------------------|-------|
| `TestCompleted` | ✅ Да | `ClearForTestCompletion()` |
| `HardReset` | ✅ Да | `ClearStateOnReset()` |
| `SoftReset` | ⚠️ Только по `AskEnd` | `HandleGridClear()` |
| `RepeatRequested` | ❌ Нет (намеренно) | `ClearForRepeat()` |
| `NokRepeatRequested` | ❌ Нет (намеренно) | `ClearForNokRepeat()` |
| `PipelineFailed` | ❌ Нет | (ничего) |
| `PipelineCancelled` | ❌ Нет | (ничего) |

### Выявленные проблемы

#### 1. SoftReset без AskEnd (HIGH)

**Сценарий:**
```
1. Тест выполняется
2. PLC отправляет Req_Reset, wasInScanPhase = true
3. HandleCycleExit(SoftReset) → НИЧЕГО не происходит (ждём AskEnd)
4. AskEnd не приходит или отменяется
5. CurrentBarcode остаётся установлен!
```

**Код:**
```csharp
// MainLoop.cs:127-129
case CycleExitReason.SoftReset:
    // Ничего - очистка произойдёт по AskEnd в HandleGridClear
    break;
```

**Проблема:** Полагается на внешний сигнал `AskEnd`, который может не прийти.

---

#### 2. PipelineFailed / PipelineCancelled (MEDIUM)

**Сценарий:**
```
1. Штрихкод отсканирован → CurrentBarcode установлен
2. ScanStep или BlockBoilerAdapterStep падает с ошибкой
3. HandleCycleExit(PipelineFailed) → НИЧЕГО не происходит
4. Следующая итерация:
   - _skipNextScan = false, НО CurrentBarcode ещё установлен
   - WaitForBarcodeAsync() перезаписывает его новым значением ✓
```

**Вывод:** На самом деле не критично — `WaitForBarcodeAsync` всегда вызывается при `!_skipNextScan`.

---

#### 3. NullReferenceException при HardReset между повторами (HIGH)

**Сценарий:**
```
1. RepeatRequested → HandleCycleExit() → _skipNextScan = true
2. Цикл готовится к следующей итерации...
3. PLC отправляет Req_Reset (HardReset) МЕЖДУ итерациями
4. HandleHardReset() → HandleStopSignal(HardReset)
5. TryCancelActiveOperation() returns false (нет активной операции)
6. HandleCycleExit(HardReset) → ClearStateOnReset() → CurrentBarcode = null
7. НО _skipNextScan всё ещё true!
8. Следующая итерация RunSingleCycleAsync():
   if (_skipNextScan) barcode = CurrentBarcode!;  ← NullReferenceException!
```

**Код:**
```csharp
// MainLoop.cs:34-36
if (_skipNextScan)
{
    barcode = CurrentBarcode!;  // ← НЕБЕЗОПАСНО!
```

---

#### 4. Race: _pendingExitReason во время pipeline (MEDIUM)

**Сценарий:**
```
1. ExecuteCycleAsync: _skipNextScan = false (сброшен в строке 82)
2. Pipeline выполняется...
3. PLC Reset приходит → _pendingExitReason = HardReset
4. Pipeline завершается
5. ExecuteCycleAsync проверяет _pendingExitReason (строка 92) → return HardReset
6. HandleCycleExit(HardReset) → CurrentBarcode = null ✓
```

**Вывод:** Этот путь корректно обрабатывается.

---

#### 5. CompletionResult.Cancelled путь (MEDIUM)

**Сценарий:**
```
1. Тест завершён → HandleTestCompletionAsync()
2. CompletionCoordinator.HandleTestCompletedAsync() возвращает:
   - CompletionResult.Cancelled (NOK сохранение отменено)
   - или CompletionResult.NokRepeatCancelled (ReworkDialog отменён)
3. Код в HandleTestCompletionAsync:
   return result switch
   {
       CompletionResult.Finished => CycleExitReason.TestCompleted,
       CompletionResult.RepeatRequested => CycleExitReason.RepeatRequested,
       CompletionResult.NokRepeatRequested => CycleExitReason.NokRepeatRequested,
       _ => _pendingExitReason ?? CycleExitReason.SoftReset,  ← DEFAULT = SoftReset!
   };
4. HandleCycleExit(SoftReset) → НИЧЕГО
5. CurrentBarcode остаётся!
```

**Код:**
```csharp
// MainLoop.cs:173-179
return result switch
{
    // ...
    _ => _pendingExitReason ?? CycleExitReason.SoftReset,  // DEFAULT CASE!
};
```

## Proposed Solution

### Fix 1: Безопасное использование CurrentBarcode при повторе

```csharp
// MainLoop.cs:34-38
if (_skipNextScan)
{
    var savedBarcode = CurrentBarcode;
    if (savedBarcode == null)
    {
        _skipNextScan = false;
        _executeFullPreparation = false;
        infra.Logger.LogWarning("CurrentBarcode был сброшен, переход к обычному сканированию");
        SetAcceptingInput(true);
        barcode = await WaitForBarcodeAsync(ct);
        SetAcceptingInput(false);
    }
    else
    {
        barcode = savedBarcode;
        infra.StatusReporter.UpdateScanStepStatus(Models.Steps.TestStepStatus.Success, "Повтор теста");
    }
}
```

### Fix 2: Сброс _skipNextScan при HardReset

```csharp
// cs:50-61 ClearStateOnReset()
private void ClearStateOnReset()
{
    state.BoilerState.Clear();
    state.PhaseState.Clear();
    ClearBarcode();
    _skipNextScan = false;           // ← ДОБАВИТЬ
    _executeFullPreparation = false; // ← ДОБАВИТЬ
    infra.ErrorService.IsHistoryEnabled = false;
    // ...
}
```

### Fix 3: Обработать CompletionResult.Cancelled явно

```csharp
// MainLoop.cs:173-179
return result switch
{
    CompletionResult.Finished => CycleExitReason.TestCompleted,
    CompletionResult.RepeatRequested => CycleExitReason.RepeatRequested,
    CompletionResult.NokRepeatRequested => CycleExitReason.NokRepeatRequested,
    CompletionResult.Cancelled => CycleExitReason.TestCompleted,  // ← ЯВНО ОБРАБОТАТЬ
    _ => _pendingExitReason ?? CycleExitReason.SoftReset,
};
```

### Fix 4: SoftReset должен очищать CurrentBarcode

Вопрос: нужно ли сбрасывать CurrentBarcode при SoftReset сразу, не дожидаясь AskEnd?

**Аргументы ЗА:**
- Гарантированная очистка даже если AskEnd не придёт

**Аргументы ПРОТИВ:**
- При SoftReset оператор мог просто остановить тест, и UI должен показывать последний штрихкод

**Решение:** Добавить сброс в `HandleCycleExit(SoftReset)` для консистентности:

```csharp
case CycleExitReason.SoftReset:
    ClearBarcode();  // ← ДОБАВИТЬ
    // Остальная очистка произойдёт по AskEnd в HandleGridClear
    break;
```

## Impact Analysis

| Изменение | Риск | Файлы |
|-----------|------|-------|
| Fix 1: null-safe barcode | Низкий | `PreExecutionCoordinator.MainLoop.cs` |
| Fix 2: reset flags | Низкий | `PreExecutionCoordinator.cs` |
| Fix 3: Cancelled handling | Низкий | `PreExecutionCoordinator.MainLoop.cs` |
| Fix 4: SoftReset cleanup | Средний | `PreExecutionCoordinator.MainLoop.cs` |

## Questions

1. **Подтвердите приоритет:** Какой из сценариев вы наблюдали?
   - SoftReset без AskEnd?
   - HardReset между повторами?
   - Отмена диалога сохранения?

2. **SoftReset поведение:** Хотите ли вы сбрасывать CurrentBarcode сразу при SoftReset, или только по AskEnd?
