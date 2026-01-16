# План: Перенос BlockBoilerAdapterStep в TestExecutionCoordinator

## Резюме
Создать `ITestStep` версию BlockBoilerAdapter, чтобы пользователь мог выбирать его в редакторе последовательностей. Шаг ставится в одну колонку (остальные пустые в этой строке), но если поставят в несколько — не сломается (операции идемпотентные).

---

## Текущая архитектура

### Два типа шагов
| Тип | Интерфейс | Контекст | Где выполняется |
|-----|-----------|----------|-----------------|
| Pre-execution | `IPreExecutionStep` | `PreExecutionContext` | `PreExecutionCoordinator` |
| Test | `ITestStep` | `TestStepContext` | `TestExecutionCoordinator` (4 колонки) |

### BlockBoilerAdapterStep сейчас
- **Файл:** `Services/Steps/Steps/BlockBoilerAdapterStep.cs`
- **Интерфейс:** `IPreExecutionStep` (не виден в редакторе)
- **Вызов:** `PreExecutionCoordinator.ExecuteBlockBoilerAdapterAsync()`
- **Критическая логика:**
  - `errorService.IsHistoryEnabled = true` — включает сбор истории
  - `boilerState.SetTestRunning(true)` — флаг теста
  - PLC: Start → ждёт End/Error

---

## План реализации

### Шаг 1: Создать BlockBoilerAdapterTestStep : ITestStep
**Файл:** `Services/Steps/Steps/BlockBoilerAdapterTestStep.cs`

```csharp
public class BlockBoilerAdapterTestStep(
    TagWaiter tagWaiter,
    ExecutionPhaseState phaseState,
    IErrorService errorService,
    BoilerState boilerState,
    DualLogger<BlockBoilerAdapterTestStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags
{
    // Те же PLC теги что и в оригинале
    // ExecuteAsync возвращает TestStepResult вместо PreExecutionResult
    // Использует TestStepContext вместо PreExecutionContext
}
```

### Шаг 2: Убрать вызов из PreExecutionCoordinator
**Файл:** `Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Pipeline.cs`

- Убрать `ExecuteBlockBoilerAdapterAsync()` из pipeline
- Убрать retry логику для BlockBoilerAdapter
- После ScanStep сразу `StartTestExecution()`

### Шаг 3: Убрать BlockBoilerAdapterStep из PreExecutionSteps
**Файл:** `Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionDependencies.cs`

- Убрать `BlockBoilerAdapterStep` из конструктора `PreExecutionSteps`
- Убрать property `BlockBoilerAdapter`

### Шаг 4: Перенести IsHistoryEnabled логику
**Проблема:** `IsHistoryEnabled = true` сейчас в BlockBoilerAdapterStep
**Решение:** Оставить в новом `BlockBoilerAdapterTestStep`

### Шаг 5: Обработка ошибок (уже работает)
Существующая система retry/skip через `ErrorCoordinator` автоматически сработает:
- При ошибке → `TestStepResult.Fail()`
- `ColumnExecutor.SetErrorState()` → `HasFailed = true`
- `TestExecutionCoordinator.HandleErrorsIfAny()` → показывает диалог → ждёт PLC сигнал
- При Retry → `RetryLastFailedStepAsync()` вызывает `ExecuteStepCoreAsync()` снова

**Ничего дополнительно реализовывать не нужно.**

---

## Файлы для изменения

| Файл | Действие |
|------|----------|
| `Services/Steps/Steps/BlockBoilerAdapterTestStep.cs` | **Создать** |
| `Services/Steps/Steps/BlockBoilerAdapterStep.cs` | Удалить или оставить для совместимости |
| `PreExecutionCoordinator.Pipeline.cs` | Убрать вызов BlockBoilerAdapter |
| `PreExecutionCoordinator.Retry.cs` | Убрать retry логику для BlockBoilerAdapter |
| `PreExecutionDependencies.cs` | Убрать из PreExecutionSteps |
| `StepsServiceExtensions.cs` | Возможно: убрать регистрацию старого шага |

---

## Идемпотентность (защита от дублирования)

Если пользователь поставит шаг в несколько колонок:
- `WriteAsync(Start, true)` × 4 — безопасно, та же операция
- `IsHistoryEnabled = true` × 4 — идемпотентно
- `SetTestRunning(true)` × 4 — идемпотентно
- `WaitAnyAsync()` × 4 — все завершатся когда PLC ответит

**Вывод:** Специальная защита не требуется.

---

## Проверка

1. Собрать проект: `dotnet build`
2. Открыть TestSequenceEditor — BlockBoilerAdapter должен быть в dropdown
3. Создать последовательность с BlockBoilerAdapter в первой строке
4. Запустить тест — шаг должен выполниться и включить IsHistoryEnabled

---

## Оценка сложности

**Простой рефакторинг.** Основная работа:
1. Создать новый класс (копия логики, другой return type)
2. Убрать 2-3 вызова из PreExecutionCoordinator
3. Retry/skip уже работает в TestExecutionCoordinator