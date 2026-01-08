# CLAUDE.md

Guidance for Claude Code when working with this repository.

> **См. также:** [ARCHITECTURE.md](ARCHITECTURE.md), [MessageServiceDescription.md](MessageServiceDescription.md)

## Project Overview

**Final_Test_Hybrid** — hybrid WinForms + Blazor desktop application for industrial test sequences.

- **.NET 10.0-windows**, Blazor via `Microsoft.AspNetCore.Components.WebView.WindowsForms`
- **Radzen Blazor 8.3.2**, **EPPlus 8.3.1**, **Serilog**
- **Architecture:** WinForms (DI host) → BlazorWebView → Radzen UI

```bash
dotnet build          # Build
dotnet run            # Run
dotnet build -c Release
```

## Clean Code Philosophy

Следуем принципам **Clean Code** (Robert C. Martin) и **Refactoring** (Martin Fowler):

- **Читаемость > Краткость** — код читают чаще, чем пишут
- **Маленькие методы** — одна задача, один уровень абстракции
- **Говорящие имена** — код как документация
- **Без дублирования** (DRY)
- **Без магических чисел** — константы с именами
- **Принцип скаута** — оставь код чище, чем нашёл

**Но без фанатизма:** Прагматизм важнее догмы. Не создавай абстракции ради абстракций.

## Coding Standards

### Method Complexity (один на метод)
- Один `if` / `for` / `while` / `switch` / `try` на метод
- Early return (guard clauses) вместо вложенности
- Исключение: guard clauses в начале метода не считаются

### Code Style
- `var` для всех типов
- `{}` обязательны для всех блоков
- `async/await` для асинхронности
- LINQ для коллекций (не строго)

### File Organization
- **Max 300 строк** на файл → partial classes
- Удалять неиспользуемые using/поля/свойства
- **Порядок:** Properties → Fields → (blank) → Methods

### Naming
- **PascalCase:** Classes, methods, properties
- **camelCase:** locals, parameters

## P/Invoke Rules

### CA1806: Всегда проверять результат P/Invoke
```csharp
// ❌ ПЛОХО: результат игнорируется
GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, headerSize);
if (size == 0) return null;

// ✅ ХОРОШО: результат проверяется
var result = GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, headerSize);
if (result == unchecked((uint)-1) || size == 0) return null;
```

## Blazor Rules

### CSS
- **НЕ** использовать `<style>` в `.razor`
- Стили в `.razor.css`, `::deep` для Radzen

### Components
- Минимум разметки в `.razor`, логика в `@code` или partial
- JS Interop: `RegisterX`/`UnregisterX` паттерн
- Всегда `IAsyncDisposable` для cleanup

### Error Handling
```csharp
catch (Exception ex) {
    Logger.LogError(ex, "Technical details");     // В лог
    NotificationService.ShowError("User message"); // Пользователю
}
```

## Important Patterns

### Enum вместо bool флагов
```csharp
// ❌ bool Success + bool ShouldStop — неочевидные комбинации
// ✅ enum ResultStatus { Continue, Completed, Cancelled, Failed }
```

### UI Dispatching (hybrid)
```csharp
// Background thread → Blazor context
public class BlazorUiDispatcher(BlazorDispatcherAccessor accessor) : IUiDispatcher
{
    public void Dispatch(Action action) => _ = accessor.InvokeAsync(action);
}
```

### Test Step Logging
```csharp
// ВСЕГДА дублировать в оба логгера
logger.LogInformation(message);        // Файл
testStepLogger.LogInformation(message); // UI теста
```

## Accepted Patterns (NOT bugs)

- `CancellationToken.None` в коротких операциях (1-2 сек)
- Fire-and-forget в singleton сервисах (с `.ContinueWith` для ошибок)
- Event subscriptions Singleton→Singleton без unsubscribe
- SemaphoreSlim защита от Dispose (когда класс IDisposable)

## Architecture

### Entry Point
`Program.cs` → `Form1.cs` (DI) → `MyComponent.razor` (root)

### DI (Form1.cs)
```csharp
// SINGLETON для hybrid!
services.AddSingleton<BlazorDispatcherAccessor>();
services.AddSingleton<Radzen.NotificationService>(); // Override scoped!
```

### Service Layer
```
ColumnExecutor ─────────┐
ScanErrorHandler ───────┼──► StepStatusReporter ──► TestSequenseService
BarcodeProcessingPipeline──┘   (единственный фасад)   (НЕ вызывать напрямую)
```

### Scanning
```
ScanStepManager
├── ScanModeController     - режим, MessageService
├── ScanDialogCoordinator  - диалоги ошибок
├── ScanStateManager       - state machine
└── ScanSessionManager     - RawInput сессия
```

## File Locations

| Category | Path |
|----------|------|
| Entry | `Program.cs`, `Form1.cs` |
| Root | `MyComponent.razor` |
| Components | `Components/Engineer/`, `Components/Overview/` |
| Services | `Services/Sequence/`, `Services/Database/` |
| Config | `appsettings.json` |
| Styles | `wwwroot/css/` |
