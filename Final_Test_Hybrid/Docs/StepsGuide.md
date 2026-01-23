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
| `IRequiresRecipes` | `Interfaces/Recipe/` | Список рецептов для валидации перед тестом |

```csharp
public interface IHasPlcBlockPath
{
    string PlcBlockPath { get; }  // Например: "DB_VI.Block_Boiler_Adapter"
}

public interface IRequiresPlcTags
{
    IReadOnlyList<string> RequiredPlcTags { get; }  // Теги для проверки при старте
}

public interface IRequiresRecipes : ITestStep
{
    IReadOnlyList<string> RequiredRecipeAddresses { get; }  // Рецепты для валидации перед тестом
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
    public int ColumnIndex { get; }                      // Индекс столбца в TestMap
    public PausableOpcUaTagService OpcUa { get; }        // OPC UA сервис (паузится при Auto OFF)
    public ILogger Logger { get; }                       // Логирование в файл
    public IRecipeProvider RecipeProvider { get; }       // Доступ к рецептам
    public Dictionary<string, object> Variables { get; } // Локальные переменные
    public PauseTokenSource PauseToken { get; }          // Токен паузы
    public PausableRegisterReader DiagReader { get; }    // Modbus чтение (паузится)
    public PausableRegisterWriter DiagWriter { get; }    // Modbus запись (паузится)
    public PausableTagWaiter TagWaiter { get; }          // Ожидание PLC сигналов (паузится)

    // Pausable версия Task.Delay — останавливается при Auto OFF
    public Task DelayAsync(TimeSpan delay, CancellationToken ct);
}
```

**Важно:** Все `Pausable*` сервисы автоматически приостанавливаются при выключении Auto режима.

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

### 2.5 Пример: Простой Test Step

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

### 2.6 Пример: Test Step с ожиданием PLC сигналов

Для шагов, которые взаимодействуют с PLC блоками (Start/End/Error), используйте `context.TagWaiter`:

```csharp
// Services/Steps/Steps/CH/FlushCircuitStep.cs
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.CH;

public class FlushCircuitStep(
    DualLogger<FlushCircuitStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags
{
    private const string BlockPath = "DB_VI.CH.Flush_Circuit";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"CH\".\"Flush_Circuit\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"CH\".\"Flush_Circuit\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"CH\".\"Flush_Circuit\".\"Error\"";

    public string Id => "ch-flush-circuit";
    public string Name => "CH/Flush_Circuit";
    public string Description => "Контур Отопления. Продувка контура.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск продувки контура отопления");

        // 1. Записать Start = true
        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        // 2. Ждать End или Error через context.TagWaiter
        return await WaitForCompletionAsync(context, ct);
    }

    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<FlushResult>()
                .WaitForTrue(EndTag, () => FlushResult.Success, "End")
                .WaitForTrue(ErrorTag, () => FlushResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            FlushResult.Success => await HandleSuccessAsync(context, ct),
            FlushResult.Error => TestStepResult.Fail("Ошибка продувки контура"),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Продувка контура завершена успешно");

        // 3. Сбросить Start = false (с ct для корректной отмены при shutdown)
        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null
            ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}")
            : TestStepResult.Pass();
    }

    private enum FlushResult { Success, Error }
}
```

**Ключевые моменты:**
- `context.TagWaiter` — pausable версия, автоматически приостанавливается при Auto OFF
- `IHasPlcBlockPath` — для установки Selected тега при ошибках
- `IRequiresPlcTags` — валидация тегов при старте приложения
- **Всегда передавайте `ct` в `HandleSuccessAsync`** — для корректной отмены записи `Start=false` при shutdown/stop

### 2.7 Пример: Test Step с валидацией и сохранением результата

Шаги, которые измеряют значения и сравнивают с порогами из рецептов, используют:
- `IRequiresRecipes` — валидация рецептов перед тестом
- `ITestResultsService` — сохранение результата измерения для MES

```csharp
// Services/Steps/Steps/CH/SlowFillCircuitStep.cs
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.CH;

/// <summary>
/// Тестовый шаг медленного заполнения контура с измерением и валидацией давления.
/// </summary>
public class SlowFillCircuitStep(
    DualLogger<SlowFillCircuitStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcTags, IRequiresRecipes
{
    private const string BlockPath = "DB_VI.CH.Slow_Fill_Circuit";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"CH\".\"Slow_Fill_Circuit\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"CH\".\"Slow_Fill_Circuit\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"CH\".\"Slow_Fill_Circuit\".\"Error\"";
    private const string FlowPressTag = "ns=3;s=\"DB_Parameter\".\"CH\".\"Flow_Press\"";
    private const string PressTestValueRecipe = "ns=3;s=\"DB_Recipe\".\"CH\".\"PresTestValue\"";

    public string Id => "ch-slow-fill-circuit";
    public string Name => "CH/Slow_Fill_Circuit";
    public string Description => "Контур Отопления. Медленное заполнение контура.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, FlowPressTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [PressTestValueRecipe];

    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск медленного заполнения контура");

        // 1. Удалить предыдущий результат (при повторном входе в шаг)
        testResultsService.Remove("CH_Flow_Press");

        // 2. Записать Start = true
        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        // 3. Ждать End или Error
        return await WaitForCompletionAsync(context, ct);
    }

    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<FillResult>()
                .WaitForTrue(EndTag, () => FillResult.Success, "End")
                .WaitForTrue(ErrorTag, () => FillResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            FillResult.Success => await HandleCompletionAsync(context, isSuccess: true, ct),
            FillResult.Error => await HandleCompletionAsync(context, isSuccess: false, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    private async Task<TestStepResult> HandleCompletionAsync(
        TestStepContext context, bool isSuccess, CancellationToken ct)
    {
        // 4. Прочитать измеренное значение из PLC
        var readResult = await context.OpcUa.ReadAsync<float>(FlowPressTag, ct);
        var flowPress = readResult.Value;

        // 5. Сбросить Start = false
        var resetResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        if (resetResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка сброса Start: {resetResult.Error}");
        }

        // 6. Получить порог из рецепта и сравнить
        var pressTestValue = context.RecipeProvider.GetValue<float>(PressTestValueRecipe)!.Value;
        var status = flowPress >= pressTestValue ? 1 : 2;  // 1 = OK, 2 = NOK

        // 7. Сохранить результат в TestResultsService (для MES)
        testResultsService.Add(
            parameterName: "CH_Flow_Press",
            value: $"{flowPress:F3}",
            min: $"{pressTestValue:F3}",
            max: "",
            status: status,
            isRanged: false,
            unit: "");

        logger.LogInformation("Давление: {FlowPress:F3}, порог: {Threshold:F3}, статус: {Status}",
            flowPress, pressTestValue, status == 1 ? "OK" : "NOK");

        // 8. Результат шага зависит ТОЛЬКО от End/Error PLC, НЕ от сравнения!
        if (isSuccess)
        {
            logger.LogInformation("Медленное заполнение завершено успешно");
            return TestStepResult.Pass();
        }

        return TestStepResult.Fail("Ошибка медленного заполнения контура");
    }

    private enum FillResult { Success, Error }
}
```

**Ключевые моменты:**
- `IRequiresRecipes` — система проверяет наличие рецептов перед запуском теста
- `testResultsService.Remove()` — очистка предыдущего результата при повторном входе
- `testResultsService.Add()` — сохранение результата измерения для MES
- `context.RecipeProvider.GetValue<T>()` — получение значения рецепта
- **Результат шага (Pass/Fail) определяется только PLC сигналами**, результат сравнения влияет только на статус в MES

### 2.8 IProvideLimits — предзагрузка пределов в грид

**Путь:** `Services/Steps/Infrastructure/Interfaces/Limits/IProvideLimits.cs`

Интерфейс для шагов, которые хотят показывать пределы в гриде **сразу при старте** (до завершения выполнения).

```csharp
/// <summary>
/// ВАЖНО: GetLimits вызывается параллельно из 4 колонок.
/// Реализация ДОЛЖНА быть thread-safe и pure (без side-effects).
/// НЕ делать IO/PLC операции - только in-memory (рецепты).
/// </summary>
public interface IProvideLimits : ITestStep
{
    /// <summary>
    /// Получает пределы для отображения в гриде.
    /// Вызывается ПЕРЕД выполнением шага.
    /// </summary>
    string? GetLimits(LimitsContext context);
}

public class LimitsContext
{
    public required int ColumnIndex { get; init; }
    public required IRecipeProvider RecipeProvider { get; init; }
}
```

**Как это работает:**

```
┌─────────────────────────────────────────────────────────────────┐
│  ColumnExecutor.StartNewStep(step)                              │
├─────────────────────────────────────────────────────────────────┤
│  1. step is IProvideLimits? ────► GetLimits(context) ──► limits │
│  2. ReportStepStarted(step, limits) ──► Грид показывает limits  │
│  3. ExecuteAsync(context, ct) ──► Шаг выполняется               │
│  4. SetSuccess/SetError(limits: null) ──► Limits НЕ меняются    │
│     SetSuccess/SetError(limits: "X")  ──► Limits переопределены │
└─────────────────────────────────────────────────────────────────┘
```

**Пример: шаг с предзагрузкой пределов**

```csharp
public class PressureTestStep(
    DualLogger<PressureTestStep> logger,
    ITestResultsService testResultsService) : ITestStep, IProvideLimits, IRequiresRecipes
{
    private const string PressureMinRecipe = "ns=3;s=\"DB_Recipe\".\"Pressure_Min\"";
    private const string PressureMaxRecipe = "ns=3;s=\"DB_Recipe\".\"Pressure_Max\"";

    public string Id => "pressure-test";
    public string Name => "Тест давления";
    public string Description => "Измерение и проверка давления";
    public IReadOnlyList<string> RequiredRecipeAddresses => [PressureMinRecipe, PressureMaxRecipe];

    /// <summary>
    /// Возвращает пределы из рецептов для отображения в гриде.
    /// Вызывается ПЕРЕД ExecuteAsync, параллельно из 4 колонок.
    /// ДОЛЖЕН быть thread-safe и pure (только чтение из RecipeProvider).
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        var min = context.RecipeProvider.GetValue<float>(PressureMinRecipe);
        var max = context.RecipeProvider.GetValue<float>(PressureMaxRecipe);
        return min != null && max != null
            ? $"{min:F2} - {max:F2} bar"
            : null;
    }

    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        testResultsService.Remove("Pressure");

        var pressure = await MeasurePressureAsync(context, ct);
        var min = context.RecipeProvider.GetValue<float>(PressureMinRecipe)!.Value;
        var max = context.RecipeProvider.GetValue<float>(PressureMaxRecipe)!.Value;

        var status = (pressure >= min && pressure <= max) ? 1 : 2;
        testResultsService.Add("Pressure", $"{pressure:F2}", $"{min:F2}", $"{max:F2}", status, true, "bar");

        logger.LogInformation("Давление: {Pressure:F2} bar, пределы: {Min:F2} - {Max:F2}",
            pressure, min, max);

        // Возвращаем результат БЕЗ limits — предзаданные из GetLimits() сохранятся
        return status == 1
            ? TestStepResult.Pass($"{pressure:F2} bar")
            : TestStepResult.Fail($"{pressure:F2} bar (вне пределов)");
    }
}
```

**Ключевые моменты:**

| Аспект | Требование |
|--------|------------|
| Thread-safety | `GetLimits` вызывается из 4 колонок параллельно |
| Pure function | Без side-effects, только чтение из `RecipeProvider` |
| Нет IO | НЕ делать PLC/Modbus/HTTP запросы |
| Nullable | Возвращать `null` если пределы недоступны |

**Когда использовать:**

- ✅ Шаги с измерениями и пределами из рецептов
- ✅ Шаги где пределы известны до выполнения
- ❌ Шаги где пределы вычисляются во время выполнения
- ❌ Шаги без пределов (не реализовывать интерфейс)

**Поведение при Retry:**

При повторном выполнении шага (Retry) пределы из `GetLimits()` сохраняются — `SetRunning()` не сбрасывает `Range`.

---

## Часть 3: Работа с PLC сигналами

### 3.1 TagWaiter vs PausableTagWaiter

| Сервис | Использование | Паузится при Auto OFF |
|--------|---------------|----------------------|
| `TagWaiter` | Pre-execution шаги (инъекция в конструктор) | Нет |
| `PausableTagWaiter` | Test шаги (через `context.TagWaiter`) | Да |

**Путь:** `Services/OpcUa/TagWaiter.cs`, `Services/OpcUa/PausableTagWaiter.cs`

```csharp
// Pre-execution шаги: инъекция TagWaiter или PausableTagWaiter в конструктор
public class MyPreExecutionStep(PausableTagWaiter tagWaiter) { ... }

// Test шаги: использовать context.TagWaiter
public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
{
    await context.TagWaiter.WaitForTrueAsync(EndTag, ct: ct);
}
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
- [ ] Добавить `IProvideLimits` если нужны пределы в гриде до выполнения
- [ ] Добавить `IRequiresRecipes` если шаг использует рецепты
- [ ] Добавить `IHasPlcBlockPath` если есть PLC блок
- [ ] Добавить `IRequiresPlcTags` если нужна валидация тегов
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
| IRequiresRecipes | `Services/Steps/Infrastructure/Interfaces/Recipe/IRequiresRecipes.cs` |
| IProvideLimits | `Services/Steps/Infrastructure/Interfaces/Limits/IProvideLimits.cs` |
| LimitsContext | `Services/Steps/Infrastructure/Interfaces/Limits/LimitsContext.cs` |
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
| PausableTagWaiter | `Services/OpcUa/PausableTagWaiter.cs` |
| WaitGroupBuilder | `Services/OpcUa/WaitGroup/WaitGroupBuilder.cs` |
| **Результаты тестов (MES)** | |
| ITestResultsService | `Services/Results/ITestResultsService.cs` |
| TestResultsService | `Services/Results/TestResultsService.cs` |
| **Примеры шагов** | |
| ScanBarcodeStep | `Services/Steps/Steps/ScanBarcodeStep.cs` |
| BlockBoilerAdapterStep | `Services/Steps/Steps/BlockBoilerAdapterStep.cs` |
| WriteRecipesToPlcStep | `Services/Steps/Steps/WriteRecipesToPlcStep.cs` |
| FlushCircuitStep | `Services/Steps/Steps/CH/FlushCircuitStep.cs` |
| SlowFillCircuitStep | `Services/Steps/Steps/CH/SlowFillCircuitStep.cs` |

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

### 6. Дублирование результатов при повторном входе в шаг

```csharp
// ❌ НЕПРАВИЛЬНО — при Retry появятся дубликаты
public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
{
    // ... выполнение ...
    testResultsService.Add("CH_Flow_Press", value, ...);
}

// ✅ ПРАВИЛЬНО — удаляем старый результат перед добавлением
public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
{
    testResultsService.Remove("CH_Flow_Press");  // Очищаем!
    // ... выполнение ...
    testResultsService.Add("CH_Flow_Press", value, ...);
}
```

### 7. IO операции в GetLimits (IProvideLimits)

```csharp
// ❌ НЕПРАВИЛЬНО — GetLimits вызывается параллельно из 4 колонок!
public string? GetLimits(LimitsContext context)
{
    // IO операции НЕ thread-safe и блокируют все колонки
    var value = await plcService.ReadAsync<float>(tag);  // ЗАПРЕЩЕНО!
    return $"{value:F2}";
}

// ❌ НЕПРАВИЛЬНО — side-effects нарушают pure function
public string? GetLimits(LimitsContext context)
{
    _counter++;  // Side-effect!
    logger.LogInformation("GetLimits called");  // Side-effect!
    return "...";
}

// ✅ ПРАВИЛЬНО — только чтение из RecipeProvider (in-memory, thread-safe)
public string? GetLimits(LimitsContext context)
{
    var min = context.RecipeProvider.GetValue<float>(MinRecipe);
    var max = context.RecipeProvider.GetValue<float>(MaxRecipe);
    return min != null && max != null ? $"{min:F2} - {max:F2}" : null;
}
```

---

## Часть 7: Контракт CancellationToken

> **КРИТИЧЕСКИ ВАЖНО:** Шаги несут полную ответственность за корректную обработку отмены.
> Система НЕ имеет защиты от зависших шагов — если шаг не реагирует на `CancellationToken`, система зависнет.

### 7.1 Почему это важно

| Сценарий | Без проверки ct | С проверкой ct |
|----------|-----------------|----------------|
| Оператор нажал "Стоп" | Система зависла | Корректная остановка |
| PLC Reset | Система зависла | Корректная остановка |
| Выход из приложения | Приложение не закрывается | Быстрый выход |

### 7.2 Обязательные правила

#### ✅ В циклах — проверять ct

```csharp
while (condition)
{
    ct.ThrowIfCancellationRequested();  // ← ОБЯЗАТЕЛЬНО
    // работа
}

for (int i = 0; i < count; i++)
{
    ct.ThrowIfCancellationRequested();  // ← ОБЯЗАТЕЛЬНО
    // работа
}
```

#### ✅ Для задержек — использовать context.DelayAsync или передавать ct

```csharp
// Test steps — использовать context.DelayAsync (pause-aware)
await context.DelayAsync(TimeSpan.FromSeconds(5), ct);

// Pre-execution steps — передавать ct в Task.Delay
await Task.Delay(TimeSpan.FromSeconds(5), ct);
```

#### ✅ I/O операции — всегда передавать ct

```csharp
// OPC-UA
await context.OpcUa.WriteAsync(tag, value, ct);
await context.OpcUa.ReadAsync<float>(tag, ct);

// TagWaiter
await context.TagWaiter.WaitForTrueAsync(tag, ct);
await context.TagWaiter.WaitAnyAsync(group, ct);
```

### 7.3 Запрещённые паттерны

```csharp
// ❌ ЗАПРЕЩЕНО — блокирует поток, игнорирует отмену
var result = someTask.Result;
someTask.Wait();

// ❌ ЗАПРЕЩЕНО — не реагирует на отмену
await Task.Delay(TimeSpan.FromSeconds(30));  // без ct!
Thread.Sleep(5000);

// ❌ ЗАПРЕЩЕНО — бесконечный цикл без проверки
while (true)
{
    await Task.Delay(100);  // Никогда не выйдет при отмене
}
```

### 7.4 Правильные примеры

```csharp
// ✅ ПРАВИЛЬНО — полный пример шага
public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
{
    // 1. Запись в PLC с ct
    var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
    if (writeResult.Error != null)
        return TestStepResult.Fail(writeResult.Error);

    // 2. Ожидание сигнала с ct
    var waitResult = await context.TagWaiter.WaitAnyAsync(
        context.TagWaiter.CreateWaitGroup<MyResult>()
            .WaitForTrue(EndTag, () => MyResult.Success, "End")
            .WaitForTrue(ErrorTag, () => MyResult.Error, "Error"),
        ct);  // ← ct передан

    // 3. Обработка результата
    return waitResult.Result switch
    {
        MyResult.Success => TestStepResult.Pass(),
        MyResult.Error => TestStepResult.Fail("Ошибка"),
        _ => TestStepResult.Fail("Неизвестный результат")
    };
}
```

### 7.5 Code Review чеклист

При ревью новых шагов проверять:

- [ ] Все `while`/`for` циклы содержат `ct.ThrowIfCancellationRequested()`
- [ ] Все задержки используют `context.DelayAsync()` или `Task.Delay(..., ct)`
- [ ] Все I/O операции передают `ct`
- [ ] Нет `.Result` или `.Wait()` вызовов
- [ ] Нет `Thread.Sleep()`