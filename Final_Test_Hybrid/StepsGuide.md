# StepsGuide.md

Подробная инструкция по созданию шагов (Steps) в проекте Final_Test_Hybrid.

> **См. также:** [CLAUDE.md](CLAUDE.md), [ErrorSystemGuide.md](ErrorSystemGuide.md)

---

## Обзор архитектуры

В проекте существует **два типа шагов**:

| Тип | Интерфейс | Когда выполняется | Пример |
|-----|-----------|-------------------|--------|
| **Pre-execution** | `IPreExecutionStep` | До запуска тестов (сканирование, загрузка рецептов) | `ScanBarcodeStep`, `BlockBoilerAdapterStep` |
| **Test step** | `ITestStep` | Во время тестирования (из Excel карт) | `MeasureVoltageStep` |

```
┌─────────────────────────────────────────────────────────────┐
│  ЗАПУСК ПРИЛОЖЕНИЯ                                          │
└─────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│  PRE-EXECUTION STEPS (последовательно)                      │
│  ├─ ScanBarcodeStep         → сканирование штрихкода        │
│  ├─ ResolveTestMapsStep     → проверка известных шагов      │
│  ├─ ValidateRecipesStep     → проверка рецептов             │
│  ├─ InitializeDatabaseStep  → инициализация БД              │
│  ├─ WriteRecipesToPlcStep   → запись рецептов в PLC         │
│  ├─ InitializeRecipeProviderStep                            │
│  └─ BlockBoilerAdapterStep  → ожидание сигнала от PLC       │
└─────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│  TEST STEPS (из Excel карт)                                 │
│  ├─ Шаг 1 из TestMap                                        │
│  ├─ Шаг 2 из TestMap                                        │
│  └─ ...                                                     │
└─────────────────────────────────────────────────────────────┘
```

---

## Часть 1: Pre-Execution Steps

### 1.1 Интерфейс IPreExecutionStep

**Путь:** `Services/Steps/Infrastructure/Interfaces/PreExecution/IPreExecutionStep.cs`

```csharp
public interface IPreExecutionStep
{
    string Id { get; }                    // Уникальный ID (kebab-case)
    string Name { get; }                  // Отображаемое имя
    string Description { get; }           // Описание на русском
    bool IsVisibleInStatusGrid { get; }   // Показывать в сетке статуса?

    Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct);
}
```

### 1.2 Контекст PreExecutionContext

**Путь:** `Services/Steps/Infrastructure/Interfaces/PreExecution/PreExecutionContext.cs`

```csharp
public class PreExecutionContext
{
    public required string Barcode { get; init; }              // Отсканированный баркод
    public required BoilerState BoilerState { get; init; }     // Состояние котла
    public required PausableOpcUaTagService OpcUa { get; init; } // OPC UA сервис
    public required ITestStepLogger TestStepLogger { get; init; } // Логирование в UI

    // Заполняются по мере выполнения шагов:
    public Guid? ScanStepId { get; set; }
    public List<RawTestMap>? RawMaps { get; set; }  // После ScanBarcodeStep
    public List<TestMap>? Maps { get; set; }        // После ResolveTestMapsStep
}
```

### 1.3 Результат PreExecutionResult

**Путь:** `Services/Steps/Infrastructure/Interfaces/PreExecution/PreExecutionResult.cs`

```csharp
public record PreExecutionResult
{
    public PreExecutionStatus Status { get; init; }  // Continue, TestStarted, Cancelled, Failed
    public string? ErrorMessage { get; init; }       // Техническое сообщение (в лог)
    public string? UserMessage { get; init; }        // Сообщение пользователю
    public string? SuccessMessage { get; init; }     // Сообщение об успехе
    public bool IsRetryable { get; init; }           // Можно повторить?
    public bool CanSkip { get; init; }               // Можно пропустить?
    public IReadOnlyList<ErrorDefinition>? Errors { get; init; } // Ошибки для ActiveErrors
}
```

**Фабричные методы:**

```csharp
// Успех — продолжаем следующий шаг
PreExecutionResult.Continue();
PreExecutionResult.Continue("Штрихкод прочитан");

// Тесты запущены — конец pre-execution
PreExecutionResult.TestStarted();

// Ошибка без возможности повтора
PreExecutionResult.Fail("Критическая ошибка");
PreExecutionResult.Fail("Техническая ошибка", userMessage: "Ошибка связи с ПЛК");

// Ошибка с возможностью повтора (показывает диалог Retry/Skip/Cancel)
PreExecutionResult.FailRetryable(
    error: "Ошибка блокировки",
    canSkip: false,                    // Можно ли пропустить
    userMessage: "Повторите попытку",  // Показывается в диалоге
    errors: []);                       // Ошибки для ActiveErrors (красная панель)
```

### 1.4 Дополнительные интерфейсы

| Интерфейс | Путь | Назначение |
|-----------|------|------------|
| `IHasPlcBlockPath` | `Interfaces/Plc/` | Указывает PLC блок шага |
| `IRequiresPlcTags` | `Interfaces/Plc/` | Список тегов для валидации при старте |

```csharp
public interface IHasPlcBlockPath
{
    string PlcBlockPath { get; }  // Например: "DB_VI.Block_Boiler_Adapter"
}

public interface IRequiresPlcTags
{
    IReadOnlyList<string> RequiredPlcTags { get; }  // Теги для проверки при старте
}
```

### 1.5 Пример: Создание Pre-Execution шага

**Задача:** Создать шаг `BlockBoilerAdapterStep` который:
1. Показывает сообщение оператору
2. Записывает `Start = true` в PLC
3. Ждёт сигнал `End` (успех) или `Error` (ошибка)

**Шаг 1: Создать файл шага**

`Services/Steps/Steps/BlockBoilerAdapterStep.cs`:

```csharp
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class BlockBoilerAdapterStep(
    TagWaiter tagWaiter,
    ExecutionMessageState messageState,
    DualLogger<BlockBoilerAdapterStep> logger)
    : IPreExecutionStep, IHasPlcBlockPath, IRequiresPlcTags
{
    // PLC блок и теги
    private const string BlockPath = "DB_VI.Block_Boiler_Adapter";
    private const string StartTag = $"ns=3;s=\"{BlockPath}\".\"Start\"";
    private const string EndTag = $"ns=3;s=\"{BlockPath}\".\"End\"";
    private const string ErrorTag = $"ns=3;s=\"{BlockPath}\".\"Error\"";

    // IPreExecutionStep
    public string Id => "block-boiler-adapter";
    public string Name => "Block boiler adapter";
    public string Description => "Блокирование адаптера";
    public bool IsVisibleInStatusGrid => true;

    // IHasPlcBlockPath
    public string PlcBlockPath => BlockPath;

    // IRequiresPlcTags — валидация тегов при старте приложения
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    public async Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        // 1. Показать сообщение оператору
        messageState.SetMessage("Подсоедините адаптер к котлу и нажмите \"Блок\"");
        logger.LogInformation("Запуск блокировки адаптера");

        // 2. Записать Start = true
        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return CreateWriteError(writeResult.Error);
        }

        // 3. Ждать End или Error
        return await WaitForCompletionAsync(ct);
    }

    private async Task<PreExecutionResult> WaitForCompletionAsync(CancellationToken ct)
    {
        var waitResult = await tagWaiter.WaitAnyAsync(
            tagWaiter.CreateWaitGroup<BlockResult>()
                .WaitForTrue(EndTag, () => BlockResult.Success, "End")
                .WaitForTrue(ErrorTag, () => BlockResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            BlockResult.Success => HandleSuccess(),
            BlockResult.Error => CreateRetryableError(),
            _ => PreExecutionResult.Fail("Неизвестный результат")
        };
    }

    private PreExecutionResult HandleSuccess()
    {
        logger.LogInformation("Адаптер заблокирован успешно");
        messageState.Clear();
        return PreExecutionResult.Continue();
    }

    private PreExecutionResult CreateRetryableError()
    {
        var error = "Ошибка блокировки адаптера";
        logger.LogWarning("{Error}", error);
        return PreExecutionResult.FailRetryable(
            error,
            canSkip: false,
            userMessage: error,
            errors: []);  // Пустой = не добавлять в ActiveErrors
    }

    private PreExecutionResult CreateWriteError(string error)
    {
        logger.LogError("Ошибка записи Start: {Error}", error);
        return PreExecutionResult.FailRetryable(
            $"Ошибка записи Start: {error}",
            canSkip: false,
            userMessage: "Ошибка связи с ПЛК",
            errors: []);
    }

    private enum BlockResult { Success, Error }
}
```

**Шаг 2: Зарегистрировать в DI**

`Services/DependencyInjection/StepsServiceExtensions.cs`:

```csharp
// В методе AddStepsServices(), секция Pre-execution:
services.AddSingleton<IPreExecutionStep, BlockBoilerAdapterStep>();
```

**Шаг 3: Добавить в порядок выполнения**

`Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionStepRegistry.cs`:

Добавить ID шага в массив `MesStepOrder` и/или `NonMesStepOrder`:

```csharp
private static readonly string[] NonMesStepOrder =
[
    "scan-barcode",
    "validate-barcode",
    "find-boiler-type",
    "load-recipes",
    "load-test-sequence",
    "build-test-maps",
    "save-boiler-state",
    "resolve-test-maps",
    "validate-recipes",
    "initialize-database",
    "write-recipes-to-plc",
    "initialize-recipe-provider",
    "block-boiler-adapter"  // ← добавить сюда
];
```

---

## Часть 2: Test Steps

### 2.1 Интерфейс ITestStep

**Путь:** `Services/Steps/Infrastructure/Interfaces/Test/ITestStep.cs`

```csharp
public interface ITestStep
{
    string Id { get; }                    // Уникальный ID
    string Name { get; }                  // Отображаемое имя
    string Description { get; }           // Описание
    bool IsVisibleInEditor => true;       // Показывать в редакторе TestSequence?

    Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct);
}
```

### 2.2 Контекст TestStepContext

**Путь:** `Services/Steps/Infrastructure/Registrator/TestStepContext.cs`

```csharp
public class TestStepContext
{
    public int ColumnIndex { get; }                    // Индекс столбца в TestMap
    public PausableOpcUaTagService OpcUa { get; }      // OPC UA сервис
    public ILogger Logger { get; }                     // Логирование в файл
    public IRecipeProvider RecipeProvider { get; }     // Доступ к рецептам
    public Dictionary<string, object> Variables { get; } // Локальные переменные
}
```

### 2.3 Результат TestStepResult

**Путь:** `Services/Steps/Infrastructure/Registrator/TestStepResult.cs`

```csharp
public class TestStepResult
{
    public bool Success { get; init; }
    public bool Skipped { get; init; }
    public string Message { get; init; }
    public Dictionary<string, object>? OutputData { get; init; }
    public List<ErrorDefinition>? Errors { get; init; }
}
```

**Фабричные методы:**

```csharp
TestStepResult.Pass();                    // Успех
TestStepResult.Pass("Значение: 220V");    // Успех с сообщением
TestStepResult.Fail("Ошибка измерения");  // Неудача
TestStepResult.Skip();                    // Пропущен
TestStepResult.Skip("Не применимо");      // Пропущен с причиной
```

### 2.4 Регистрация Test Steps

Test steps регистрируются **автоматически** через рефлексию в `TestStepRegistry`:

```csharp
// Services/Steps/Infrastructure/Registrator/TestStepRegistry.cs
private List<ITestStep> LoadSteps()
{
    return Assembly.GetExecutingAssembly()
        .GetTypes()
        .Where(t => typeof(ITestStep).IsAssignableFrom(t)
                    && !t.IsInterface
                    && !t.IsAbstract)
        .Select(type => ActivatorUtilities.CreateInstance(_serviceProvider, type) as ITestStep)
        .Where(s => s != null)
        .OrderBy(s => s.Name)
        .ToList();
}
```

**Важно:** Достаточно создать класс, реализующий `ITestStep` — он будет найден автоматически.

### 2.5 Пример: Создание Test Step

```csharp
// Services/Steps/Steps/MeasureVoltageStep.cs
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class MeasureVoltageStep : ITestStep
{
    public string Id => "measure-voltage";
    public string Name => "Измерение напряжения";
    public string Description => "Измеряет напряжение на выходе";
    public bool IsVisibleInEditor => true;

    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        // Читаем значение из PLC
        var voltage = await context.OpcUa.ReadAsync<float>("ns=3;s=DB_VI.Voltage", ct);

        if (voltage < 200 || voltage > 250)
        {
            return TestStepResult.Fail($"Напряжение вне нормы: {voltage}V");
        }

        return TestStepResult.Pass($"Напряжение: {voltage}V");
    }
}
```

---

## Часть 3: Работа с PLC сигналами

### 3.1 TagWaiter — ожидание сигналов

**Путь:** `Services/OpcUa/TagWaiter.cs`

```csharp
// Инъекция в конструктор
public class MyStep(TagWaiter tagWaiter) { ... }
```

**Простое ожидание:**

```csharp
// Ждать пока тег станет true
await tagWaiter.WaitForTrueAsync("ns=3;s=DB_VI.Ready", ct);

// Ждать пока тег станет false
await tagWaiter.WaitForFalseAsync("ns=3;s=DB_VI.Busy", ct);

// Ждать с timeout
await tagWaiter.WaitForTrueAsync(
    "ns=3;s=DB_VI.Ready",
    timeout: TimeSpan.FromSeconds(10),
    ct);

// Ждать конкретного значения
var value = await tagWaiter.WaitForValueAsync<float>(
    "ns=3;s=DB_VI.Temperature",
    condition: v => v >= 50.0f,  // Ждать пока >= 50
    timeout: TimeSpan.FromSeconds(30),
    ct);
```

### 3.2 WaitGroupBuilder — ожидание первого из нескольких

Когда нужно ждать **один из нескольких** сигналов (кто первый — тот и победил):

```csharp
// Создаём группу условий с результатом
var waitResult = await tagWaiter.WaitAnyAsync(
    tagWaiter.CreateWaitGroup<MyResult>()
        .WaitForTrue(endTag, () => MyResult.Success, "End")
        .WaitForTrue(errorTag, () => MyResult.Error, "Error")
        .WaitForTrue(cancelTag, () => MyResult.Cancel, "Cancel")
        .WithTimeout(TimeSpan.FromSeconds(60)),  // Опционально
    ct);

// Результат
waitResult.Result;      // MyResult.Success / Error / Cancel
waitResult.NodeId;      // Какой тег сработал
waitResult.Name;        // "End" / "Error" / "Cancel"
waitResult.WinnerIndex; // 0 / 1 / 2
```

**Полный пример:**

```csharp
private enum BlockResult { Success, Error }

private async Task<PreExecutionResult> WaitForCompletionAsync(CancellationToken ct)
{
    var waitResult = await tagWaiter.WaitAnyAsync(
        tagWaiter.CreateWaitGroup<BlockResult>()
            .WaitForTrue(EndTag, () => BlockResult.Success, "End")
            .WaitForTrue(ErrorTag, () => BlockResult.Error, "Error"),
        ct);

    return waitResult.Result switch
    {
        BlockResult.Success => PreExecutionResult.Continue(),
        BlockResult.Error => PreExecutionResult.FailRetryable("Ошибка"),
        _ => PreExecutionResult.Fail("Неизвестный результат")
    };
}
```

### 3.3 Чтение и запись тегов

```csharp
// Запись
var writeResult = await context.OpcUa.WriteAsync("ns=3;s=DB_VI.Start", true, ct);
if (writeResult.Error != null)
{
    // Ошибка записи
}

// Чтение
var value = await context.OpcUa.ReadAsync<float>("ns=3;s=DB_VI.Value", ct);

// Запись разных типов
await context.OpcUa.WriteAsync(tag, (float)123.45, ct);   // REAL
await context.OpcUa.WriteAsync(tag, (short)100, ct);      // INT16
await context.OpcUa.WriteAsync(tag, 1000, ct);            // DINT
await context.OpcUa.WriteAsync(tag, true, ct);            // BOOL
await context.OpcUa.WriteAsync(tag, "text", ct);          // STRING
```

---

## Часть 4: Чек-лист создания шага

### Pre-Execution Step

- [ ] Создать файл `Services/Steps/Steps/МойШаг.cs`
- [ ] Реализовать `IPreExecutionStep`
- [ ] Добавить `IHasPlcBlockPath` если есть PLC блок
- [ ] Добавить `IRequiresPlcTags` если нужна валидация тегов
- [ ] Зарегистрировать в `StepsServiceExtensions.cs`
- [ ] Добавить в порядок в `PreExecutionStepRegistry.cs`
- [ ] Протестировать

### Test Step

- [ ] Создать файл `Services/Steps/Steps/МойШаг.cs`
- [ ] Реализовать `ITestStep`
- [ ] Шаг автоматически зарегистрируется через рефлексию
- [ ] Протестировать

---

## Часть 5: Справочник путей

| Компонент | Путь |
|-----------|------|
| **Интерфейсы** | |
| IPreExecutionStep | `Services/Steps/Infrastructure/Interfaces/PreExecution/IPreExecutionStep.cs` |
| ITestStep | `Services/Steps/Infrastructure/Interfaces/Test/ITestStep.cs` |
| IHasPlcBlockPath | `Services/Steps/Infrastructure/Interfaces/Plc/IHasPlcBlockPath.cs` |
| IRequiresPlcTags | `Services/Steps/Infrastructure/Interfaces/Plc/IRequiresPlcTags.cs` |
| **Контексты** | |
| PreExecutionContext | `Services/Steps/Infrastructure/Interfaces/PreExecution/PreExecutionContext.cs` |
| TestStepContext | `Services/Steps/Infrastructure/Registrator/TestStepContext.cs` |
| **Результаты** | |
| PreExecutionResult | `Services/Steps/Infrastructure/Interfaces/PreExecution/PreExecutionResult.cs` |
| TestStepResult | `Services/Steps/Infrastructure/Registrator/TestStepResult.cs` |
| **Реестры** | |
| PreExecutionStepRegistry | `Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionStepRegistry.cs` |
| TestStepRegistry | `Services/Steps/Infrastructure/Registrator/TestStepRegistry.cs` |
| **DI** | |
| StepsServiceExtensions | `Services/DependencyInjection/StepsServiceExtensions.cs` |
| **Ожидание сигналов** | |
| TagWaiter | `Services/OpcUa/TagWaiter.cs` |
| WaitGroupBuilder | `Services/OpcUa/WaitGroup/WaitGroupBuilder.cs` |
| **Примеры шагов** | |
| ScanBarcodeStep | `Services/Steps/Steps/ScanBarcodeStep.cs` |
| BlockBoilerAdapterStep | `Services/Steps/Steps/BlockBoilerAdapterStep.cs` |
| WriteRecipesToPlcStep | `Services/Steps/Steps/WriteRecipesToPlcStep.cs` |

---

## Часть 6: Типичные ошибки

### 1. Забыли зарегистрировать Pre-Execution шаг

```
Шаг не выполняется → проверить StepsServiceExtensions.cs
```

### 2. Забыли добавить в порядок выполнения

```
Шаг зарегистрирован, но не выполняется → проверить PreExecutionStepRegistry.cs
```

### 3. Неправильный формат тега OPC UA

```csharp
// ❌ НЕПРАВИЛЬНО
private const string Tag = "DB_VI.Block.Start";

// ✅ ПРАВИЛЬНО
private const string Tag = "ns=3;s=\"DB_VI\".\"Block\".\"Start\"";
```

### 4. Блокирующий вызов вместо async

```csharp
// ❌ НЕПРАВИЛЬНО — блокирует поток
var result = tagWaiter.WaitForTrueAsync(tag, ct).Result;

// ✅ ПРАВИЛЬНО
var result = await tagWaiter.WaitForTrueAsync(tag, ct);
```

### 5. Не очищается сообщение после успеха

```csharp
// ❌ НЕПРАВИЛЬНО — сообщение остаётся висеть
private PreExecutionResult HandleSuccess()
{
    return PreExecutionResult.Continue();
}

// ✅ ПРАВИЛЬНО
private PreExecutionResult HandleSuccess()
{
    messageState.Clear();  // Очищаем!
    return PreExecutionResult.Continue();
}
```