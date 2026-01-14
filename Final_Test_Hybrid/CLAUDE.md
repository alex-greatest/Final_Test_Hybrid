# CLAUDE.md

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

| Контекст | Сервис |
|----------|--------|
| Тестовые шаги | `PausableOpcUaTagService`, `PausableTagWaiter` |
| Системные операции | `OpcUaTagService`, `TagWaiter` |

## ErrorCoordinator — Strategy Pattern

Координатор прерываний с расширяемой архитектурой. См. [ErrorCoordinatorGuide.md](ErrorCoordinatorGuide.md)

| Метод | Действия |
|-------|----------|
| `HandleInterruptAsync(reason)` | Делегирует в `IInterruptBehavior` |
| `Reset()` | `_state.PauseToken.Resume()` → `OnReset` |
| `ForceStop()` | `_state.PauseToken.Resume()` |

**Добавить новый InterruptReason:** создать класс `XxxBehavior : IInterruptBehavior` → зарегистрировать в DI.

**Подписчики OnReset:** TestExecutionCoordinator, ReworkDialogService, PreExecutionCoordinator.Retry

## Accepted Patterns (NOT bugs)

| Паттерн | Почему OK |
|---------|-----------|
| `ExecutionStateManager.State` без Lock | Enum assignment atomic, stale read допустим для UI |
| `_disposed` volatile в ResetSubscription | Visibility гарантирована |
| `?.TrySetResult()` без синхронизации | Идемпотентна |
| Fire-and-forget в singleton | С `.ContinueWith` для ошибок |

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
