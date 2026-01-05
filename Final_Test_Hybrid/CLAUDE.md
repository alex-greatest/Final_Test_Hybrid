# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Final_Test_Hybrid** is a hybrid WinForms + Blazor desktop application for managing industrial test sequences.

> **См. также:**
> - [ARCHITECTURE.md](ARCHITECTURE.md) — детальная архитектура системы выполнения тестов (PreExecution, TestExecution, передача данных между шагами)
> - [MessageServiceDescription.md](MessageServiceDescription.md) — система управления сообщениями (приоритеты, состояния, прерывания)

It runs on .NET 10 with a Windows Forms host containing a BlazorWebView control that renders the UI using Radzen Blazor components.

**Architecture:** WinForms acts as the container and dependency injection host, while Blazor components provide the interactive UI. This hybrid approach combines desktop stability with modern web UI patterns.

## Key Technologies

- **.NET 10.0-windows** with Windows Forms hosting
- **Blazor** (component-based UI framework) via `Microsoft.AspNetCore.Components.WebView.WindowsForms`
- **Radzen Blazor 8.3.2** (UI component library)
- **EPPlus 8.3.1** (Excel file I/O for test sequences)
- **OPCFoundation.NetStandard.Opc.Ua.Client 1.5.377.22** (planned OPC UA integration, currently commented out)
- **Serilog** (structured logging to file)

## Build and Run Commands

```bash
# Build the project
dotnet build

# Run the application
dotnet run

# Build for release
dotnet build -c Release

# Clean build artifacts
dotnet clean
```

## Architecture and Structure

### Application Entry Point

**Flow:** `Program.cs` → `Form1.cs` (WinForms host) → `MyComponent.razor` (Blazor root)

- `Program.cs` - Standard WinForms application entry point
- `Form1.cs` - Initializes BlazorWebView, configures DI container, loads `appsettings.json`, registers services
- `MyComponent.razor` - Root Blazor component containing tabbed interface (947KB - main application container)

### Dependency Injection Setup (Form1.cs)

**Ключевые сервисы:**
```csharp
// UI Dispatching (SINGLETON - важно для hybrid!)
services.AddSingleton<BlazorDispatcherAccessor>();
services.AddSingleton<IUiDispatcher, BlazorUiDispatcher>();
services.AddSingleton<INotificationService, NotificationServiceWrapper>();
services.AddSingleton<Radzen.NotificationService>();  // Override Radzen's scoped!

// Scoped сервисы
services.AddScoped<IFilePickerService, WinFormsFilePickerService>();
services.AddScoped<ISequenceExcelService, SequenceExcelService>();
services.AddScoped<TestSequenceService>();

// Radzen components (scoped by default)
services.AddRadzenComponents();
```

**⚠️ ВАЖНО:** В hybrid-приложении `Radzen.NotificationService` должен быть **Singleton**, иначе singleton-сервисы (ScanErrorHandler) получат другой экземпляр, чем UI компоненты.

### Component Organization

**Blazor Components:**
- `Components/Base/` - Base classes like `GridInplaceEditorBase<TItem>` for reusable editor patterns
- `Components/Engineer/` - Engineering UI (MainEngineering menu, test sequence editor)
- `Components/Engineer/Sequence/` - Test sequence editor with partial classes for code organization
- `Components/Overview/` - Dashboard indicators and panels (LampIndicator, ValueIndicator, PanelBox)

**Partial Class Pattern (TestSequenceEditor):**
```
TestSequenceEditor.razor           # UI markup
TestSequenceEditor.razor.cs        # Component lifecycle, properties, initialization
TestSequenceEditor.Grid.cs         # Grid rendering and animations
TestSequenceEditor.File.cs         # File creation and opening operations
TestSequenceEditor.Save.cs         # Save operations
TestSequenceEditor.Actions.cs      # Context menu and row actions
```

**StandDatabase Components (`Components/Engineer/StandDatabase/`):**
```
StandDatabaseDialog.razor          # Главный диалог с табами
BoilerTypesGrid.razor              # Типы котлов (CRUD)
Recipe/RecipesGrid.razor           # Рецепты (CRUD + Copy)
Recipe/RecipesGrid.Copy.cs         # Логика копирования рецептов
ResultSettings/ResultSettingsTab   # Настройки результатов (3 вкладки + Copy)
StepFinalTestsGrid.razor           # Шаги финального теста
ErrorSettingsTemplatesGrid.razor   # Шаблоны ошибок
Modals/                            # Диалоги копирования и ошибок
```

### Service Layer

**Core Services:**
- `TestSequenceService` - Manages test sequence data, row operations, file I/O coordination, path configuration
- `SequenceExcelService` - Low-level Excel operations using EPPlus
- `WinFormsFilePickerService` - Provides Windows file dialogs with security constraints (enforces selection within authorized root path)
- `NotificationServiceWrapper` - Thread-safe Radzen notification wrapper with message deduplication
- `IndicatorHelper` - Utility for status indicator image paths

**Database Services (`Services/Database/`):**
- `BoilerTypeService` - CRUD для типов котлов
- `RecipeService` - CRUD + копирование рецептов между типами котлов
- `ResultSettingsService` - CRUD + копирование настроек результатов
- `StepFinalTestService` - CRUD для шагов финального теста
- `ErrorSettingsTemplateService` - CRUD для шаблонов ошибок
- `DatabaseConnectionService` - Управление подключением к SQLite

**Test Execution Services (`Services/Steps/Infrastructure/Execution/`):**

Архитектура обновления грида (SRP):
```
ColumnExecutor ─────────┐
ScanErrorHandler ───────┼──► StepStatusReporter ──► TestSequenseService
BarcodeProcessingPipeline ──┘   (единственный        (CRUD хранилище,
                                 фасад для            НЕ вызывать
                                 обновлений)          напрямую!)
```

**ВАЖНО: Единая точка входа**
- `StepStatusReporter` — **единственный** класс для обновления грида тестов
- `TestSequenseService` — внутреннее хранилище, **НИКОГДА** не вызывать напрямую из других сервисов
- UI компоненты (TestSequenseGrid, BoilerInfo) могут читать данные и подписываться на события

Сервисы:
- `StepStatusReporter` - Фасад для обновления грида (ReportStepStarted, ReportSuccess, ReportError, ClearAll)
- `TestSequenseService` - CRUD хранилище данных грида (внутренний, не использовать напрямую)
- `ColumnExecutor` - Выполнение шагов в колонке (4 параллельных executor'а)
- `TestExecutionCoordinator` - Координация выполнения тестов
- `ExecutionStateManager` - State machine состояния выполнения

Error Handling (`ErrorHandling/`):
- `ErrorPlcMonitor` - OPC UA подписки на сигналы ошибок (Retry, Skip)
- `StepErrorHandler` - Бизнес-логика обработки ошибок

### Configuration (appsettings.json)

**Configuration Sections:**
```json
"Paths" - Test step and sequence directories
"Logging" - Serilog file logging configuration
"ConnectionPrinter" - Printer device connection
"ConnectionWeigher" - Weigher device connection
"OpcUa" - OPC UA server connection (planned, services commented out)
```

**Accessing Configuration:**
```csharp
public MyService(IConfiguration configuration) {
    var path = configuration["Paths:PathToTestsSequence"];
}
```

### Data Models

**SequenceRow** (`Models/SequenceRow.cs`):
- Core data model for test sequence grid
- Properties: `Id` (Guid), `Columns` (List<string>), `CssClass` (for animations)
- Variable column count initialized in constructor

**Enumerations:**
- `SequenceContextAction` - Grid operations (InsertStep, InsertRowBefore, DeleteRow)

## Coding Standards (from .cursorrules)

### Method Complexity Rules

**ONE control flow statement per method:**
- One `if` per method
- One `for` per method
- One `while` per method
- One `switch` per method
- Use early return guards instead of nested conditions

### Code Style

- Always use `var` for type inference
- Braces `{}` mandatory for all blocks (even single-line)
- Prefer LINQ for collection operations
- Use `async/await` for asynchronous operations
- Avoid nested blocks
- Write small, focused methods

### File Organization

**Max 300 lines per file** - Break into partial classes if needed
**Remove unused:** usings, namespaces, properties, fields

**Class order:**
1. Properties (no blank lines between)
2. Fields (no blank lines between)
3. (single blank line)
4. Methods (one blank line between methods, no blank lines within methods)

### Naming Conventions

- **PascalCase:** Classes, methods, properties
- **camelCase:** Local variables, parameters

### Blazor-Specific Rules

**CSS:**
- NEVER use `<style>` tags in `.razor` files
- Always use separate `.razor.css` files
- Use `::deep` selector to override Radzen component styles

**Components:**
- Minimal markup in `.razor` files
- Logic in `@code` blocks or partial classes
- JS Interop in dedicated methods with `RegisterX`/`UnregisterX` pattern
- Always implement `IAsyncDisposable` for cleanup

**Error Handling:**
```csharp
try {
    // Operation
}
catch (Exception ex) {
    Logger.LogError(ex, "Technical details for logging");
    NotificationService.ShowError("User-friendly message");
}
```
- Log technical details with `Logger.LogError(ex, ...)`
- Show user-friendly messages with `NotificationService.ShowError(...)`
- Use specific messages when possible: "Файл занят. Закройте его в Excel"
- Use generic messages as fallback: "Не удалось сохранить файл"

### Test Step Logging (ITestStepLogger)

**ВАЖНО:** Все шаги тестирования (`ITestStep`, `IPreExecutionStep`, `IScanBarcodeStep`) должны использовать `ITestStepLogger` для логирования.

`ITestStepLogger` — это человекочитаемый лог, привязанный к тесту. Он отображается пользователю и сохраняется в историю теста.

```csharp
public class MyTestStep(
    ILogger<MyTestStep> logger,           // Технический лог (файл/консоль)
    ITestStepLogger testStepLogger)       // Человекочитаемый лог теста
{
    private void LogInfo(string message, params object?[] args)
    {
        logger.LogInformation(message, args);       // Файл
        testStepLogger.LogInformation(message, args); // Лог теста
    }

    private BarcodeStepResult Fail(string error)
    {
        testStepLogger.LogError(null, "{Error}", error); // ОБЯЗАТЕЛЬНО!
        return BarcodeStepResult.Fail(error);
    }
}
```

**Правила:**
- Всегда дублировать логи в `ITestStepLogger` и `ILogger`
- При ошибке ВСЕГДА логировать в `testStepLogger.LogError()`
- Использовать `testStepLogger.LogStepStart(Name)` в начале шага
- Использовать `testStepLogger.LogStepEnd(Name)` при успешном завершении
- Сообщения должны быть на русском языке (для пользователя)

## Common Patterns

### Modal Dialog Pattern

```csharp
await DialogService.OpenAsync<ComponentName>("Dialog Title",
    new Dictionary<string, object> { /* parameters */ },
    new DialogOptions {
        Width = "95vw",
        Height = "95vh",
        Resizable = true,
        Draggable = true,
        CloseDialogOnOverlayClick = false
    });
```

### In-Place Grid Editor Pattern

Components that need editable cells inherit from `GridInplaceEditorBase<TItem>`:
- Manages cell edit state
- Handles outside-click detection via JS interop
- Auto-commits changes when clicking outside

### File Picker with Security

`WinFormsFilePickerService` enforces file selection within authorized directories:
```csharp
await FilePickerService.PickFileRelative(rootPath); // Validates selection is within rootPath
```

### Row Animation Pattern

```csharp
row.CssClass = "fade-in";
StateHasChanged();
await Task.Delay(500);
row.CssClass = "";
```

### Tab Refresh Pattern (HashSet)

Независимое обновление табов при изменении данных в другом табе:
```csharp
private readonly HashSet<int> _tabsNeedingRefresh = [];

private void MarkDependentTabsForRefresh()
{
    _tabsNeedingRefresh.Add(RecipesTabIndex);
    _tabsNeedingRefresh.Add(ResultSettingsTabIndex);
}

private async Task OnTabChanged(int tabIndex)
{
    if (!_tabsNeedingRefresh.Remove(tabIndex)) return;
    await RefreshTabContent(tabIndex);
}
```

### UI Dispatching (BlazorDispatcherAccessor)

**Проблема:** В hybrid-приложении WinForms UI thread ≠ Blazor renderer context. Вызов Radzen сервисов из background потоков (RawInputService, OPC UA callbacks) требует маршрутизации в Blazor context.

**Решение:**
```csharp
// Services/Common/UI/BlazorDispatcherAccessor.cs
public class BlazorDispatcherAccessor
{
    private Func<Action, Task>? _invokeAsync;
    private Action? _stateHasChanged;

    public void Initialize(Func<Action, Task> invokeAsync, Action stateHasChanged)
    {
        _invokeAsync = invokeAsync;
        _stateHasChanged = stateHasChanged;
    }

    public Task InvokeAsync(Action action)
    {
        if (_invokeAsync == null) { action(); return Task.CompletedTask; }
        return _invokeAsync(() => { action(); _stateHasChanged?.Invoke(); });
    }
}

// MyComponent.razor (root component)
protected override void OnInitialized()
{
    BlazorDispatcherAccessor.Initialize(
        action => InvokeAsync(action),
        StateHasChanged);
}
```

**Использование в сервисах:**
```csharp
public class BlazorUiDispatcher(BlazorDispatcherAccessor accessor) : IUiDispatcher
{
    public void Dispatch(Action action) => _ = accessor.InvokeAsync(action);
}
```

## Accepted Patterns (NOT bugs)

При code review НЕ флагать следующие паттерны как проблемы:

### CancellationToken.None в коротких операциях
```csharp
await _preExecutionCoordinator.ExecuteAsync(barcode, CancellationToken.None);
```
- Pre-execution занимает 1-2 секунды
- Пользователь не будет делать dispose в этот момент
- Добавление токенов везде — overengineering

### Fire-and-forget в singleton сервисах
```csharp
_ = HandleInterruptAsync(reason).ContinueWith(t => LogError(t), TaskContinuationOptions.OnlyOnFaulted);
```
- Singleton сервисы живут всё время работы приложения
- Dispose вызывается только при закрытии приложения
- Ошибки логируются через ContinueWith
- Tracking задач не нужен

### Event subscriptions в Singleton → Singleton
```csharp
// В конструкторе singleton сервиса
appSettings.UseMesChanged += _ => Clear();
```
- Оба сервиса singleton с одинаковым lifetime
- Unsubscribe не нужен — GC не соберёт ни один из них

### Когда ЭТО проблема:
- Scoped/Transient сервис подписывается на Singleton — нужен unsubscribe
- Короткоживущий объект подписывается на долгоживущий — утечка памяти
- Fire-and-forget без обработки ошибок — исключения теряются

## Important Implementation Notes

### Current State

**Implemented:**
- WinForms + Blazor hybrid architecture
- Test sequence editor with Excel I/O
- Radzen-based UI components (tabs, grids, dialogs, notifications)
- File picker with security constraints
- Serilog file logging
- Configuration management

**Planned (folders/config present but not active):**
- OPC UA services (services commented out in Form1.cs, configuration in appsettings.json)
- Additional engineering tools (Hand Program, IO Editor, AI/RTD Correction)
- Test log viewer
- Gas & DHW charts

### Recent Changes

- **UI Dispatching:** Добавлен `BlazorDispatcherAccessor` для маршрутизации вызовов из background потоков в Blazor context
- **Notifications:** `Radzen.NotificationService` и `INotificationService` теперь Singleton (исправлен scope mismatch)
- **ITestStepLogger:** Все шаги теперь логируют ошибки в человекочитаемый тестовый лог
- **OPC UA:** Полная интеграция с PLC (подписки, чтение/запись тегов, обработка прерываний)
- **MES Integration:** `ScanBarcodeMesStep` для работы с MES сервером (рецепты, rework flow)
- Database сервисы для SQLite (BoilerType, Recipe, ResultSettings, StepFinalTest, ErrorSettingsTemplate)
- StandDatabase компоненты для управления данными стенда
- Система паузы/прерываний (`TestInterruptCoordinator`, `PauseTokenSource`)

### Known Issues

- `MyComponent.razor` — 947KB, рассмотреть разбиение на компоненты

## File Locations

**Entry points:** `Program.cs`, `Form1.cs`
**Root component:** `MyComponent.razor`
**Main features:** `Components/Engineer/MainEngineering.razor`, `Components/Engineer/Sequence/TestSequenceEditor.*`
**Stand database:** `Components/Engineer/StandDatabase/`
**Core services:** `Services/Sequence/`, `Services/Common/`
**Database services:** `Services/Database/`
**Configuration:** `appsettings.json`
**Static assets:** `wwwroot/css/`, `wwwroot/images/`
**Coding standards:** `.cursorrules`
