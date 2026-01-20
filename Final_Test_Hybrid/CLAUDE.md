# CLAUDE.md

## Codex Review Protocol (ОБЯЗАТЕЛЬНО)

**ПЕРЕД изменениями:** `codex "Критически проанализируй план: [план]"` → правьте до консенсуса
**ПОСЛЕ изменений:** `git diff` → `codex "Проверь diff: [diff]"` → исправляйте до одобрения

## Project Overview

**Final_Test_Hybrid** — WinForms + Blazor (.NET 10) для промышленных тестов.
- **Stack:** Radzen Blazor, EPPlus, Serilog
- **Build:** `dotnet build && dotnet run`

## Code Philosophy

**Clean Code + Прагматизм.** Читаемость > краткость. DRY. Без магических чисел.

| НЕ нужно | Когда |
|----------|-------|
| `IDisposable`, блокировки | Singleton без конкуренции |
| `CancellationToken`, retry | Короткие операции (<2 сек) |
| null-проверки DI | Внутренний код |

**Проверки НУЖНЫ:** границы системы, внешний ввод, десериализация, P/Invoke.

## Coding Standards

- **Один** `if`/`for`/`while`/`switch`/`try`/`await` на метод (guard clauses OK)
- `var` везде, `{}` обязательны, **max 300 строк** → partial classes
- **PascalCase:** типы, методы | **camelCase:** локальные, параметры
- **Blazor:** CSS в `.razor.css`, `::deep` для Radzen, `IAsyncDisposable` для cleanup

## Key Patterns

### DualLogger (ОБЯЗАТЕЛЬНО)
```csharp
public class MyService(DualLogger<MyService> logger)
{
    logger.LogInformation("msg"); // → файл + UI теста
}
```

### Pausable vs Non-Pausable ([TagWaiterGuide.md](TagWaiterGuide.md))

| Контекст | Сервис |
|----------|--------|
| Тестовые шаги | `PausableOpcUaTagService`, `PausableTagWaiter` |
| Системные операции | `OpcUaTagService`, `TagWaiter` |

**В шагах:** `context.DelayAsync()`, `context.PauseToken.WaitWhilePausedAsync()`. НЕ вызывать `Pause()/Resume()`.

## Coordinators & State Management

### ErrorCoordinator ([ErrorCoordinatorGuide.md](ErrorCoordinatorGuide.md))

| Метод | Действие |
|-------|----------|
| `HandleInterruptAsync(reason)` | Делегирует в `IInterruptBehavior` |
| `Reset()` | Resume → OnReset |
| `ForceStop()` | Resume |

**Новый InterruptReason:** `XxxBehavior : IInterruptBehavior` → DI

### PlcReset ([PlcResetGuide.md](PlcResetGuide.md))

| Тип | Условие | Метод |
|-----|---------|-------|
| Мягкий | `wasInScanPhase = true` | `ForceStop()` |
| Жёсткий | `wasInScanPhase = false` | `Reset()` |

### CycleExitReason ([CycleExitGuide.md](CycleExitGuide.md))

| Состояние | Очистка |
|-----------|---------|
| `TestCompleted` | `ClearForTestCompletion()` — сразу |
| `SoftReset` | `ClearStateOnReset()` — по AskEnd |
| `HardReset` | `ClearStateOnReset()` + grid — сразу |
| `RepeatRequested` | `ClearForRepeat()` |
| `NokRepeatRequested` | `ClearForNokRepeat()` |
| `PipelineFailed/Cancelled` | Ничего |

### ErrorService — Очистка данных

| Момент | Действие |
|--------|----------|
| Завершение теста | `ClearForTestCompletion()` — grid, timing, recipes, BoilerState; `IsHistoryEnabled=false` |
| Готовность к сканированию | `ResetScanTiming()` |
| Начало нового теста | `ClearForNewTestStart()` — history, results; затем `IsHistoryEnabled=true` |

**История:** при включении — копирует активные ошибки; при выключении — закрывает открытые записи.

### Retry/Skip ([RetrySkipGuide.md](RetrySkipGuide.md))

| Действие | PLC → PC | PC → PLC |
|----------|----------|----------|
| Повтор | `Req_Repeat=true` | `AskRepeat=true`, ждёт `Error=false` |
| Пропуск | `End=true` | — (NOK) |

### Settings Blocking ([SettingsBlockingGuide.md](SettingsBlockingGuide.md))

| Сервис | Блокирует |
|--------|-----------|
| `SettingsAccessStateManager` | Тест не на scan step |
| `PlcResetCoordinator` | Сброс PLC |
| `ErrorCoordinator` | Активное прерывание |
| `PreExecutionCoordinator` | SwitchMes при pre-execution |

## Accepted Patterns (NOT bugs)

| Паттерн | Почему OK |
|---------|-----------|
| `ExecutionStateManager.State` без Lock | Atomic enum, stale read OK для UI |
| `?.TrySetResult()` без синхронизации | Идемпотентна |
| Fire-and-forget в singleton | `.ContinueWith` или внутренний try-catch |
| `TryStartInBackground()` | Исключения в `RunWithErrorHandlingAsync` |

## Safety Patterns

### Hang Protection

| Сценарий | Защита |
|----------|--------|
| Пустые Maps | `StartTestExecution()→false` + `RollbackTestStart()` → `PipelineFailed` |
| Двойной старт | `TryStartInBackground()→false`, состояние не меняется |
| Исключение в `OnSequenceCompleted` | `InvokeSequenceCompletedSafely()` — логирует, cleanup выполняется |

### TOCTOU Prevention

**Правило:** захватывай поле в локальную переменную перед `await` или в event handler.
```csharp
var step = _state.FailedStep;  // Захват
if (step != null) { await ExecuteStepCoreAsync(step, ct); }
```

### CancellationToken Sync

| Событие | Отменить |
|---------|----------|
| Reset + AutoMode OFF | `_loopCts`, `_currentCts` |
| ForceStop | `_currentCts`, `_cts` |
| Logout | `_loopCts` |

## Architecture

```
Program.cs → Form1.cs (DI) → BlazorWebView → Radzen UI

Excel → TestMapBuilder → TestMapResolver → TestMap
                                            ↓
                          TestExecutionCoordinator
                          ├── 4 × ColumnExecutor (parallel)
                          ├── ExecutionStateManager
                          └── ErrorCoordinator
```

## Step Execution Flow

```
[Barcode] → PreExecutionCoordinator → TestExecutionCoordinator → [OK/NOK]
                    │                           │
            ScanStep (10 steps)         4 × ColumnExecutor
            BlockBoilerAdapterStep      ExecuteMapOnAllColumns
                    │                           │
            StartTestExecution()        OnSequenceCompleted
            TryStartInBackground()→bool         │
                    │                   HandleTestCompleted()
            false → RollbackTestStart()
                    PipelineFailed
```

### ExecutionActivityTracker ([ExecutionActivityTrackerGuide.md](ExecutionActivityTrackerGuide.md))

| Свойство | Описание |
|----------|----------|
| `IsPreExecutionActive` | Фаза подготовки |
| `IsTestExecutionActive` | Фаза выполнения |
| `IsAnyActive` | Любая активность |

## Interfaces & DI

### Test Step Interfaces
```
ITestStep ← IRequiresPlcSubscriptions, IRequiresRecipes, IHasPlcBlock
IScanBarcodeStep, IPreExecutionStep (отдельные)
```

### DI Patterns

| Паттерн | Пример |
|---------|--------|
| Extension chain | `AddFinalTestServices()` → `AddOpcUaServices()` |
| Singleton state | `ExecutionStateManager`, `BoilerState` |
| Pausable decorator | `PausableOpcUaTagService` wraps `OpcUaTagService` |
| DbContextFactory | `AddDbContextFactory<AppDbContext>()` |

## OPC-UA Layer

| Сервис | Назначение |
|--------|------------|
| `OpcUaConnectionService` | Session, auto-reconnect |
| `OpcUaSubscription` | Pub/sub, callbacks |
| `OpcUaTagService` | Read/write (`ReadResult<T>`, `WriteResult`) |
| `TagWaiter` | Multi-tag conditions |

## File Locations

| Category | Path |
|----------|------|
| Entry | `Program.cs`, `Form1.cs` |
| Components | `Components/{Engineer,Main,Overview}/` |
| Services | `Services/{OpcUa,Steps,Database}/` |
| Models | `Models/{Steps,Errors,Database}/` |
