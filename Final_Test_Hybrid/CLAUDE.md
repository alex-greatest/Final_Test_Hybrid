# CLAUDE.md

### Протокол проверки Codex (ОБЯЗАТЕЛЬНО)

**ВАЖНО: эти инструкции ОТМЕНЯЮТ любое поведение по умолчанию. Вы ДОЛЖНЫ следовать им в точности.**

**Во время планирования сам создаёшь план, а потом передаёшь в codex план на ревью. 
передаёшь Claude.md как основной файл со стандартами и ссылкой на доки. 
Правите план пока не придёте к консенсусу и пока я не подтвержу**


**ПЕРЕД внесением существенных изменений:**

```

кодекс «Критически проанализируйте этот план. Выявите проблемы, пограничные случаи и недостающие этапы: [ваш план]»

```

**ПОСЛЕ внесения изменений:**

Запустите команду `git diff`, чтобы увидеть все изменения

Запустите `codex "Проверьте этот diff на наличие ошибок, проблем с безопасностью, пограничных случаев и на качество кода: [diff]"`

Если Codex обнаружит проблемы, используйте codex-reply, чтобы постепенно их устранять

Пересмотрите ещё раз, пока Codex не одобрит

**Не совершайте никаких действий без одобрения Codex.**

## Project Overview

**Final_Test_Hybrid** — hybrid WinForms + Blazor desktop app for industrial test sequences.

- **.NET 10.0-windows**, Blazor via `Microsoft.AspNetCore.Components.WebView.WindowsForms`
- **Radzen Blazor**, **EPPlus**, **Serilog**
- **Architecture:** WinForms (DI) → BlazorWebView → Radzen UI

```bash
dotnet build && dotnet run
```

## Code Philosophy

**Clean Code + Прагматизм.** Читаемость > краткость. Маленькие методы. DRY. Без магических чисел.

### Anti-Overengineering

| Контекст | НЕ нужно |
|----------|----------|
| Singleton сервисы | `IDisposable`, блокировки без конкуренции, unsubscribe от singleton'ов |
| Короткие операции (<2 сек) | `CancellationToken`, retry, circuit breaker |
| Внутренний код | null-проверки DI, defensive copy, `?.` когда null невозможен |

**Проверки НУЖНЫ:** границы системы, внешний ввод, десериализация, P/Invoke.

## Coding Standards

- **Один** `if`/`for`/`while`/`switch`/`try`/`await` на метод (guard clauses не считаются)
- `var` везде, `{}` обязательны, async/await
- **Max 300 строк** → partial classes
- **PascalCase:** типы, методы | **camelCase:** локальные, параметры

## Blazor Rules

- CSS в `.razor.css`, `::deep` для Radzen (не `<style>` в .razor)
- `IAsyncDisposable` для cleanup
- Error: `Logger.LogError(ex, "details")` + `NotificationService.ShowError("message")`

## Key Patterns

### DualLogger (ОБЯЗАТЕЛЬНО)
```csharp
public class MyService(DualLogger<MyService> logger)
{
    logger.LogInformation("msg"); // → файл + UI теста
}
```

### UI Dispatching
```csharp
public class BlazorUiDispatcher(BlazorDispatcherAccessor a) : IUiDispatcher
{
    public void Dispatch(Action action) => _ = a.InvokeAsync(action);
}
```

### Pausable vs Non-Pausable

См. [TagWaiterGuide.md](TagWaiterGuide.md) для подробностей.

| Контекст | Сервис |
|----------|--------|
| Тестовые шаги | `PausableOpcUaTagService`, `PausableTagWaiter` |
| Системные операции | `OpcUaTagService`, `TagWaiter` |

### Pausable операции в шагах

| Операция | Метод |
|----------|-------|
| Задержка с паузой | `await context.DelayAsync(TimeSpan.FromSeconds(5), ct)` |
| Ручная проверка паузы | `await context.PauseToken.WaitWhilePausedAsync(ct)` |

**ВАЖНО:** Не вызывать `PauseToken.Pause()` / `Resume()` в шагах — это делает `ErrorCoordinator`.

## ErrorCoordinator — Strategy Pattern

Координатор прерываний с расширяемой архитектурой. См. [ErrorCoordinatorGuide.md](ErrorCoordinatorGuide.md)

| Метод | Действия |
|-------|----------|
| `HandleInterruptAsync(reason)` | Делегирует в `IInterruptBehavior` |
| `Reset()` | `_state.PauseToken.Resume()` → `OnReset` |
| `ForceStop()` | `_state.PauseToken.Resume()` |

**Добавить новый InterruptReason:** создать класс `XxxBehavior : IInterruptBehavior` → зарегистрировать в DI.

**Подписчики OnReset:** TestExecutionCoordinator, ReworkDialogService, PreExecutionCoordinator.Retry

## PlcReset — Логика сброса

Сброс по сигналу PLC с очисткой состояния. См. [PlcResetGuide.md](PlcResetGuide.md)

| Тип сброса | Условие | Метод |
|------------|---------|-------|
| Мягкий | `wasInScanPhase = true` | `ForceStop()` |
| Жёсткий | `wasInScanPhase = false` | `Reset()` |

**Очистка BoilerState:** гарантирована через `HandleCycleExit()` в PreExecutionCoordinator.

## CycleExitReason — Состояния выхода из цикла

Явное управление очисткой состояния. См. [CycleExitGuide.md](CycleExitGuide.md)

```csharp
enum CycleExitReason { PipelineFailed, PipelineCancelled, TestCompleted, SoftReset, HardReset, RepeatRequested, NokRepeatRequested }
```

| Состояние | Очистка | Когда |
|-----------|---------|-------|
| `TestCompleted` | `ClearForTestCompletion()` | Сразу (результаты сохраняются для оператора) |
| `SoftReset` | `ClearStateOnReset()` | По AskEnd (синхронно с гридом) |
| `HardReset` | `ClearStateOnReset()` + grid | Сразу |
| `RepeatRequested` | `ClearForRepeat()` | OK повтор |
| `NokRepeatRequested` | `ClearForNokRepeat()` | NOK повтор с подготовкой |
| `PipelineFailed/Cancelled` | Ничего | — |

**Добавить новое состояние:** enum → `HandleCycleExit` case → источник сигнала.

## ErrorService и очистка данных — Двухфазная модель

### Фаза 1: Завершение теста (`ClearForTestCompletion`)
При завершении теста (OK/NOK) очищаются:
- Грид шагов (ClearAllExceptScan)
- Время шагов (StepTimingService.Clear)
- Рецепты (RecipeProvider.Clear)
- BoilerState
- IsHistoryEnabled = false (но **НЕ** история!)

**НЕ чистятся:** История ошибок и Результаты — оператор должен их видеть до следующего теста.

### Фаза 2: Готовность к новому циклу (`SetAcceptingInput(true)`)
- ResetScanTiming() — таймер сканирования сбрасывается и запускается

### Фаза 3: Начало нового теста (`ClearForNewTestStart`)
Перед включением IsHistoryEnabled:
- ClearHistory() — очистка истории ошибок
- TestResultsService.Clear() — очистка результатов

### Таблица моментов

| Момент | Действие | Где |
|--------|----------|-----|
| Завершение теста | `ClearForTestCompletion()` | `HandleCycleExit(TestCompleted)` |
| Готовность к сканированию | `ResetScanTiming()` | `SetAcceptingInput(true)` |
| Перед включением истории | `ClearForNewTestStart()` | `ExecutePreExecutionPipelineAsync`, `ExecuteRepeatPipelineAsync` |
| После успешного ScanStep | `IsHistoryEnabled = true` | `PreExecutionCoordinator.Pipeline` |
| При сбросе PLC | `IsHistoryEnabled = false` | `ClearStateOnReset`, `ClearForTestCompletion`, `ClearForRepeat` |

**При включении истории:** все текущие активные ошибки автоматически копируются в историю. Это гарантирует корректное закрытие записей для ошибок, возникших ДО сканирования.

## Retry/Skip — Логика повтора и пропуска шагов

Обработка ошибок шагов с сигналами PLC. См. [RetrySkipGuide.md](RetrySkipGuide.md)

| Действие | PLC сигнал | PC → PLC |
|----------|------------|----------|
| Повтор | `Req_Repeat = true` | `AskRepeat = true`, ждёт `Block.Error = false` |
| Пропуск | `End = true` | Ничего, переход к следующему шагу (NOK) |

## Settings Blocking — Блокировка настроек

Галочки в панели Engineer блокируются во время операций. См. [SettingsBlockingGuide.md](SettingsBlockingGuide.md)

| Сервис | Блокирует |
|--------|-----------|
| `SettingsAccessStateManager` | Когда тест НЕ на scan step |
| `PlcResetCoordinator` | Во время сброса PLC |
| `ErrorCoordinator` | При активном прерывании |
| `PreExecutionCoordinator` | Только SwitchMes, при pre-execution |

## Accepted Patterns (NOT bugs)

| Паттерн | Почему OK |
|---------|-----------|
| `ExecutionStateManager.State` без Lock | Enum assignment atomic, stale read допустим для UI |
| `_disposed` volatile в ResetSubscription | Visibility гарантирована |
| `?.TrySetResult()` без синхронизации | Идемпотентна |
| Fire-and-forget в singleton | С `.ContinueWith` для ошибок |

## Race Condition Prevention — TOCTOU Pattern

При работе с полями класса между `await` или в event handlers — **захватывай в локальную переменную**.

### Проблема (TOCTOU — Time-of-Check-Time-of-Use)
```csharp
// ПЛОХО: поле может измениться между проверкой и использованием
if (_state.FailedStep != null)
{
    RestartFailedStep();  // Может обнулить _state.FailedStep!
    await ExecuteStepCoreAsync(_state.FailedStep, ct);  // NullReferenceException
}
```

### Решение
```csharp
// ХОРОШО: захватить в локальную переменную
var step = _state.FailedStep;
if (step != null)
{
    RestartFailedStep();
    await ExecuteStepCoreAsync(step, ct);  // Безопасно
}
```

### Когда применять

| Ситуация | Паттерн |
|----------|---------|
| Поле читается перед `await` и после | Захват в локальную переменную |
| Поле в event handler | Захват в начале метода |
| `CancellationTokenSource` | Захват перед использованием `.Token` |
| `TaskCompletionSource` | `Interlocked.Exchange` при присваивании |
| Property с side effects | Один вызов, результат в переменную |

### Примеры из кодовой базы

- `ColumnExecutor.RetryLastFailedStepAsync` — захват `step`
- `TestExecutionCoordinator.HandleErrorsIfAny` — захват `_cts`
- `ErrorCoordinator.HandleConnectionChanged` — захват `IsAnyActive`
- `ScanModeController.IsInScanningPhase` — `IsInScanningPhaseUnsafe` для внутренних вызовов

## CancellationToken Synchronization Pattern

При сбросе системы все связанные CancellationTokenSource должны быть отменены синхронно.

### Проблема

Система имеет несколько независимых CTS:
- `_loopCts` в ScanModeController (цикл ввода)
- `_currentCts` в PreExecutionCoordinator (текущий цикл)
- `_cts` в TestExecutionCoordinator (выполнение тестов)

При сбросе легко забыть отменить один из них, что приводит к рассинхронизации UI.

### Пример бага (исправлен)

`TransitionToReadyInternal` не отменял `_loopCts` когда `!IsScanModeEnabled`:
- Сканер отключался ✓
- Но цикл ввода продолжал работать → поле ввода оставалось доступным ✗

### Правило

**При изменении состояния готовности системы — проверь ВСЕ связанные CTS:**

| Событие | Что отменить |
|---------|--------------|
| Reset + AutoMode OFF | `_loopCts`, `_currentCts` |
| ForceStop | `_currentCts`, `_cts` |
| Logout | `_loopCts` |

### Чеклист для review

При добавлении нового CTS или изменении логики сброса:
1. [ ] Где создаётся CTS?
2. [ ] Где отменяется при нормальном завершении?
3. [ ] Где отменяется при сбросе?
4. [ ] Синхронизирован ли с другими CTS?

## Architecture

```
Program.cs → Form1.cs (DI) → MyComponent.razor

Excel → TestMapBuilder → TestMapResolver → TestMap
                                            ↓
                          TestExecutionCoordinator
                          ├── 4 × ColumnExecutor (parallel)
                          ├── ExecutionStateManager (error queue)
                          └── ErrorCoordinator (interrupts)

ScanStepManager
├── ScanModeController
├── ScanDialogCoordinator
└── ScanSessionManager
```

## Step Execution System — Полный Flow

Система выполнения тестов состоит из двух фаз: **PreExecution** (подготовка) и **TestExecution** (параллельное выполнение).

### Общая схема

```
[Сканирование штрихкода]
        ↓
┌─────────────────────────────────────┐
│ PreExecutionCoordinator             │
│ ├─ ScanStep (подготовка данных)     │
│ └─ BlockBoilerAdapterStep (PLC)     │
└─────────────────────────────────────┘
        ↓ StartTestExecution()
┌─────────────────────────────────────┐
│ TestExecutionCoordinator            │
│ └─ 4 × ColumnExecutor (parallel)    │
│    └─ TestMap → TestMapRow[4]       │
└─────────────────────────────────────┘
        ↓ OnSequenceCompleted
[Результат: OK/NOK]
```

### Фаза 1: PreExecution

**Файлы:** `Services/Steps/Infrastructure/Execution/PreExecution/`

```
PreExecutionCoordinator.StartMainLoopAsync()
└─ while (!ct.IsCancellationRequested)
   └─ RunSingleCycleAsync()
      ├─ SetAcceptingInput(true)      // UI: поле ввода активно
      ├─ WaitForBarcodeAsync()        // Блокируется до сканирования
      ├─ SetAcceptingInput(false)
      └─ ExecuteCycleAsync(barcode)
         ├─ ScanStep.ExecuteAsync()   // 10 шагов подготовки
         ├─ BlockBoilerAdapterStep    // Блокировка адаптера
         └─ StartTestExecution()      // Fire-and-forget
```

**ScanStep** (10 шагов):
1. `ValidateBarcode()` → проверка формата
2. `LoadBoilerDataAsync()` → тип котла из БД/MES
3. `LoadTestSequenceAsync()` → последовательность тестов
4. `BuildTestMaps()` → матрица шагов
5. `SaveBoilerState()` → сохранение в singleton
6. `ResolveTestMaps()` → резолвинг шагов по именам
7. `ValidateRecipes()` → проверка рецептов
8. `InitializeDatabaseAsync()` → записи в БД
9. `WriteRecipesToPlcAsync()` → запись рецептов в PLC
10. `InitializeRecipeProvider()` → подготовка провайдера

**BlockBoilerAdapterStep:**
```csharp
WriteAsync(StartTag, true)           // Сигнал PLC
WaitAnyAsync([EndTag, ErrorTag])     // Ждём ответ PLC
├─ EndTag → Continue()               // Успех, запуск теста
└─ ErrorTag → FailRetryable()        // Ошибка с возможностью повтора
```

### Фаза 2: TestExecution

**Файлы:** `Services/Steps/Infrastructure/Execution/Coordinator/`

```
TestExecutionCoordinator.StartAsync()
├─ BeginExecution()
│  ├─ TransitionTo(Running)
│  └─ SetTestExecutionActive(true)
├─ RunAllMaps()
│  └─ for each TestMap:
│     └─ ExecuteMapOnAllColumns()
│        ├─ executor[0].ExecuteMapAsync() ─┐
│        ├─ executor[1].ExecuteMapAsync() ─┼─ Task.WhenAll()
│        ├─ executor[2].ExecuteMapAsync() ─┤
│        ├─ executor[3].ExecuteMapAsync() ─┘
│        └─ HandleErrorsIfAny()        // После каждого Map
└─ Complete()
   └─ OnSequenceCompleted?.Invoke()
```

**ColumnExecutor** — выполнение одной колонки:
```csharp
ExecuteMapAsync(map, ct)
└─ foreach row in map.Rows:
   └─ step = row.Steps[ColumnIndex]  // Берёт шаг для своей колонки
   └─ ExecuteStepCoreAsync(step, ct)
      ├─ step.ExecuteAsync()
      ├─ Success → SetSuccessState()
      └─ Error → SetErrorState() → EnqueueError()
```

### Обработка ошибок

```
ColumnExecutor.SetErrorState()
        ↓ OnStateChanged
TestExecutionCoordinator.EnqueueFailedExecutors()
        ↓
ExecutionStateManager.EnqueueError()
        ↓ Task.WhenAll завершился
HandleErrorsIfAny()
├─ TransitionTo(PausedOnError)
├─ OnErrorOccurred?.Invoke(error)    // UI показывает диалог
├─ WaitForResolutionAsync()          // Ждём PLC сигнал
│  ├─ ErrorRetry → Retry
│  └─ ErrorSkip → Skip
├─ Retry: RetryLastFailedStepAsync()
└─ Skip: ClearFailedState() + DequeueError()
```

### Синхронизация между Coordinators

```
PreExecutionCoordinator                    TestExecutionCoordinator
        │                                          │
        │ StartTestExecution()                     │
        ├────── SetMaps(context.Maps) ────────────►│
        │                                          │
        │ fire-and-forget                          │
        ├────── StartAsync() ─────────────────────►│
        │                                          ├─ RunAllMaps()
        │                                          │
        │ await testCompletionTcs.Task             │
        │◄─────── OnSequenceCompleted ─────────────┤ Complete()
        │                                          │
        ▼                                          │
HandleTestCompleted()
└─ SetTestResult(1 or 2)
```

### Данные между фазами

| Данные | Источник | Назначение |
|--------|----------|------------|
| `BoilerState` | ScanStep | Singleton, shared |
| `TestMap[]` | ScanStep → `context.Maps` | `SetMaps()` → `_maps` |
| `Recipes` | ScanStep | `RecipeProvider` |
| `TestResult` | `HandleTestCompleted()` | `BoilerState.SetTestResult()` |

### ExecutionActivityTracker

Отслеживает активные фазы для UI и блокировок. См. [ExecutionActivityTrackerGuide.md](ExecutionActivityTrackerGuide.md)

| Свойство | Описание |
|----------|----------|
| `IsPreExecutionActive` | Фаза подготовки |
| `IsTestExecutionActive` | Фаза выполнения |
| `IsAnyActive` | Любая активность |

## Test Step Interfaces

```
ITestStep (базовый)
├── IRequiresPlcSubscriptions : ITestStep
├── IRequiresRecipes : ITestStep
└── IHasPlcBlock : ITestStep

IScanBarcodeStep (отдельный)
IPreExecutionStep (отдельный)
```

## DI Patterns

| Паттерн | Пример |
|---------|--------|
| Extension chain | `AddFinalTestServices()` → `AddOpcUaServices()` → `AddStepsServices()` |
| Singleton state | `ExecutionStateManager`, `BoilerState`, `OrderState` |
| Pausable decorator | `PausableOpcUaTagService` wraps `OpcUaTagService` + `PauseTokenSource` |
| DbContextFactory | `AddDbContextFactory<AppDbContext>()` для scoped доступа |

## OPC-UA Layer

| Сервис | Назначение |
|--------|------------|
| `OpcUaConnectionService` | Session lifecycle, auto-reconnect |
| `OpcUaSubscription` | Pub/sub broker, callback registry |
| `OpcUaTagService` | Read/write API (`ReadResult<T>`, `WriteResult`) |
| `TagWaiter` | WaitGroup builder для multi-tag conditions |

## Component Organization

| Папка | Содержимое |
|-------|------------|
| `Engineer/` | Sequence editor, Stand DB, Auth QR |
| `Main/` | Test flow, Parameter display, Modals |
| `Overview/` | Indicators, gauges (read-only) |
| `Errors/`, `Results/`, `Logs/` | Специализированные UI |

**Code-behind:** `.razor.cs` только если логика >50 строк или нужен `IAsyncDisposable`.

## File Locations

| Category | Path |
|----------|------|
| Entry | `Program.cs`, `Form1.cs` |
| Root | `MyComponent.razor` |
| Components | `Components/Engineer/`, `Components/Main/`, `Components/Overview/` |
| Services | `Services/OpcUa/`, `Services/Steps/`, `Services/Database/` |
| Models | `Models/Steps/`, `Models/Errors/`, `Models/Database/` |
| DI | `Services/DependencyInjection/` |
