# ARCHITECTURE.md

Архитектура системы выполнения тестов.

## Общая схема

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              ScanStepManager                                 │
│  - Координатор процесса сканирования                                        │
│  - Делегирует управление режимом → ScanModeController                       │
│  - Делегирует диалоги ошибок → ScanDialogCoordinator                        │
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌──────────────────────┐     ┌──────────────────────┐                      │
│  │  ScanModeController  │     │ ScanDialogCoordinator│                      │
│  │  - Вкл/выкл режим    │     │ - 6 событий диалогов │                      │
│  │  - MessageService    │     │ - Rework callback    │                      │
│  │  - Session mgmt      │     │ - Уведомления        │                      │
│  └──────────────────────┘     └──────────────────────┘                      │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          PreExecutionCoordinator                             │
│  - Выполняет IPreExecutionStep последовательно                              │
│  - Резолвит RawMaps → TestMaps через ITestMapResolver                       │
│  - При успехе запускает TestExecutionCoordinator                            │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                    ┌─────────────────┼─────────────────┐
                    ▼                 ▼                 ▼
            ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
            │ScanBarcode  │   │ScanBarcode  │   │  Будущие    │
            │   Step      │   │  MesStep    │   │   шаги      │
            └─────────────┘   └─────────────┘   └─────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         TestExecutionCoordinator                             │
│  - Выполняет Excel-шаги на 4 колонках параллельно                           │
│  - Использует тот же StepStatusReporter для обновления грида                │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                    ┌─────────────────┼─────────────────┐
                    ▼                 ▼                 ▼
            ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
            │ColumnExec 1 │   │ColumnExec 2 │   │ColumnExec N │
            │  (шаги)     │   │  (шаги)     │   │  (шаги)     │
            └─────────────┘   └─────────────┘   └─────────────┘
```

---

## Два типа шагов

| Тип | Интерфейс | Когда выполняется | Пример |
|-----|-----------|-------------------|--------|
| PreExecution | `IPreExecutionStep` | До тестов (валидация, загрузка) | `ScanBarcodeStep` |
| Test (Excel) | `ITestStep` | Во время тестов (параллельно) | `MeasureVoltageStep` |

---

## Валидация PLC подписок

Система проверки и создания OPC UA подписок при старте приложения.

### Схема инициализации

```
Form1.StartOpcUaConnection()
          │
          ▼
OpcUaConnectionService.ConnectAsync()
          │
          ▼
ErrorPlcMonitor.InitializeAsync()
          │
          ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         PlcSubscriptionInitializer                           │
│  - Ждёт подключения к PLC                                                   │
│  - Собирает теги из всех IRequiresPlcSubscriptions шагов                    │
│  - Создаёт физические подписки (MonitoredItems)                             │
│  - При ошибке → PlcSubscriptionException → крэш приложения                  │
└─────────────────────────────────────────────────────────────────────────────┘
          │
          ▼
PlcSubscriptionState.SetCompleted()
          │
          ▼
UI: SubscriptionLoadingOverlay скрывается
```

### Файлы

| Файл | Назначение |
|------|------------|
| `Services/Steps/Infrastructure/Interaces/Plc/IRequiresPlcSubscriptions.cs` | Интерфейс для шагов с PLC тегами |
| `Services/Steps/Validation/PlcSubscriptionValidator.cs` | Валидатор + результаты |
| `Services/OpcUa/PlcSubscriptionInitializer.cs` | Инициализатор подписок |
| `Services/OpcUa/PlcSubscriptionException.cs` | Исключение при ошибке |
| `Services/OpcUa/PlcSubscriptionState.cs` | Состояние для UI |
| `Components/Loading/SubscriptionLoadingOverlay.razor` | Оверлей загрузки |

### IRequiresPlcSubscriptions

```csharp
public interface IRequiresPlcSubscriptions : ITestStep
{
    IReadOnlyList<string> RequiredPlcTags { get; }
}
```

Шаги, реализующие этот интерфейс, объявляют теги для подписки при старте.

### PlcSubscriptionState

```csharp
public class PlcSubscriptionState
{
    public bool IsCompleted { get; private set; }
    public bool IsInitializing => !IsCompleted;
    public event Action? OnStateChanged;

    public void SetCompleted();
}
```

### Как добавить PLC теги в шаг

1. **Реализовать интерфейс:**

```csharp
public class MyTestStep(
    PausableTagWaiter tagWaiter,
    ILogger<MyTestStep> logger) : ITestStep, IRequiresPlcSubscriptions
{
    public string Id => "my-test-step";
    public string Name => "Мой тестовый шаг";
    public string Description => "Описание";

    // Теги для подписки при старте приложения
    public IReadOnlyList<string> RequiredPlcTags =>
    [
        "ns=3;s=\"DB_Test\".\"Ready\"",
        "ns=3;s=\"DB_Test\".\"Value\""
    ];

    public async Task<TestStepResult> ExecuteAsync(
        TestStepContext context,
        CancellationToken ct)
    {
        // Ожидание значения через PausableTagWaiter
        // (подписка уже создана при старте)
        await tagWaiter.WaitForTrueAsync(
            "ns=3;s=\"DB_Test\".\"Ready\"",
            TimeSpan.FromSeconds(10),
            ct);

        return TestStepResult.Pass();
    }
}
```

2. **Зарегистрировать в DI** — автоматически через `ITestStepRegistry`.

### Разделение ответственности

| Компонент | Когда | Что делает |
|-----------|-------|------------|
| `PlcSubscriptionValidator` | Старт приложения | Создаёт `MonitoredItem` (физическая подписка) |
| `PausableTagWaiter` | Выполнение теста | Добавляет/удаляет колбэки |

**Преимущество:** Физическая подписка создаётся один раз, колбэки добавляются по требованию.

### Обработка ошибок

При ошибке подписки:
1. `PlcSubscriptionException` бросается
2. Попадает в `Application.ThreadException`
3. `LogCritical` записывает в лог (`D:/Logs/app-.txt`)
4. `MessageBox` показывает ошибку (в DEBUG — полную, в RELEASE — краткую)
5. `Environment.Exit(1)` — приложение закрывается

---

## PreExecution шаги

### Файлы

| Файл | Назначение |
|------|------------|
| `Services/Steps/Infrastructure/Interaces/PreExecution/IPreExecutionStep.cs` | Интерфейс шага |
| `Services/Steps/Infrastructure/Interaces/PreExecution/IPreExecutionStepRegistry.cs` | Интерфейс реестра |
| `Services/Steps/Infrastructure/Interaces/PreExecution/PreExecutionContext.cs` | Контекст выполнения |
| `Services/Steps/Infrastructure/Interaces/PreExecution/PreExecutionResult.cs` | Результат выполнения |
| `Services/Steps/Infrastructure/Interaces/PreExecution/IPreExecutionErrorDetails.cs` | Детали ошибок |
| `Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.cs` | Координатор |
| `Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionStepRegistry.cs` | Реестр шагов |
| `Services/Steps/Infrastructure/Execution/Scanning/ScanStepManager.cs` | Координатор сканирования |
| `Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs` | Управление режимом сканирования |
| `Services/Steps/Infrastructure/Execution/Scanning/ScanDialogCoordinator.cs` | Координация диалогов ошибок |
| `Services/Steps/Infrastructure/Execution/Scanning/ScanStateManager.cs` | State machine сканирования |
| `Services/Steps/Infrastructure/Execution/Scanning/ScanSessionManager.cs` | Управление сессией сканера |
| `Services/Steps/Steps/ScanBarcodeStep.cs` | Шаг сканирования (не MES) |
| `Services/Steps/Steps/ScanBarcodeMesStep.cs` | Шаг сканирования (MES) |

### IPreExecutionStep

```csharp
public interface IPreExecutionStep
{
    string Id { get; }           // Уникальный идентификатор ("scan-barcode")
    string Name { get; }         // Отображаемое имя
    string Description { get; }  // Описание для грида
    Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct);
}
```

### PreExecutionContext

```csharp
public class PreExecutionContext
{
    public required string Barcode { get; init; }
    public required BoilerState BoilerState { get; init; }
    public required PausableOpcUaTagService OpcUa { get; init; }  // Pausable для паузы при потере автомата
    public required ITestStepLogger TestStepLogger { get; init; }

    public Guid? ScanStepId { get; set; }
    public List<RawTestMap>? RawMaps { get; set; }  // Заполняется шагом
    public List<TestMap>? Maps { get; set; }        // Резолвится координатором
}
```

### PreExecutionResult

```csharp
public class PreExecutionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public bool ShouldStop { get; init; }
    public IPreExecutionErrorDetails? ErrorDetails { get; init; }

    public static PreExecutionResult Ok();
    public static PreExecutionResult Stop();
    public static PreExecutionResult Fail(string error);
    public static PreExecutionResult Fail(string error, IPreExecutionErrorDetails details);
}
```

### Как добавить новый PreExecution шаг

1. **Создать класс шага:**

```csharp
// Services/Steps/Steps/MyNewStep.cs
public class MyNewStep(
    SomeDependency dependency,
    ILogger<MyNewStep> logger) : IPreExecutionStep
{
    public string Id => "my-new-step";
    public string Name => "Мой новый шаг";
    public string Description => "Описание шага";

    public async Task<PreExecutionResult> ExecuteAsync(
        PreExecutionContext context,
        CancellationToken ct)
    {
        // Логика шага
        if (error)
        {
            return PreExecutionResult.Fail("Ошибка");
        }
        return PreExecutionResult.Ok();
    }
}
```

2. **Зарегистрировать в DI (Form1.cs):**

```csharp
services.AddSingleton<IPreExecutionStep, MyNewStep>();
```

3. **(Опционально) Добавить в фильтрацию реестра:**

Если шаг зависит от режима UseMes, обновить `PreExecutionStepRegistry.GetOrderedSteps()`.

4. **(Опционально) Добавить детальные ошибки:**

```csharp
// IPreExecutionErrorDetails.cs
public record MyStepErrorDetails(IReadOnlyList<string> Items) : IPreExecutionErrorDetails;

// ScanStepManager.cs - добавить в switch
MissingRecipesDetails details => OnMissingRecipesDialogRequested?.Invoke(details.Recipes),

// Создать диалог в Components/Main/Modals/
```

### Детальные ошибки

| Тип | Данные | Диалог |
|-----|--------|--------|
| `MissingPlcTagsDetails` | `IReadOnlyList<string> Tags` | `MissingTagsDialog` |
| `MissingRequiredTagsDetails` | `IReadOnlyList<string> Tags` | `MissingTagsDialog` |
| `UnknownStepsDetails` | `IReadOnlyList<UnknownStepInfo> Steps` | `UnknownStepsDialog` |
| `MissingRecipesDetails` | `IReadOnlyList<MissingRecipeInfo> Recipes` | `MissingRecipesDialog` |

### Переключение UseMes

`PreExecutionStepRegistry` фильтрует шаги по `AppSettingsService.UseMes`:

```csharp
public IReadOnlyList<IPreExecutionStep> GetOrderedSteps()
{
    var targetId = appSettings.UseMes ? "scan-barcode-mes" : "scan-barcode";
    return _steps.Where(s => s.Id == targetId).ToList();
}
```

При смене UseMes автоматически:
- `BoilerState.Clear()` — очистка состояния котла
- `StepStatusReporter.ClearAll()` — очистка грида
- Переключатель UI заблокирован во время обработки

---

## Test (Excel) шаги

### Файлы

| Файл | Назначение |
|------|------------|
| `Services/Steps/Infrastructure/Interaces/Test/ITestStep.cs` | Интерфейс шага |
| `Services/Steps/Infrastructure/Registrator/TestStepContext.cs` | Контекст выполнения |
| `Services/Steps/Infrastructure/Registrator/TestStepResult.cs` | Результат выполнения |
| `Services/Steps/Infrastructure/Execution/TestExecutionCoordinator.cs` | Координатор |
| `Services/Steps/Infrastructure/Execution/ColumnExecutor.cs` | Исполнитель колонки |
| `Services/Steps/Steps/*.cs` | Реализации шагов |

### ITestStep

```csharp
public interface ITestStep
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    bool IsVisibleInEditor => true;
    Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct);
}
```

### TestStepContext

```csharp
public class TestStepContext(
    int columnIndex,
    PausableOpcUaTagService opcUa,  // Pausable для паузы при потере автомата
    ILogger logger,
    IRecipeProvider recipeProvider)
{
    public int ColumnIndex { get; }
    public PausableOpcUaTagService OpcUa { get; }  // Все PLC операции паузятся
    public ILogger Logger { get; }
    public IRecipeProvider RecipeProvider { get; }
    public Dictionary<string, object> Variables { get; } = [];  // Общие переменные колонки
}
```

### TestStepResult

```csharp
public class TestStepResult
{
    public bool Success { get; init; }
    public bool Skipped { get; init; }
    public string Message { get; init; }
    public Dictionary<string, object>? OutputData { get; init; }

    public static TestStepResult Pass(string value = "", string? limits = null);
    public static TestStepResult Fail(string value, string? limits = null);
    public static TestStepResult Skip(string value = "");
}
```

### Передача данных между шагами

**Внутри одной колонки** — через `context.Variables`:

```csharp
// Шаг 1 записывает
public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
{
    var value = await Measure();
    context.Variables["MeasuredValue"] = value;
    return TestStepResult.Pass($"{value}");
}

// Шаг 5 читает
public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
{
    var value = (double)context.Variables["MeasuredValue"];
    // использование value
}
```

**Между колонками** — через общие сервисы (`BoilerState`, OPC UA).

### Как добавить новый Test шаг

1. **Создать класс шага:**

```csharp
// Services/Steps/Steps/MyTestStep.cs
public class MyTestStep(
    OpcUaTagService opcUa,
    ILogger<MyTestStep> logger) : ITestStep
{
    public string Id => "my-test-step";
    public string Name => "Мой тестовый шаг";
    public string Description => "Описание";

    public async Task<TestStepResult> ExecuteAsync(
        TestStepContext context,
        CancellationToken ct)
    {
        var value = await opcUa.ReadAsync<double>("Tag.Address");
        if (value < 0)
        {
            return TestStepResult.Fail($"Значение {value} меньше 0", "Min: 0");
        }
        return TestStepResult.Pass($"{value}", "Min: 0");
    }
}
```

2. **Зарегистрировать в DI (Form1.cs):**

```csharp
services.AddSingleton<ITestStep, MyTestStep>();
```

3. **Добавить в Excel-файл последовательности** с именем шага.

---

## Обновление грида (SRP)

**Единственная точка входа:** `StepStatusReporter`

```
PreExecutionCoordinator ───┐
ColumnExecutor ────────────┼──► StepStatusReporter ──► TestSequenseService
StepErrorHandler ──────────┘         │                        │
                                     │                        ▼
                                     │               OnDataChanged event
                                     │                        │
                                     ▼                        ▼
                              ReportStepStarted()      UI компоненты
                              ReportSuccess()          (StateHasChanged)
                              ReportError()
                              ClearAll()
```

**Методы StepStatusReporter:**

| Метод | Назначение |
|-------|------------|
| `ReportStepStarted(step)` | Добавить шаг в грид, вернуть Guid |
| `ReportSuccess(id, message, limits)` | Отметить успех |
| `ReportError(id, message, limits)` | Отметить ошибку |
| `ReportRetry(id)` | Перезапуск шага |
| `ReportSkip(id)` | Пропуск шага |
| `ClearAll()` | Очистить грид |

---

## Обработка ошибок Excel-шагов

```
ColumnExecutor
      │
      ▼ (ошибка шага)
ErrorPlcMonitor (OPC UA подписки)
      │
      ├─► Retry сигнал от PLC
      │         │
      │         ▼
      │   StepErrorHandler.HandleRetry()
      │         │
      │         ▼
      │   StepStatusReporter.ReportRetry(stepId)
      │   ColumnExecutor повторяет шаг
      │
      └─► Skip сигнал от PLC
                │
                ▼
          StepErrorHandler.HandleSkip()
                │
                ▼
          StepStatusReporter.ReportSkip(stepId)
          ColumnExecutor пропускает шаг
```

---

## Поток данных при сканировании

```
1. Оператор сканирует штрихкод
          │
          ▼
2. ScanStepManager.ProcessBarcodeAsync(barcode)
          │
          ▼
3. PreExecutionCoordinator.ExecuteAsync(barcode)
          │
          ├─► ClearAll() — очистка грида
          │
          ├─► ScanBarcodeStep.ExecuteAsync()
          │       │
          │       ├─ Валидация штрихкода
          │       ├─ Загрузка рецептов → BoilerState
          │       └─ context.RawMaps = [...]
          │
          ├─► ResolveTestMaps(context)
          │       │
          │       └─ context.Maps = mapResolver.Resolve(context.RawMaps)
          │
          └─► StartTestExecution(context)
                  │
                  ▼
4. TestExecutionCoordinator.StartAsync()
          │
          ├─► ColumnExecutor[0].ExecuteAsync()  ─┐
          ├─► ColumnExecutor[1].ExecuteAsync()   │ параллельно
          ├─► ColumnExecutor[2].ExecuteAsync()   │
          └─► ColumnExecutor[3].ExecuteAsync()  ─┘
```

---

## Регистрация в DI (Form1.cs)

```csharp
// Validation
services.AddSingleton<RecipeValidator>();
services.AddSingleton<PlcSubscriptionValidator>();
services.AddSingleton<PlcSubscriptionState>();
services.AddSingleton<PlcSubscriptionInitializer>();

// PreExecution
services.AddSingleton<IPreExecutionStepRegistry, PreExecutionStepRegistry>();
services.AddSingleton<IPreExecutionStep, ScanBarcodeStep>();
services.AddSingleton<IPreExecutionStep, ScanBarcodeMesStep>();
services.AddSingleton<PreExecutionCoordinator>();

// Scanning (порядок важен!)
services.AddSingleton<ScanSessionManager>();
services.AddSingleton<ScanStateManager>();
services.AddSingleton<ScanErrorHandler>();
services.AddSingleton<ScanDialogCoordinator>();
services.AddSingleton<ScanModeController>();
services.AddSingleton<ScanStepManager>();

// Test Execution
services.AddSingleton<TestExecutionCoordinator>();
services.AddSingleton<StepStatusReporter>();

// Test Steps
services.AddSingleton<ITestStep, MeasureVoltageStep>();
services.AddSingleton<ITestStep, CheckResistanceStep>();
// ... другие шаги

// OPC UA Services (порядок важен!)
services.AddSingleton<OpcUaConnectionState>();
services.AddSingleton<OpcUaSubscription>();
services.AddSingleton<OpcUaConnectionService>();
services.AddSingleton<OpcUaTagService>();
services.AddSingleton<OpcUaBrowseService>();
services.AddSingleton<PausableOpcUaTagService>();  // Обёртка с паузой
services.AddSingleton<TagWaiter>();                 // Базовый waiter без паузы
services.AddSingleton<PausableTagWaiter>();         // Обёртка с паузой

// Pause & Interrupt System
services.AddSingleton<PauseTokenSource>();
services.AddSingleton<ErrorCoordinator>();          // Использует TagWaiter (без паузы)
services.AddSingleton<PlcResetCoordinator>();       // Использует TagWaiter (без паузы)
services.AddSingleton<TestExecutionCoordinator>();  // Передаёт PausableOpcUaTagService в шаги
```

---

## Система паузы и прерываний

Система для паузы/сброса тестов при потере связи с PLC или автоматом.

### Архитектура: TagWaiter vs PausableTagWaiter

**Ключевой принцип:** Шаги паузятся при потере автомата, координаторы продолжают работать.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              TagWaiter (базовый)                             │
│  - Ожидание значений OPC UA тегов                                           │
│  - БЕЗ проверки паузы — всегда работает                                     │
│  - Используется: ErrorCoordinator, PlcResetCoordinator                      │
└─────────────────────────────────────────────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         PausableTagWaiter (обёртка)                          │
│  - Делегирует к TagWaiter                                                   │
│  - ДОБАВЛЯЕТ WaitWhilePausedAsync() перед каждой операцией                  │
│  - Используется: ITestStep через context.OpcUa                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Распределение по компонентам

| Компонент | Сервис | Паузится? | Причина |
|-----------|--------|-----------|---------|
| **TestStepContext.OpcUa** | `PausableOpcUaTagService` | ✓ | Шаги должны ждать |
| **PreExecutionContext.OpcUa** | `PausableOpcUaTagService` | ✓ | Шаги должны ждать |
| **ErrorCoordinator** | `TagWaiter`, `OpcUaTagService` | ✗ | Обрабатывает ошибки |
| **PlcResetCoordinator** | `TagWaiter`, `OpcUaTagService` | ✗ | Обрабатывает сброс |
| **TestExecutionCoordinator._plcService** | `OpcUaTagService` | ✗ | SetSelectedAsync |

### Схема

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            ErrorCoordinator                                  │
│  - Подписывается на OpcUaConnectionState.ConnectionStateChanged             │
│  - Подписывается на AutoReadySubscription.OnStateChanged                    │
│  - Управляет PauseTokenSource (пауза/возобновление)                         │
│  - Использует TagWaiter (без паузы) для своей работы                        │
└─────────────────────────────────────────────────────────────────────────────┘
                                     │
         ┌───────────────────────────┼───────────────────────────┐
         ▼                           ▼                           ▼
┌─────────────────┐        ┌─────────────────┐        ┌─────────────────┐
│ PauseTokenSource│        │PlcResetCoord    │        │TestExecution    │
│   (пауза)       │        │(TagWaiter)      │        │  Coordinator    │
│ Pause()/Resume()│        │(без паузы)      │        │                 │
└─────────────────┘        └─────────────────┘        └─────────────────┘
         │                                                     │
         ▼                                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              ColumnExecutor                                  │
│  - WaitWhilePausedAsync() между шагами                                      │
│  - context.OpcUa = PausableOpcUaTagService (паузится при Read/Write)        │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Файлы

| Файл | Назначение |
|------|------------|
| `Services/Common/PauseTokenSource.cs` | Thread-safe примитив паузы |
| `Services/OpcUa/TagWaiter.cs` | **Базовый** waiter без паузы |
| `Services/OpcUa/PausableTagWaiter.cs` | Обёртка с паузой над TagWaiter |
| `Services/OpcUa/PausableOpcUaTagService.cs` | Обёртка Read/Write с паузой |
| `Services/Steps/Infrastructure/Execution/ErrorCoordinator.cs` | Координатор прерываний |
| `Services/Main/PlcReset/PlcResetCoordinator.cs` | Координатор сброса PLC |

### PauseTokenSource

Thread-safe примитив паузы на основе `TaskCompletionSource`:

```csharp
public class PauseTokenSource
{
    private readonly Lock _lock = new();
    private TaskCompletionSource? _pauseTcs;

    public bool IsPaused { get; }
    public void Pause();      // Установить паузу
    public void Resume();     // Снять паузу
    public Task WaitWhilePausedAsync(CancellationToken ct);  // Ждать пока на паузе
}
```

### TestInterruptReason

```csharp
public enum TestInterruptReason
{
    PlcConnectionLost,   // Потеря связи с PLC
    AutoModeDisabled,    // Пропал автомат (AutoReady)
    TagTimeout           // Таймаут ожидания тега
}
```

### InterruptBehavior

```csharp
public record InterruptBehavior(
    string Message,              // Сообщение для toast
    InterruptAction Action,      // PauseAndWait или ResetAfterDelay
    TimeSpan? Delay = null,      // Задержка перед сбросом
    TimeSpan? WaitForRecovery = null
);

public enum InterruptAction
{
    PauseAndWait,      // Пауза, ждать восстановления
    ResetAfterDelay    // Сброс после задержки
}
```

### Поведение системы

| Событие | Toast | Действие |
|---------|-------|----------|
| Потеря связи с PLC | "Потеря связи с PLC" | 5 сек → сброс теста |
| Пропал автомат | "Нет автомата" | Пауза (бесконечно) |
| Автомат вернулся | "Автомат восстановлен" | Продолжить тест |
| Таймаут тега | "Нет ответа" | Сброс теста |

### Потеря связи с PLC: детали Reset

При потере связи `ErrorCoordinator.Reset()` очищает всё:

```
Reset() → ClearAllState():
├── _stateManager.ClearErrors()              ✓ Очередь ошибок
├── _stateManager.TransitionTo(Failed)       ✓ State → Failed
├── _errorService.ClearActiveApplicationErrors()  ✓ ActiveErrors
└── _interruptMessage.Clear()                ✓ Сообщение прерывания
```

**Диалог ошибки шага** (`ErrorHandlingDialog`) закрывается автоматически:
- Подписан на `StateManager.OnStateChanged`
- `TransitionTo(Failed)` → `IsDialogClosingState(Failed) = true` → `DialogService.Close()`

**SetSelectedAsync при потере связи:**
- Запись не удастся → warning в лог
- Не критично: Reset через 5 сек всё равно произойдёт

### Поток прерывания: AutoReady Loss → Pause

```
1. AutoReadySubscription детектит AutoReady = false
          │
          ▼
2. ErrorCoordinator.HandleAutoReadyChanged()
          │
          ▼
3. FireAndForgetInterrupt(InterruptReason.AutoModeDisabled)
          │
          ▼
4. HandleInterruptAsync() → Behavior: PauseAndWait
          │
          ▼
5. _pauseToken.Pause()
          │
          ▼
6. Все шаги (PausableTagWaiter/PausableOpcUaTagService) блокируются
   ErrorCoordinator/PlcResetCoordinator продолжают работать (TagWaiter)
```

### Поток восстановления: AutoReady Recovery

```
1. AutoReadySubscription детектит AutoReady = true
          │
          ▼
2. ErrorCoordinator.HandleAutoReadyChanged()
          │
          ▼
3. FireAndForgetResume()
          │
          ▼
4. TryResumeFromPauseAsync() → _pauseToken.Resume()
          │
          ▼
5. Все заблокированные операции разблокируются
          │
          ▼
6. Toast: "Автомат восстановлен"
```

### Интеграция с ColumnExecutor

В `ColumnExecutor` добавлена проверка паузы перед каждым шагом:

```csharp
foreach (var step in stepsToExecute)
{
    await _pauseToken.WaitWhilePausedAsync(ct);  // Ждать если на паузе
    await ExecuteStep(step!, ct);
}
```

### ⚠️ ВАЖНО: Сброс теста

При любом сбросе теста **ОБЯЗАТЕЛЬНО** вызывать `_pauseToken.Resume()`:

```csharp
private void Reset()
{
    _pauseToken.Resume();  // ← КРИТИЧНО! Иначе следующий тест зависнет
    _testCoordinator.Stop();
    _statusReporter.ClearAll();
    _boilerState.Clear();
}
```

**Почему:** Если тест был на паузе (AutoModeDisabled) и произошёл Reset (PlcConnectionLost),
`_pauseTcs` останется не-null. При следующем StartAsync все `WaitWhilePausedAsync()` сразу зависнут.

### Thread-Safety

| Компонент | Механизм | Защищает |
|-----------|----------|----------|
| `PauseTokenSource` | `Lock` | `_pauseTcs` |
| `ErrorCoordinator` | `Interlocked` | `_isHandlingInterrupt` |
| `PlcResetCoordinator` | `Interlocked` | `_isHandlingReset` |

### Как добавить новый тип прерывания

1. **Добавить в enum:**

```csharp
public enum InterruptReason
{
    // ... существующие
    EmergencyStop  // ← НОВЫЙ
}
```

2. **Добавить поведение в ErrorCoordinator:**

```csharp
InterruptBehaviors[InterruptReason.EmergencyStop] = new(
    Message: "Аварийная остановка!",
    Action: InterruptAction.ResetAfterDelay,
    Delay: TimeSpan.Zero);
```

3. **Подписаться на событие:**

```csharp
_emergencyMonitor.OnEmergency += () =>
    FireAndForgetInterrupt(InterruptReason.EmergencyStop);
```

---

## ErrorCoordinator: Partial Classes

`ErrorCoordinator` разделён на 3 partial-файла для улучшения читаемости:

```
Services/Steps/Infrastructure/Execution/
├── ErrorCoordinator.cs            # Базовый: поля, конструктор, события, dispose
├── ErrorCoordinator.Interrupts.cs # Обработка прерываний
└── ErrorCoordinator.Recovery.cs   # Сброс и восстановление
```

### ErrorCoordinator.cs (Base)

| Член | Назначение |
|------|------------|
| **Поля** | `_connectionState`, `_autoReady`, `_pauseToken`, `_tagWaiter`, `_plcService`, etc. |
| **События** | `OnReset`, `OnRecovered` |
| **InterruptBehaviors** | Dictionary с настройками для каждого InterruptReason |
| **SubscribeToEvents()** | Подписка на ConnectionStateChanged, AutoReadyChanged |
| **FireAndForgetInterrupt()** | Запуск обработки прерывания |
| **FireAndForgetResume()** | Запуск восстановления |
| **DisposeAsync()** | Отписка и очистка ресурсов |

### ErrorCoordinator.Interrupts.cs

| Метод | Назначение |
|-------|------------|
| **TryAcquireInterruptFlag()** | Атомарный захват флага обработки |
| **ReleaseInterruptFlag()** | Освобождение флага |
| **HandleInterruptAsync()** | Основной цикл обработки прерывания |
| **ProcessInterruptAsync()** | Логирование, уведомление, действие |
| **ExecuteInterruptActionAsync()** | PauseAndWait или ResetAfterDelay |
| **WaitForResolutionAsync()** | Ожидание Retry/Skip от оператора |
| **WaitForOperatorSignalAsync()** | WaitGroup для PLC сигналов |

### ErrorCoordinator.Recovery.cs

| Метод | Назначение |
|-------|------------|
| **Reset()** | Полный сброс: Resume + Clear + OnReset |
| **ForceStop()** | Мягкий сброс: только очистка ошибок, сохраняет данные |
| **ClearAllState()** | Очистка всех состояний |
| **ClearErrorsOnly()** | Очистка только ошибок |
| **TryResumeFromPauseAsync()** | Восстановление после паузы |
| **ResumeIfPaused()** | Снятие паузы если активна |
| **ResumeExecution()** | Resume + уведомление + OnRecovered |

### Два типа сброса

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              ForceStop()                                     │
│  - Мягкий сброс — сохраняет данные теста                                    │
│  - Вызывается: PlcResetCoordinator после Req_Reset                          │
│  - Действия: ClearErrorsOnly(), очистка сообщений                           │
└─────────────────────────────────────────────────────────────────────────────┘
                                     ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│                                Reset()                                       │
│  - Полный сброс — очищает всё                                               │
│  - Вызывается: таймаут, критическая ошибка, потеря связи                    │
│  - Действия: Resume() + ClearAllState() + OnReset?.Invoke()                 │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## WaitGroup Pattern

Паттерн "первый побеждает" для ожидания одного из нескольких PLC сигналов.

### Файлы

```
Services/OpcUa/WaitGroup/
├── TagWaitCondition.cs    # Условие ожидания: NodeId + Condition + Name
├── TagWaitResult.cs       # Результат: WinnerIndex + NodeId + Value + Name
├── WaitGroupBuilder.cs    # Builder без типизированного результата
└── WaitGroupBuilder{T}.cs # Builder с типизированным результатом
```

### Структура

```csharp
public record TagWaitCondition
{
    public required string NodeId { get; init; }
    public required Func<object?, bool> Condition { get; init; }
    public string? Name { get; init; }
}

public record TagWaitResult<TResult>
{
    public int WinnerIndex { get; init; }      // Индекс сработавшего условия
    public required string NodeId { get; init; }
    public object? RawValue { get; init; }
    public TResult? Result { get; init; }      // Типизированный результат
    public string? Name { get; init; }
}
```

### Использование

```csharp
// Ожидание Retry или Skip от оператора
var result = await tagWaiter.WaitAnyAsync(
    tagWaiter.CreateWaitGroup<ErrorResolution>()
        .WaitForTrue(BaseTags.ErrorRetry, () => ErrorResolution.Retry, "Retry")
        .WaitForTrue(BaseTags.ErrorSkip, () => ErrorResolution.Skip, "Skip")
        .WithTimeout(TimeSpan.FromSeconds(60)),
    ct);

switch (result.Result)
{
    case ErrorResolution.Retry:
        // Повторить шаг
        break;
    case ErrorResolution.Skip:
        // Пропустить шаг
        break;
}
```

### Логика работы

```
1. CheckCurrentValues() — проверка уже активных сигналов
         │
         ▼ (если ни один не активен)
2. CreateHandlers() — создание обработчиков для каждого условия
         │
         ▼
3. SubscribeAllAsync() — подписка на все NodeId
         │
         ▼
4. RecheckAfterSubscribe() — повторная проверка (race condition protection)
         │
         ▼
5. WaitWithTimeoutAsync() — ожидание первого срабатывания
         │
         ▼
6. UnsubscribeAllAsync() — отписка от всех (в finally)
```

### Преимущества

| Аспект | Преимущество |
|--------|-------------|
| **Race-free** | Двойная проверка: до и после подписки |
| **Автоотписка** | `finally` гарантирует cleanup |
| **Типобезопасность** | Generic `WaitGroupBuilder<T>` |
| **Расширяемость** | Легко добавить новые условия |

---

## ExecutionStateManager

State machine для управления состоянием выполнения теста.

### Файл

`Models/Steps/ExecutionStateManager.cs`

### Состояния

```csharp
public enum ExecutionState
{
    Idle,           // Ожидание
    Running,        // Выполнение
    Paused,         // Пауза (AutoReady = false)
    Error,          // Ошибка (ожидание Retry/Skip)
    Completing,     // Завершение
    Completed       // Завершён
}
```

### API

| Член | Назначение |
|------|------------|
| `State` | Текущее состояние |
| `HasPendingErrors` | Есть ошибки в очереди |
| `CanProcessSignals` | Можно обрабатывать PLC сигналы |
| `IsActive` | Тест активен (не Idle, не Completed) |
| `CurrentError` | Текущая ошибка из очереди |
| `ErrorCount` | Количество ошибок в очереди |
| `TransitionTo()` | Переход в новое состояние |
| `EnqueueError()` | Добавить ошибку в очередь |
| `DequeueError()` | Извлечь ошибку из очереди |
| `ClearErrors()` | Очистить очередь ошибок |
| `OnStateChanged` | Событие смены состояния |

### Очередь ошибок

`ExecutionStateManager` поддерживает очередь ошибок для последовательной обработки:

```csharp
private readonly Queue<ErrorInfo> _errorQueue = new();
private readonly Lock _queueLock = new();

public void EnqueueError(ErrorInfo error)
{
    lock (_queueLock)
    {
        _errorQueue.Enqueue(error);
    }
    TransitionTo(ExecutionState.Error);
}
```

---

## ExecutionActivityTracker

Трекер активных фаз выполнения (PreExecution, TestExecution).

### Файл

`Services/Common/ExecutionActivityTracker.cs`

### API

| Член | Назначение |
|------|------------|
| `IsPreExecutionActive` | PreExecution шаги выполняются |
| `IsTestExecutionActive` | Test шаги выполняются |
| `IsAnyActive` | Любая фаза активна |
| `SetPreExecutionActive(bool)` | Установить статус PreExecution |
| `SetTestExecutionActive(bool)` | Установить статус TestExecution |
| `Clear()` | Сбросить все флаги |
| `OnChanged` | Событие изменения |

### Thread-Safety

```csharp
private readonly Lock _lock = new();
private int _isPreExecutionActive;
private int _isTestExecutionActive;

public bool IsAnyActive
{
    get
    {
        lock (_lock)
        {
            return _isPreExecutionActive > 0 || _isTestExecutionActive > 0;
        }
    }
}
```

### Использование

```csharp
// PreExecutionCoordinator.ExecuteAsync()
_activityTracker.SetPreExecutionActive(true);
try
{
    return await ExecutePreExecutionPipelineAsync(...);
}
finally
{
    _activityTracker.SetPreExecutionActive(false);
}
```

---

## MessageStateBase

Базовый класс для thread-safe хранения сообщений с уведомлением об изменении.

### Файл

`Services/Main/MessageStateBase.cs`

### Наследники

| Класс | Файл | Назначение |
|-------|------|------------|
| `ExecutionMessageState` | `Services/Main/ExecutionMessageState.cs` | Сообщения выполнения |
| `InterruptMessageState` | `Services/Main/InterruptMessageState.cs` | Сообщения прерываний |
| `ResetMessageState` | `Services/Main/PlcReset/ResetMessageState.cs` | Сообщения сброса |

### API

```csharp
public abstract class MessageStateBase
{
    private readonly Lock _lock = new();
    private string _message = "";

    public event Action? OnChange;

    public void SetMessage(string message)
    {
        lock (_lock)
        {
            if (_message == message) return;
            _message = message;
        }
        OnChange?.Invoke();
    }

    public void Clear() => SetMessage("");

    public string GetMessage()
    {
        lock (_lock) { return _message; }
    }
}
```

### Использование в UI

```razor
@inject ExecutionMessageState MessageState

@if (!string.IsNullOrEmpty(MessageState.GetMessage()))
{
    <div class="execution-message">@MessageState.GetMessage()</div>
}

@code {
    protected override void OnInitialized()
    {
        MessageState.OnChange += StateHasChanged;
    }
}
```

---

## PlcResetCoordinator: Поток сброса

Координатор обработки сигнала `Req_Reset` от PLC.

### Файл

`Services/Main/PlcReset/PlcResetCoordinator.cs`

### Компоненты

| Файл | Назначение |
|------|------------|
| `PlcResetCoordinator.cs` | Основная логика сброса |
| `ResetSubscription.cs` | Подписка на Req_Reset |
| `ResetMessageState.cs` | Сообщения для UI |

### Поток сброса

```
1. ResetSubscription детектит Req_Reset = true
          │
          ▼
2. PlcResetCoordinator.HandleResetSignal()
          │
          ▼
3. TryAcquireResetFlag() — защита от повторного входа
          │
          ▼
4. ExecuteResetStepsAsync():
   ├── SignalForceStop() → OnForceStop?.Invoke()
   ├── SendDataToMesAsync() → отправка данных в MES
   └── SendResetAndWaitAckAsync():
       ├── WriteAsync(BaseTags.Reset, true)
       └── WaitAnyAsync(AskEnd с таймаутом 60 сек)
          │
          ▼
5. ErrorCoordinator.ForceStop() — мягкий сброс
          │
          ▼
6. Cleanup() → ReleaseResetFlag()
```

### Обработка ошибок

| Исключение | Действие |
|------------|----------|
| `OperationCanceledException` + disposed | Логирование, выход |
| `OperationCanceledException` | Логирование, выход |
| `TimeoutException` (AskEnd) | `ErrorCoordinator.Reset()` — полный сброс |
| Другие | `ErrorCoordinator.Reset()` — полный сброс |

### Связь с ErrorCoordinator

```
PlcResetCoordinator                     ErrorCoordinator
       │                                       │
       │─────── OnForceStop ──────────────────►│ (подписчик)
       │                                       │
       │─────── ForceStop() ──────────────────►│ ClearErrorsOnly()
       │                                       │
       │                                       │
       │ (при таймауте/ошибке)                 │
       │─────── Reset() ──────────────────────►│ ClearAllState() + OnReset
```

---

## UI Dispatching в Hybrid-приложении

### Проблема

В WinForms + Blazor hybrid есть **два разных UI контекста**:

```
┌─────────────────────────────────────────────────────────────┐
│                    WinForms UI Thread                        │
│  - Control.BeginInvoke()                                     │
│  - Windows Messages (WM_INPUT, etc.)                         │
└─────────────────────────────────────────────────────────────┘
                           ≠
┌─────────────────────────────────────────────────────────────┐
│                   Blazor Renderer Context                    │
│  - ComponentBase.InvokeAsync()                               │
│  - Radzen NotificationService                                │
│  - StateHasChanged()                                         │
└─────────────────────────────────────────────────────────────┘
```

Radzen сервисы (NotificationService, DialogService) работают **только** в Blazor context.

### Решение: BlazorDispatcherAccessor

```
RawInputService (WM_INPUT thread)
       │
       ▼
NotificationServiceWrapper.ShowError()
       │
       ▼
BlazorUiDispatcher.Dispatch()
       │
       ▼
BlazorDispatcherAccessor.InvokeAsync()
       │
       ▼
ComponentBase.InvokeAsync()  ← Blazor context!
       │
       ▼
Radzen.NotificationService.Notify()
       ✓
```

### Файлы

| Файл | Назначение |
|------|------------|
| `Services/Common/UI/IUiDispatcher.cs` | Интерфейс диспетчера |
| `Services/Common/UI/BlazorDispatcherAccessor.cs` | Захват InvokeAsync из root component |
| `Services/Common/UI/BlazorUiDispatcher.cs` | Реализация IUiDispatcher |
| `Services/Common/UI/NotificationServiceWrapper.cs` | Обёртка с dispatching |

### Регистрация (Form1.cs)

```csharp
// ВАЖНО: Все должны быть Singleton!
services.AddSingleton<BlazorDispatcherAccessor>();
services.AddSingleton<IUiDispatcher, BlazorUiDispatcher>();
services.AddSingleton<INotificationService, NotificationServiceWrapper>();
services.AddSingleton<Radzen.NotificationService>();  // Override scoped!
```

### Инициализация (MyComponent.razor)

```csharp
@inject BlazorDispatcherAccessor BlazorDispatcherAccessor

protected override void OnInitialized()
{
    BlazorDispatcherAccessor.Initialize(
        action => InvokeAsync(action),
        StateHasChanged);
}
```

### ⚠️ Типичная ошибка: Scope Mismatch

```
ScanErrorHandler (Singleton)
       │
       ▼ инжектит
INotificationService (Scoped)
       │
       ▼ получает instance из
Root Scope (≠ Blazor UI Scope!)
       │
       ▼ вызывает
NotificationService.Notify()
       │
       ✗ UI не обновляется!
```

**Решение:** Сделать `Radzen.NotificationService` и `INotificationService` Singleton.
