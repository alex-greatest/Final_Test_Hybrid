# CLAUDE.md

Guidance for Claude Code when working with this repository.

> **См. также:** [ARCHITECTURE.md](ARCHITECTURE.md), [MessageServiceDescription.md](MessageServiceDescription.md), [ErrorSystemGuide.md](ErrorSystemGuide.md)

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

## Anti-Overengineering

**Контекст важнее паттернов.** Перед добавлением сложности — спроси "а нужно ли это здесь?"

### Singleton сервисы (живут всё время работы приложения)
- **НЕ нужен** `IDisposable` / `IAsyncDisposable` — процесс завершится, ОС освободит ресурсы
- **НЕ нужны** блокировки для thread-safety если нет реальной конкуренции
- **НЕ нужен** unsubscribe от событий других singleton'ов
- **НЕ нужна** защита от double-dispose

### Короткоживущие объекты
- **НЕ нужен** CancellationToken для операций < 2 сек
- **НЕ нужны** retry-политики для единичных операций
- **НЕ нужен** circuit breaker для локальных вызовов

### Когда блокировки НУЖНЫ
- Scoped/Transient сервисы с состоянием
- Доступ к shared mutable state из разных потоков
- Работа с внешними ресурсами (файлы, сеть, БД)

### Принцип
```
Сложность оправдана только если:
1. Есть реальная проблема (не гипотетическая)
2. Проблема воспроизводима
3. Решение проще проблемы
```

### Избегай защитного программирования

**Доверяй системе типов, DI-контейнеру и upstream-коду.**

```csharp
// ❌ ПЛОХО: параноидальные проверки
public void Process(IService service)
{
    if (service == null) throw new ArgumentNullException(nameof(service));
    var result = service.GetData();
    if (result is not ExpectedType typed) throw new InvalidCastException();
    // ...
}

// ✅ ХОРОШО: доверяем DI и типам
public void Process(IService service)
{
    var result = (ExpectedType)service.GetData();
    // ...
}
```

**НЕ проверяй:**
- `null` для DI-зависимостей — контейнер гарантирует
- `null` после `FirstOrDefault()` если коллекция гарантированно не пуста
- Типы после `is`/`as`/pattern matching — уже проверено
- Границы массива если индекс вычислен корректно
- Enum на валидность если значение из своего кода
- Строки на `IsNullOrEmpty` если источник гарантирует непустоту

**НЕ делай:**
- Defensive copy коллекций внутри класса
- `?.` (null-conditional) когда null невозможен
- `?? throw` когда левая часть не может быть null
- Try-catch вокруг кода который не бросает исключений
- Валидацию параметров в private методах

**Где проверки НУЖНЫ:**
- Границы системы (public API, HTTP endpoints)
- Внешний ввод (пользователь, файлы, сеть)
- Десериализация (JSON, XML, БД)
- P/Invoke и unmanaged код

**Принцип:** Код внутри модуля доверяет другому коду внутри модуля. Валидация — на границах.

## Coding Standards

### Method Complexity (один на метод)
- Один `if` / `for` / `while` / `switch` / `try` / `await` на метод
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

## Test Step Interfaces

Шаги реализуют `ITestStep` и опционально дополнительные интерфейсы:

| Интерфейс | Путь | Назначение |
|-----------|------|------------|
| `ITestStep` | `Interfaces/Test/` | Базовый: Id, Name, Description, ExecuteAsync |
| `IRequiresPlcSubscriptions` | `Interfaces/Plc/` | Требует PLC подписки (RequiredPlcTags) |
| `IRequiresRecipes` | `Interfaces/Recipe/` | Требует рецепты |
| `IHasPlcBlock` | `Interfaces/Plc/` | Имеет PLC блок (PlcBlockPath для Selected/Error/End) |
| `IScanBarcodeStep` | `Interfaces/` | Обработка баркодов |
| `IPreExecutionStep` | `Interfaces/PreExecution/` | Пред-выполнение |

### Иерархия интерфейсов
```
ITestStep (базовый, обязательный)
├── IRequiresPlcSubscriptions : ITestStep
├── IRequiresRecipes : ITestStep
└── IHasPlcBlock : ITestStep

IScanBarcodeStep (отдельный)
IPreExecutionStep (отдельный)
```

### Пример реализации шага
```csharp
public class BoilerAdapterStep : ITestStep, IRequiresPlcSubscriptions, IHasPlcBlock
{
    public string Id => "boiler_adapter";
    public string Name => "Boiler Adapter";
    public string Description => "...";

    // IRequiresPlcSubscriptions
    public IReadOnlyList<string> RequiredPlcTags => ["ns=3;s=..."];

    // IHasPlcBlock
    public string PlcBlockPath => "DB_VI.Block_Boiler_Adapter";

    public async Task<TestStepResult> ExecuteAsync(...) { ... }
}
```

## WaitGroupBuilder для PLC сигналов

Используй `WaitGroupBuilder` когда нужно ждать один из нескольких сигналов ("первый побеждает"):

```csharp
var result = await tagWaiter.WaitAnyAsync(
    tagWaiter.CreateWaitGroup<ErrorResolution>()
        .WaitForTrue(BaseTags.ErrorRetry, () => ErrorResolution.Retry)
        .WaitForTrue(BaseTags.ErrorSkip, () => ErrorResolution.Skip),
    ct);
```

Преимущества:
- Автоматическая отписка после срабатывания
- Встроенный timeout (`.WithTimeout()`)
- Чистый async/await вместо event-driven

## File Locations

| Category | Path |
|----------|------|
| Entry | `Program.cs`, `Form1.cs` |
| Root | `MyComponent.razor` |
| Components | `Components/Engineer/`, `Components/Overview/` |
| Services | `Services/Sequence/`, `Services/Database/` |
| Config | `appsettings.json` |
| Styles | `wwwroot/css/` |
