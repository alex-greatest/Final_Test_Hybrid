# StepsGuide.md

Инструкция по созданию шагов (Steps) в проекте Final_Test_Hybrid.

> **См. также:** [CLAUDE.md](../../CLAUDE.md), [ErrorSystemGuide.md](../runtime/ErrorSystemGuide.md), [DiagnosticGuide.md](../diagnostics/DiagnosticGuide.md)

---

## Обзор архитектуры

| Тип | Интерфейс | Когда выполняется | Пример |
|-----|-----------|-------------------|--------|
| **Pre-execution** | `IPreExecutionStep` | До тестов (сканирование, рецепты) | `ScanBarcodeStep` |
| **Test step** | `ITestStep` | Во время тестирования (из Excel) | `MeasureVoltageStep` |

```
PRE-EXECUTION STEPS (последовательно)
├─ ScanBarcodeStep / ScanBarcodeMesStep (выбор по UseMes)
├─ StartTimer1Step (фиксируется в StepTimingService как completed-step с `00.00`)
├─ BlockBoilerAdapterStep
└─ StartTestExecution()

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
context.ScanServiceContext // Кэш scan-служебных полей (App_Version, Plant_ID, Shift_No, Tester_No)

// PreExecutionResult — фабричные методы
PreExecutionResult.Continue();                    // Успех
PreExecutionResult.TestStarted();                 // Тесты запущены
PreExecutionResult.Fail("Ошибка");                // Критическая ошибка
PreExecutionResult.FailRetryable(error, canSkip: false, userMessage: "...", errors: []);
```

### 1.3 Регистрация

1. DI: зарегистрировать шаг в `StepsServiceExtensions.cs` (обычно `services.AddSingleton<MyStep>();`)
2. Порядок: добавить шаг в `PreExecutionStepAdapter.GetOrderedSteps()` (`IPreExecutionStepRegistry`)
3. Если шаг зависит от MES/Non-MES ветки, выбрать его в `GetOrderedSteps()` через `AppSettingsService.UseMes`

### 1.4 Scan-служебные результаты (контракт)

`ScanStepBase` в scan-фазе только собирает кэшируемый scan-контекст.
Прямая запись scan-служебных результатов из scan-step в `ITestResultsService` не используется.

`Plant_ID` остаётся частью внутреннего `ScanServiceContext`, но больше не пишется
в `TestResultsService` и не входит в набор scan-служебных результатов UI/storage.

Фактическая запись выполняется централизованно в `PreExecutionCoordinator.InitializeTestRunningAsync()` сразу после `ClearForNewTestStart()`:

1. Кэшируемые поля берутся из `context.ScanServiceContext`:
   - `App_Version`
   - `Shift_No`
   - `Tester_No`
2. Давления читаются из OPC каждый старт теста (включая repeat):
   - `Pres_atmosph.`
   - `Pres_in_gas`

Если чтение давлений из OPC неуспешно, старт pipeline завершается ошибкой (fail-fast, без ослабления safety).

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
context.DiagReader        // Базовый PausableRegisterReader (не использовать в обычных test steps)
context.DiagWriter        // Базовый PausableRegisterWriter (не использовать в обычных test steps)
context.PacedDiagReader   // Обязательный путь для step-level Modbus чтения
context.PacedDiagWriter   // Обязательный путь для step-level Modbus записи
context.RecipeProvider    // Доступ к рецептам
context.Variables         // Локальные переменные шага
context.PauseToken        // Токен паузы

// Pause-aware задержка
await context.DelayAsync(TimeSpan.FromMilliseconds(100), ct);

// Промежуточные результаты (обновляют UI без завершения шага)
context.ReportProgress("Инициализация...");
```

### 2.2.1 Промежуточные результаты (ReportProgress)

### 2.2.2 Контракт Modbus для test steps

- Для обычных step-level Modbus операций использовать `context.PacedDiagReader` / `context.PacedDiagWriter`.
- `context.DiagReader` / `context.DiagWriter` считать базовым/совместимостным путём и не использовать в новых test steps без отдельного обоснования.
- Ручные `DelayAsync(...WriteVerifyDelayMs...)` между соседними Modbus `read/write` в test steps не использовать.
- Global pacing получает тот же `CancellationToken`, умеет отменяться и pause-aware: ожидание не продолжается во время `Auto OFF`.

#### Контракт удержания режима `1036`

- Шаги `Coms/CH_Start_Max_Heatout`, `Coms/CH_Start_Min_Heatout` и `Coms/CH_Start_ST_Heatout` после успешной записи и read-back `1036` обязаны вызвать `BoilerOperationModeRefreshService.ArmMode(rawModeValue, step.Name)`.
- При входе в любой active `CH_Start_*` шаг сервис сначала делает `ClearAndDrainAsync(...)`, чтобы предыдущий retained-mode не жил во время retry-path и ожиданий PLC до нового arm.
- В `1036` хранится raw `ushort`; `SystemWorkMode` для этого контура не использовать.
- `BoilerOperationModeRefreshService` работает системными `RegisterWriter` / `RegisterReader`, а не `context.PacedDiag*`, потому что удержание режима не должно зависеть от step pause (`Auto OFF`).
- Step-level write/read-back `1036` и `ChReset` выполняются под shared mode-change lease из `BoilerOperationModeRefreshService`, чтобы фоновый refresh не мог вклиниться между шаговой записью, verify и `ArmMode(...)` / `Clear(...)`.
- Refresh-цикл использует `Diagnostic:OperationModeRefreshInterval` (по умолчанию `15 минут`) и повторно подтверждает сохранённый режим только при `dispatcher ready` (`IsStarted && IsConnected && !IsReconnecting && LastPingData != null`).
- Если к моменту refresh диагностика недоступна, сервис не делает `StartAsync()` сам, не очищает latch и ждёт восстановления ready-state.
- Если refresh уже дошёл до write/read/verify и получил fail при готовом dispatcher, сервис повторяет попытку через отдельный slow retry `5 секунд`, а не через step pacing `WriteVerifyDelayMs`.
- Успешный `Coms/CH_Reset` очищает latch только после подтверждённого `1036 == 0`.
- Дополнительные точки очистки latch: `PlcResetCoordinator.OnForceStop`, `ErrorCoordinator.OnReset`, `BoilerState.OnCleared` и `TestExecutionCoordinator.ResetForRepeat()`.
- Ручные инженерные изменения режима и `SetStandModeAsync(...)` не обновляют retained-state `1036`.

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

### 2.4 Ошибки в шагах (ErrorDefinition)

| Тип ошибки | PlcTag | Активация | Пример |
|------------|--------|-----------|--------|
| **PLC** | Есть | Автоматически через PlcErrorMonitorService | Al_NoWaterFlow, Al_FillTime |
| **Программная** | Нет | Вручную через `errors:` в TestStepResult.Fail | NoDiagnosticConnection, EcuArticleMismatch |

**PLC ошибки** (с `PlcTag`) — НЕ передавать в `TestStepResult.Fail()`. Они автоматически активируются когда PLC поднимает соответствующий Al_* сигнал. `PlcErrorMonitorService` подписывается на все теги из `ErrorDefinitions.PlcErrors`.

**Программные ошибки** (без `PlcTag`) — передавать в `TestStepResult.Fail()`:
```csharp
// ✅ ПРАВИЛЬНО — программная ошибка без PlcTag
return TestStepResult.Fail("Нет связи с котлом", errors: [ErrorDefinitions.NoDiagnosticConnection]);

// ❌ НЕПРАВИЛЬНО — PLC ошибка с PlcTag (активируется автоматически)
return TestStepResult.Fail(msg, errors: [ErrorDefinitions.AlNoWaterFlowCh]);
```

### 2.5 Регистрация

Test steps регистрируются **автоматически** через рефлексию — достаточно создать класс с `ITestStep`.

### 2.6 Контракт записи runtime-результатов (обязательно)

Сигнатура сервиса результатов:

```csharp
void Add(string parameterName, string value, string min, string max, int status, bool isRanged, string unit, string test);
```

`test` обязателен для всех runtime-записей и пробрасывается в MES как есть.
Запись с пустым `test` считается нарушением контракта.

Правила заполнения `test`:

| Категория записи | Значение `test` |
|------------------|-----------------|
| Результат обычного `ITestStep` | `step.Name` |
| Scan-служебные результаты | `"ScanBarcode"` |
| Тайминги (`Test_Time`, `Change_Over_Time`, `Complete_Time`) | `"Test Time"` |
| Completion (`Final_result`, `Testing_date`) | `"Test Completion"` |

#### 2.6.0 Формат времени для `Timer_1` / `Timer_2`

- Параметры `Timer_1` и `Timer_2` записываются в `value` только в формате `HH:mm:ss`.
- Формат секунд с дробной частью (`123.45`) для этих параметров не используется.
- В MES для `Timer_1` и `Timer_2` используется `valueType = "string"`.

#### 2.6.1 Контракт сохранения в `TB_RESULT`

Сохранение runtime-результатов в `TB_RESULT` выполняется через сопоставление:
`TestResultItem.Test` -> активный `StepFinalTestHistory.Name` (точное строковое сравнение).

Правила:
1. Если шаг найден, `Result` сохраняется с `StepFinalTestHistoryId`.
2. Если шаг не найден, запись `Result` пропускается (`continue`), пишется warning (`StepHistoryNotFound`), общий `SaveAsync` продолжается.
3. Это штатное поведение и не считается падением операции сохранения.

Источник решения: `plan-result-step-testname.md`.

Рекомендуемый шаблон вызова:

```csharp
testResultsService.Remove(parameterName);
testResultsService.Add(
    parameterName: parameterName,
    value: value,
    min: "",
    max: "",
    status: 1,
    isRanged: false,
    unit: unit,
    test: Name);
```

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

**Примечание по `Coms/Check_Comms`:**
- `CheckCommsStep` реализует `INonSkippable`, поэтому оператор не может обойти шаг.
- При `AutoReady = false` и отсутствии диагностической связи шаг возвращает `NoDiagnosticConnection`; рабочий путь продолжения — восстановить автомат и выполнить `Retry`.
- После захвата runtime-lease шаг ждёт именно свежий runtime ping; stale `LastPingData` от ручной панели не считается успешной проверкой связи.

**Примечание по execution stand-write (`SetStandModeAsync`)**
- Execution-шаги, которые на retry возвращают котёл в режим Стенд, не должны писать ключ во время `dispatcher.IsReconnecting`.
- Перед фактической записью такой шаг обязан дождаться ready-state диагностики: `IsStarted=true`, `IsConnected=true`, `IsReconnecting=false`, `LastPingData!=null`.
- Ожидание выполняется boundedly (`20 c`, polling `100 мс`) через `context.DelayAsync(...)`, поэтому остаётся pause-aware и cancellation-aware.
- Если запись после ready-check упала reconnect-reject ошибкой класса `State=pending / начато переподключение Modbus до начала выполнения`, helper считает это race-window, повторно ждёт ready-state и делает ещё одну попытку только в пределах того же общего дедлайна.
- Если ready-state не восстановился за это окно, шаг завершается communication-fail своего текущего fail-path; generic multi-write retry по другим ошибкам не используется.
- После фактического старта записи/чтения собственный error-handling шага не меняется: любая реальная ошибка операции завершает попытку так же, как раньше.

**Примечание по `Coms/Safety_Time`:**
- Шаг измерения `Safety time` использует только фактические step-level Modbus операции как источник истины по связи.
- Любой read/write fail текущей попытки завершает шаг ошибкой без отдельного diagnostic connection latch.
- Краткий reconnect внутри этого шага не пережидается: продолжение допускается только через штатный `Retry`.

---

## Часть 4: Работа с PLC (OPC-UA)

### 4.1 TagWaiter

| Сервис | Использование | Паузится |
|--------|---------------|----------|
| `TagWaiter` | System/runtime waits, `ErrorCoordinator`, execution skip/reset guards | Нет |
| `PausableTagWaiter` | Test steps (`context.TagWaiter`) и pre-execution PLC retry guards | Да |

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

**Примечание для execution PLC-block шагов:**
- Внутри `ColumnExecutor` для текущего PLC-блока автоматически открывается fresh-signal scope.
- После успешного `Start=true` terminal `End/Error` этого блока в `context.TagWaiter` принимаются только как свежие runtime updates текущей попытки.
- Специально очищать cache, добавлять polling или писать per-step stale-guard не нужно.

### 4.2 Чтение/запись тегов

```csharp
var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
if (writeResult.Error != null)
    return TestStepResult.Fail($"Ошибка записи: {writeResult.Error}");

var value = await context.OpcUa.ReadAsync<float>(ValueTag, ct);
```

---

## Часть 5: Работа с Modbus (Диагностика)

> **Подробно:** [DiagnosticGuide.md](../diagnostics/DiagnosticGuide.md) — архитектура, чтение/запись регистров, обработка ошибок.

**Краткая справка:**

| Сервис | Использование | Паузится |
|--------|---------------|----------|
| `RegisterReader/Writer` | Системные операции | Нет |
| `PausableRegisterReader/Writer` | Базовый слой test-step Modbus | Да |
| `PacedRegisterReader/Writer` | Обычные test-step операции (`context.PacedDiagReader/Writer`) | Да |

```csharp
// Чтение и запись в тестовом шаге
var result = await context.PacedDiagReader.ReadUInt32Async(modbusAddress, ct);
var writeResult = await context.PacedDiagWriter.WriteUInt32Async(address, value, ct);
```

---

## Часть 5.5: Паттерн сброса Start тега

### Правило

**Start тег сбрасывается ТОЛЬКО при успехе (End от PLC).** При ошибке шаг возвращает `Fail(...)` без записи `Start=false`. Координатор сбрасывает `Start` только в skip-ветке.
Перед запуском PLC-шага общий execution/pre-execution path не ждёт `Block.End=false`; `Start=true` пишется сразу.
Перед retry координатор тоже не делает безусловный pre-start wait по `Block.End=false`: после `Req_Repeat=false` повторный запуск идёт сразу.
Для execution PLC-block шага stale cached `Block.Error/End` от прошлой попытки не принимаются автоматически: после нового `Start=true` terminal wait использует fresh runtime barrier текущего запуска.

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
| **Ошибка (Error)** | Никто: шаг завершает `Fail(...)` без записи `Start=false` |
| **Retry** | Никто: шаг перезапускается без предварительного `Start=false` от PC |
| **Skip** | Координатор `ResetBlockStartAsync` |
| **Cancel** | Отдельного per-step сброса `Start` нет |

### Почему так

- При ошибке шаг возвращает `TestStepResult.Fail()` и завершается
- Координатор показывает диалог Retry/Skip
- При Skip — координатор вызывает `ResetBlockStartAsync(step)` через `PlcBlockTagHelper.GetStartTag()`
- При Retry — шаг перезапускается, `Start=true` запишется заново без промежуточного `Start=false` от PC
- Отдельного безусловного pre-start guard по `Block.End=false` нет ни для первого запуска, ни для retry
- Перед retry есть обязательный handshake `Req_Repeat=false`
- Для execution PLC-block шага terminal `Block.End/Error` после нового `Start=true` должны быть свежими относительно текущей попытки; stale cached сигнал прошлой попытки шаг не принимает

**См. также:** `TestExecutionCoordinator.ErrorResolution.cs` — `ProcessSkipAsync`

---

## Часть 6: CancellationToken

> **Подробно:** [CancellationGuide.md](CancellationGuide.md) — архитектура, Soft/Hard Reset, примеры.

**КРИТИЧНО:** Шаги полностью ответственны за обработку отмены. Система НЕ защищает от зависших шагов.

| Правило | Пример |
|---------|--------|
| Проверка в циклах | `ct.ThrowIfCancellationRequested();` |
| Задержки с ct | `await context.DelayAsync(..., ct);` |
| IO с ct | `await context.OpcUa.WriteAsync(..., ct);` |

| Запрещено | Почему |
|-----------|--------|
| `.Result`, `.Wait()` | Блокирует поток |
| `Task.Delay(...)` без ct | Игнорирует отмену |
| `Thread.Sleep()` | Блокирует, не отменяется |

---

## Часть 7: Чек-листы

### Pre-Execution Step

- [ ] Файл в папке шагов `Services/Steps/Steps/`
- [ ] Реализовать `IPreExecutionStep`
- [ ] `IHasPlcBlockPath` / `IRequiresPlcTags` если нужно
- [ ] Регистрация в `StepsServiceExtensions.cs`
- [ ] Порядок в `PreExecutionStepAdapter.cs` (`GetOrderedSteps`)

### Test Step

- [ ] Файл в папке шагов `Services/Steps/Steps/`
- [ ] Реализовать `ITestStep`
- [ ] `IProvideLimits` / `IRequiresRecipes` / `INonSkippable` если нужно
- [ ] Автоматическая регистрация через рефлексию
- [ ] Все `testResultsService.Add(...)` вызываются с именованным `test: ...`

### Code Review (CancellationToken)

> **Полный checklist:** [CancellationGuide.md](CancellationGuide.md#checklist-для-новых-шагов)

- [ ] Циклы: `ct.ThrowIfCancellationRequested()`
- [ ] Задержки: `context.DelayAsync(..., ct)`
- [ ] IO: передаёт `ct`
- [ ] Нет `.Result` / `.Wait()` / `Thread.Sleep()`

---

## Часть 8: Типичные ошибки

| Ошибка | Решение |
|--------|---------|
| Шаг не выполняется | Проверить `StepsServiceExtensions.cs`, `PreExecutionStepAdapter.cs` (pre-exec) и `TestStepRegistry.cs` (test steps) |
| Неправильный тег | Формат: `ns=3;s="DB_VI"."Block"."Start"` |
| Блокирующий вызов | Использовать `await`, не `.Result` |
| Сообщение висит после успеха | `messageState.Clear()` |
| Дубликаты результатов при Retry | `testResultsService.Remove()` перед `Add()` |
| Запись не попала в `TB_RESULT` при непустом `test` | Проверить warning `StepHistoryNotFound` и сопоставление `test -> активный StepFinalTestHistory.Name` (при нерезолве действует штатный `warning + skip`) |
| IO в `GetLimits` | Только `RecipeProvider` (in-memory) |

---

## Справочник путей

| Компонент | Путь |
|-----------|------|
| IPreExecutionStep | `Services/Steps/Infrastructure/Interfaces/PreExecution/` |
| ITestStep | `Services/Steps/Infrastructure/Interfaces/Test/` |
| TestStepContext | `Services/Steps/Infrastructure/Registrator/` |
| PreExecutionStepAdapter | `Services/Steps/Infrastructure/Execution/PreExecution/` |
| TagWaiter | `Services/OpcUa/` |
| DiagnosticSettings | `Services/Diagnostic/Connection/` |
| StepsServiceExtensions | `Services/DependencyInjection/` |
