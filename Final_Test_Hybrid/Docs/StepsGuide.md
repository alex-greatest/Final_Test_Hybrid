# StepsGuide.md

Инструкция по созданию шагов (Steps) в проекте Final_Test_Hybrid.

> **См. также:** [CLAUDE.md](../CLAUDE.md), [ErrorSystemGuide.md](ErrorSystemGuide.md), [DiagnosticGuide.md](DiagnosticGuide.md)

---

## Обзор архитектуры

| Тип | Интерфейс | Когда выполняется | Пример |
|-----|-----------|-------------------|--------|
| **Pre-execution** | `IPreExecutionStep` | До тестов (сканирование, рецепты) | `ScanBarcodeStep` |
| **Test step** | `ITestStep` | Во время тестирования (из Excel) | `MeasureVoltageStep` |

```
PRE-EXECUTION STEPS (последовательно)
├─ ScanBarcodeStep → ValidateRecipesStep → BlockBoilerAdapterStep
└─ ... → StartTestExecution()

TEST STEPS (из Excel карт, 4 колонки параллельно)
├─ Шаг 1..N из TestMap
└─ OnSequenceCompleted → HandleTestCompleted()
```

---

## Часть 1: Pre-Execution Steps

### 1.1 Интерфейс

```csharp
public interface IPreExecutionStep
{
    string Id { get; }                    // kebab-case
    string Name { get; }
    string Description { get; }
    bool IsVisibleInStatusGrid { get; }

    Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct);
}
```

### 1.2 Контекст и результат

```csharp
// PreExecutionContext
context.Barcode          // Отсканированный баркод
context.BoilerState      // Состояние котла
context.OpcUa            // PausableOpcUaTagService
context.TestStepLogger   // Логирование в UI

// PreExecutionResult — фабричные методы
PreExecutionResult.Continue();                    // Успех
PreExecutionResult.TestStarted();                 // Тесты запущены
PreExecutionResult.Fail("Ошибка");                // Критическая ошибка
PreExecutionResult.FailRetryable(error, canSkip: false, userMessage: "...", errors: []);
```

### 1.3 Регистрация

1. DI: `services.AddSingleton<IPreExecutionStep, MyStep>();` в `StepsServiceExtensions.cs`
2. Порядок: добавить ID в `PreExecutionStepRegistry.cs` → `NonMesStepOrder` / `MesStepOrder`

---

## Часть 2: Test Steps

### 2.1 Интерфейс

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

### 2.2 Контекст TestStepContext

```csharp
context.ColumnIndex       // Индекс столбца (0-3)
context.OpcUa             // PausableOpcUaTagService (паузится при Auto OFF)
context.TagWaiter         // PausableTagWaiter
context.DiagReader        // PausableRegisterReader (Modbus)
context.DiagWriter        // PausableRegisterWriter (Modbus)
context.RecipeProvider    // Доступ к рецептам
context.Variables         // Локальные переменные шага
context.PauseToken        // Токен паузы

// Pause-aware задержка
await context.DelayAsync(TimeSpan.FromMilliseconds(100), ct);

// Промежуточные результаты (обновляют UI без завершения шага)
context.ReportProgress("Инициализация...");
```

### 2.2.1 Промежуточные результаты (ReportProgress)

Шаги могут сообщать о прогрессе выполнения через `context.ReportProgress()`. Сообщение отображается в колонке "Результаты" пока шаг выполняется.

```csharp
public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
{
    context.ReportProgress("Инициализация...");
    await InitializeAsync(ct);

    context.ReportProgress("Чтение данных...");
    var data = await ReadDataAsync(ct);

    context.ReportProgress("Проверка...");
    var isValid = ValidateData(data);

    // Финальный результат заменяет ProgressMessage
    return isValid
        ? TestStepResult.Pass($"Значение: {data.Value}")
        : TestStepResult.Fail("Данные невалидны");
}
```

**Поведение:**
- Показывается в UI пока `StepStatus == Running`
- После завершения (Pass/Fail) показывается `Result`
- Автоматически очищается при завершении, retry и skip
- Потокобезопасен — защита от race condition через `Volatile` и проверку статуса

**Ограничения:**
- НЕ вызывать после завершения шага (после return)
- НЕ вызывать из фоновых задач, переживающих шаг

### 2.3 Результат TestStepResult

```csharp
TestStepResult.Pass();                              // Успех
TestStepResult.Pass("220V");                        // Успех с сообщением
TestStepResult.Fail("Ошибка");                      // Неудача (Skip разрешён)
TestStepResult.Fail("Ошибка", canSkip: false);      // Неудача (Skip запрещён)
TestStepResult.Fail("Ошибка", errors: [ErrorDef]);  // С ошибками для ActiveErrors
TestStepResult.Skip("Не применимо");                // Пропущен
```

### 2.4 Регистрация

Test steps регистрируются **автоматически** через рефлексию — достаточно создать класс с `ITestStep`.

---

## Часть 3: Дополнительные интерфейсы

| Интерфейс | Назначение |
|-----------|------------|
| `IHasPlcBlockPath` | PLC блок шага (для Selected тега при ошибках) |
| `IRequiresPlcTags` | Теги для валидации при старте |
| `IRequiresRecipes` | Рецепты для валидации перед тестом |
| `INonSkippable` | Запрет пропуска (даже при исключениях) |
| `IProvideLimits` | Пределы в гриде до выполнения |

```csharp
// INonSkippable — маркерный интерфейс
public class CriticalStep : ITestStep, INonSkippable { }

// IProvideLimits — thread-safe, pure, без IO
public string? GetLimits(LimitsContext context)
{
    var min = context.RecipeProvider.GetValue<float>(MinRecipe);
    return min != null ? $">= {min:F2}" : null;
}
```

---

## Часть 4: Работа с PLC (OPC-UA)

### 4.1 TagWaiter

| Сервис | Использование | Паузится |
|--------|---------------|----------|
| `TagWaiter` | Pre-execution (инъекция) | Нет |
| `PausableTagWaiter` | Test steps (`context.TagWaiter`) | Да |

```csharp
// Простое ожидание
await context.TagWaiter.WaitForTrueAsync(EndTag, ct);
await context.TagWaiter.WaitForFalseAsync(BusyTag, timeout: TimeSpan.FromSeconds(10), ct);

// Ожидание первого из нескольких
var result = await context.TagWaiter.WaitAnyAsync(
    context.TagWaiter.CreateWaitGroup<MyResult>()
        .WaitForTrue(EndTag, () => MyResult.Success, "End")
        .WaitForTrue(ErrorTag, () => MyResult.Error, "Error"),
    ct);

return result.Result switch
{
    MyResult.Success => TestStepResult.Pass(),
    MyResult.Error => TestStepResult.Fail("Ошибка"),
    _ => TestStepResult.Fail("Неизвестный результат")
};
```

### 4.2 Чтение/запись тегов

```csharp
var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
if (writeResult.Error != null)
    return TestStepResult.Fail($"Ошибка записи: {writeResult.Error}");

var value = await context.OpcUa.ReadAsync<float>(ValueTag, ct);
```

---

## Часть 5: Работа с Modbus (Диагностика)

### 5.1 Сервисы

| Сервис | Использование | Паузится |
|--------|---------------|----------|
| `RegisterReader/Writer` | Системные операции | Нет |
| `PausableRegisterReader/Writer` | Test steps (`context.DiagReader/Writer`) | Да |

```csharp
// Чтение регистров
var result = await context.DiagReader.ReadUInt32Async(modbusAddress, ct);
if (!result.Success)
    return TestStepResult.Fail($"Ошибка чтения: {result.Error}");

// Запись регистров
var writeResult = await context.DiagWriter.WriteUInt32Async(address, value, ct);
```

### 5.2 Задержка перед чтением (WriteVerifyDelayMs)

При записи значения в ECU и последующей верификации чтением, ECU может потребоваться время для применения изменений.

**Настройка:** `DiagnosticSettings.WriteVerifyDelayMs` (по умолчанию 100мс)

```csharp
// Пример: запись → задержка → чтение для верификации
await context.DiagWriter.WriteUInt32Async(address, key, ct);

// Задержка для применения изменений ECU
await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

var readResult = await context.DiagReader.ReadUInt32Async(address, ct);
```

### 5.3 Формат сообщений об ошибках Modbus

```csharp
// Ошибка записи
$"Ошибка при записи ключа 0x{key:X8} в регистры {addr}-{addr+1}. {result.Error}"

// Ошибка чтения
$"Ошибка при чтении ключа из регистров {addr}-{addr+1}. {result.Error}"

// Неверное значение
$"Ошибка: прочитан ключ 0x{actual:X8} из регистров {addr}-{addr+1}, ожидался 0x{expected:X8}"
```

**Правила:**
- Hex: `0x{value:X8}` (8 цифр для uint32) или `0x{value:X4}` (4 цифры для uint16)
- Адреса: документационные (1000), не Modbus
- Всегда добавлять `result.Error` для деталей

---

## Часть 5.5: Паттерн сброса Start тега

### Правило

**Start тег сбрасывается ТОЛЬКО при успехе (End от PLC).** При ошибке, retry или skip — координатор сбросит Start через `ResetBlockStartAsync`.

```csharp
// ✅ ПРАВИЛЬНО — сброс только при успехе
private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
{
    logger.LogInformation("Шаг завершён успешно");

    var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
    return writeResult.Error != null
        ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}")
        : TestStepResult.Pass();
}

// ❌ НЕПРАВИЛЬНО — finally сбрасывает Start
public async Task<TestStepResult> ExecuteAsync(...)
{
    try { return await WaitPhase1Async(context, ct); }
    finally { await TryResetStartTagAsync(context); }  // НЕ ДЕЛАТЬ!
}
```

### Кто сбрасывает Start

| Сценарий | Кто сбрасывает |
|----------|----------------|
| **Успех (End)** | Шаг в `HandleSuccessAsync` |
| **Ошибка (Error)** | Координатор `ResetBlockStartAsync` при Skip |
| **Retry** | Координатор перед повторным выполнением |
| **Cancel** | Координатор |

### Почему так

- При ошибке шаг возвращает `TestStepResult.Fail()` и завершается
- Координатор показывает диалог Retry/Skip
- При Skip — координатор вызывает `ResetBlockStartAsync(step)` через `PlcBlockTagHelper.GetStartTag()`
- При Retry — шаг перезапускается, Start запишется заново

**См. также:** `TestExecutionCoordinator.ErrorHandling.cs:369` — `ProcessSkipAsync`

---

## Часть 6: CancellationToken

> **КРИТИЧНО:** Шаги полностью ответственны за обработку отмены. Система НЕ защищает от зависших шагов.

### Обязательные правила

```csharp
// В циклах
while (condition)
{
    ct.ThrowIfCancellationRequested();
    // ...
}

// Задержки
await context.DelayAsync(TimeSpan.FromSeconds(5), ct);  // Test steps
await Task.Delay(TimeSpan.FromSeconds(5), ct);          // Pre-execution

// IO операции — всегда передавать ct
await context.OpcUa.WriteAsync(tag, value, ct);
await context.DiagReader.ReadUInt32Async(address, ct);
```

### Запрещено

```csharp
// Блокирующие вызовы
var result = task.Result;  // ❌
task.Wait();               // ❌

// Задержки без ct
await Task.Delay(1000);    // ❌
Thread.Sleep(1000);        // ❌

// Бесконечные циклы без проверки
while (true) { }           // ❌
```

---

## Часть 7: Чек-листы

### Pre-Execution Step

- [ ] Файл `Services/Steps/Steps/MyStep.cs`
- [ ] Реализовать `IPreExecutionStep`
- [ ] `IHasPlcBlockPath` / `IRequiresPlcTags` если нужно
- [ ] Регистрация в `StepsServiceExtensions.cs`
- [ ] Порядок в `PreExecutionStepRegistry.cs`

### Test Step

- [ ] Файл `Services/Steps/Steps/Category/MyStep.cs`
- [ ] Реализовать `ITestStep`
- [ ] `IProvideLimits` / `IRequiresRecipes` / `INonSkippable` если нужно
- [ ] Автоматическая регистрация через рефлексию

### Code Review

- [ ] Циклы содержат `ct.ThrowIfCancellationRequested()`
- [ ] Задержки через `context.DelayAsync()` или `Task.Delay(..., ct)`
- [ ] IO передаёт `ct`
- [ ] Нет `.Result` / `.Wait()` / `Thread.Sleep()`

---

## Часть 8: Типичные ошибки

| Ошибка | Решение |
|--------|---------|
| Шаг не выполняется | Проверить `StepsServiceExtensions.cs` и `PreExecutionStepRegistry.cs` |
| Неправильный тег | Формат: `ns=3;s="DB_VI"."Block"."Start"` |
| Блокирующий вызов | Использовать `await`, не `.Result` |
| Сообщение висит после успеха | `messageState.Clear()` |
| Дубликаты результатов при Retry | `testResultsService.Remove()` перед `Add()` |
| IO в `GetLimits` | Только `RecipeProvider` (in-memory) |

---

## Справочник путей

| Компонент | Путь |
|-----------|------|
| IPreExecutionStep | `Services/Steps/Infrastructure/Interfaces/PreExecution/` |
| ITestStep | `Services/Steps/Infrastructure/Interfaces/Test/` |
| TestStepContext | `Services/Steps/Infrastructure/Registrator/` |
| PreExecutionStepRegistry | `Services/Steps/Infrastructure/Execution/PreExecution/` |
| TagWaiter | `Services/OpcUa/` |
| DiagnosticSettings | `Services/Diagnostic/Connection/` |
| StepsServiceExtensions | `Services/DependencyInjection/` |
