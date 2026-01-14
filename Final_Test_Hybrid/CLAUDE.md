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

## ErrorCoordinator — сигнализатор

Только снимает паузу и сигнализирует. Логику делают подписчики.

| Метод | Действия |
|-------|----------|
| `Reset()` | `_pauseToken.Resume()` → `OnReset` |
| `ForceStop()` | `_pauseToken.Resume()` |
| `ResumeExecution()` | `_pauseToken.Resume()` → `ClearConnectionErrors()` → `OnRecovered` |

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

ColumnExecutor ─────────┐
ScanErrorHandler ───────┼──► StepStatusReporter ──► TestSequenseService
BarcodeProcessingPipeline──┘

ScanStepManager
├── ScanModeController
├── ScanDialogCoordinator
├── ScanStateManager
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

## File Locations

| Category | Path |
|----------|------|
| Entry | `Program.cs`, `Form1.cs` |
| Root | `MyComponent.razor` |
| Components | `Components/Engineer/`, `Components/Overview/` |
| Services | `Services/Sequence/`, `Services/Database/` |
