# План: Система повтора/завершения теста

## Обзор

Реализация системы обработки завершения теста с поддержкой:
- Показа изображения результата вместо грида
- Ожидания решения PLC (повтор или завершение)
- Сохранения в MES/БД (заглушка)
- Диалога повтора при ошибке сохранения

---

## Этапы реализации

### Этап 1: Базовая инфраструктура
- `CompletionModels.cs` — enum'ы и модели
- `TestCompletionUiState.cs` — состояние UI для картинки
- Регистрация в DI

**Результат:** Можно показывать/скрывать картинку (пока вручную)

### Этап 2: UI — картинка вместо грида
- Изменения в `MyComponent.razor`
- CSS стили
- Подписка на `OnStateChanged`

**Результат:** Картинка отображается когда `ShowResultImage = true`

### Этап 3: Основной координатор (без сохранения)
- `TestCompletionCoordinator.cs` — базовый класс
- `TestCompletionCoordinator.Flow.cs` — логика End/Req_Repeat
- Интеграция в `PreExecutionCoordinator`
- Новый `CycleExitReason.RepeatRequested`

**Результат:** OK повтор работает (без сохранения), картинка появляется/исчезает

### Этап 4: Заглушка сохранения
- `ITestResultStorage.cs`
- `TestResultStorageStub.cs`
- Диалог "Не удалось сохранить результаты"

**Результат:** Завершение с "сохранением" (заглушка)

### Этап 5: NOK повтор с подготовкой
- `TestCompletionCoordinator.Repeat.cs`
- `ScanStepBase.ExecuteWithoutScanAsync()`
- Диалог "Не удалось запустить тест"
- Интеграция с ReworkDialogService

**Результат:** Полный NOK flow с подготовкой

### Этап 6: Обработка сбросов
- Подписка на `PlcResetCoordinator.OnForceStop`
- Подписка на `ErrorCoordinator.OnReset`
- Закрытие диалогов при сбросе

**Результат:** Корректная отмена при сбросах PLC

---

## Сценарии

### OK (testResult = 1)
1. Записать `End = true` в PLC
2. Показать изображение `green_smiley_clean.png` с текстом инструкции
3. Ждать `End = false` (PLC сбросит)
4. Ждать 1 секунду
5. Читаем `Req_Repeat`:
   - **true → Повтор**: очистить грид/время/результаты (кроме скана), запустить заново
   - **false → Завершение**: сохранить OK → **сброс как мягкий/жёсткий**

### NOK (testResult = 2)
1. Записать `End = true`, показать изображение
2. Ждать `End = false` (PLC сбросит)
3. Ждать 1 секунду
4. Читаем `Req_Repeat`:
   - **false → Завершение**: сохранить NOK → **сброс как мягкий/жёсткий**
   - **true → Повтор**:
     - СНАЧАЛА: сохранить NOK в MES/БД (диалог при ошибке)
     - ПОТОМ: подготовка (шаги ScanStep без сканирования)
     - Очистить грид/время/результаты/историю
     - Запустить тест заново

---

## Сброс при завершении (как в PlcResetGuide.md)

После успешного сохранения выполнить те же действия что и при мягком/жёстком сбросе:

```csharp
private void ExecuteFinishReset()
{
    // Те же действия что в ClearStateOnReset()
    state.BoilerState.Clear();        // Очистка данных котла
    state.PhaseState.Clear();         // Очистка фазы
    ClearBarcode();                   // Очистка штрихкода
    infra.ErrorService.IsHistoryEnabled = false;  // Отключение истории

    // Очистка грида (как при HardReset)
    infra.StatusReporter.ClearAllExceptScan();
}
```

**Это гарантирует:**
- Полную очистку состояния
- Готовность к новому циклу сканирования
- Единообразное поведение с ручным сбросом

---

## Картинка: когда появляется/исчезает

### ПОЯВЛЯЕТСЯ:
| Момент | Место в коде |
|--------|--------------|
| После записи `End = true` в PLC | `HandleTestCompletedAsync()` → `CompletionUiState.ShowImage()` |

### ИСЧЕЗАЕТ:

| Сценарий | Момент | Место в коде |
|----------|--------|--------------|
| **Повтор** | После `ClearForRepeat()`, перед запуском теста | `HandleRepeatAsync()` → `HideImage()` |
| **Завершение** | После сохранения, перед сбросом | `HandleFinishAsync()` → `HideImage()` |
| **Сброс PLC** | Сразу при получении сигнала | `HandleReset()` → `HideImage()` |

### Визуально:
```
[Тест завершён]
      │
      ▼
┌─────────────────────────┐
│  >>> КАРТИНКА <<<       │  ← ShowImage()
│  + текст инструкции     │
└─────────────────────────┘
      │
      ├─── Повтор ──────► HideImage() → [Тест заново]
      ├─── Завершение ──► HideImage() → [Сброс]
      └─── Сброс PLC ───► HideImage() → [Сброс]
```

---

## Диалоговые окна

### Принцип разделения:
- **Диалог** — общее сообщение + кнопка "Повторить"
- **Уведомление (NotificationService)** — конкретная ошибка от сервиса

### Диалог "Не удалось сохранить результаты"
**Когда:** Ошибка при сохранении результата в БД/MES
```
┌─────────────────────────────────────────┐
│                                         │
│    Не удалось сохранить результаты      │
│                                         │
│                         [Повторить]     │
└─────────────────────────────────────────┘
```
+ Уведомление: "MES: Connection timeout" (текст ошибки)

### Диалог "Не удалось запустить тест"
**Когда:** Ошибка при запросе на старт (подготовка для повтора), и это НЕ rework
```
┌─────────────────────────────────────────┐
│                                         │
│      Не удалось запустить тест          │
│                                         │
│                         [Повторить]     │
└─────────────────────────────────────────┘
```
+ Уведомление: "Не удалось загрузить рецепты" (текст ошибки)

### Rework (MES)
**Когда:** Запрос на старт вернул `NeedsRework = true`
→ Запускаем **ReworkDialogService** (как сейчас работает)

### Когда закрываются:
- ✅ Успех операции (OK от сервиса)
- ✅ Мягкий сброс PLC
- ✅ Жёсткий сброс PLC

**НЕ закрываются** при нажатии "Повторить" — просто retry операции.

### Разделение: Rework vs Ошибка

| Сценарий | Что показываем |
|----------|----------------|
| MES вернул `NeedsRework` | **ReworkDialogService** (как сейчас) — логин/пароль/причина |
| Неизвестная ошибка/баг | **Диалог ошибки** + уведомление с деталями |

---

## Детальный Flow для NOK повтора

```
[testResult = 2, Req_Repeat = true]
        │
        ▼
┌───────────────────────────────────────┐
│ ШАГ 1: Сохранить NOK в MES/БД         │
│                                       │
│  while (!saved && !cancelled):        │
│    result = SaveToStorage(NOK)        │
│    if (result.Error):                 │
│      shouldRetry = ShowSaveErrorDialog│
│      if (!shouldRetry): return Cancel │
│    else: saved = true                 │
└───────────────────────────────────────┘
        │
        ▼
┌───────────────────────────────────────┐
│ ШАГ 2: Записать Ask_Repeat = true     │
│ (PLC сбросит Req_Repeat и End)        │
└───────────────────────────────────────┘
        │
        ▼
┌───────────────────────────────────────┐
│ ШАГ 3: Подготовка (без сканирования)  │
│                                       │
│  while (!prepared && !cancelled):     │
│    - LoadBoilerData (тип котла)       │  ← Phase: "Загрузка данных..."
│    - LoadRecipes (рецепты)            │  ← Phase: "Загрузка рецептов..."
│    - BuildTestMaps                    │
│    - ResolveSteps                     │  ← Phase: "Проверка шагов..."
│    - ValidateRecipes                  │  ← Phase: "Проверка рецептов..."
│    - InitializeDatabase (новая запись)│  ← Phase: "Создание записей в БД..."
│    - WriteRecipesToPlc                │
│                                       │
│  MessageService показывает фазы       │
│  через ExecutionPhaseState (правило   │
│  110 с приоритетом)                   │
│                                       │
│  if (Error):                          │
│    if (MES && ReworkNeeded):          │
│      ReworkDialogService.Execute()    │
│    else:                              │
│      Диалог "Не удалось запустить     │
│      тест" + уведомление с ошибкой    │
└───────────────────────────────────────┘
        │
        ▼
┌───────────────────────────────────────┐
│ ШАГ 4: Очистка                        │
│  - Grid (кроме scan step)             │
│  - StepTimings (кроме scan)           │
│  - TestResults                        │
│  - ErrorHistory                       │
└───────────────────────────────────────┘
        │
        ▼
┌───────────────────────────────────────┐
│ ШАГ 5: Скрыть изображение             │
│ Показать грид                         │
└───────────────────────────────────────┘
        │
        ▼
┌───────────────────────────────────────┐
│ ШАГ 6: Запуск теста                   │
│  → BlockBoilerAdapterStep             │
│  → TestExecutionCoordinator           │
└───────────────────────────────────────┘
```

**Важно для MES:** При ошибке подготовки может потребоваться rework flow (авторизация админа + причина).

---

## Новые файлы

### 1. `Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.cs`
Главный координатор завершения теста.

```csharp
public partial class TestCompletionCoordinator(
    TestCompletionDependencies deps,
    TestCompletionState state)
{
    public bool IsWaitingForCompletion { get; private set; }
    public event Action? OnCompletionStateChanged;

    public async Task<CompletionResult> HandleTestCompletedAsync(int testResult, CancellationToken ct);
}
```

### 2. `Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.Flow.cs`
Логика ожидания PLC и обработки решений.

### 3. `Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.Repeat.cs`
Логика повтора теста (очистка, подготовка для NOK).

### 4. `Services/Steps/Infrastructure/Execution/Completion/CompletionModels.cs`
```csharp
public enum CompletionPhase { None, WaitingForPlcEnd, SavingToStorage, Completed }
public enum CompletionResult { Finished, RepeatRequested, Cancelled }
```

### 5. `Services/Steps/Infrastructure/Execution/Completion/TestCompletionUiState.cs`
Состояние UI для показа изображения.

```csharp
public class TestCompletionUiState
{
    public bool ShowResultImage { get; private set; }
    public int TestResult { get; private set; }
    public string ImagePath => "images/green_smiley_clean.png";
    public string InstructionText => "\"Один шаг\" - закончить тест или \"Повтор\" для повтора теста";
    public event Action? OnStateChanged;

    public void ShowImage(int testResult);
    public void HideImage();
}
```

### 6. `Services/Storage/ITestResultStorage.cs`
```csharp
public interface ITestResultStorage
{
    Task<SaveResult> SaveAsync(TestResultData data, CancellationToken ct);
}
```

### 7. `Services/Storage/TestResultStorageStub.cs`
Заглушка сохранения (для БД и MES).

### 8. `Components/Main/Modals/SaveErrorDialog.razor`
Диалог ошибки сохранения с кнопкой "Повторить".

---

## Изменения в существующих файлах

### 1. `MyComponent.razor` (строки 67-68)
**Было:**
```razor
<MessageHelper />
<TestSequenseGrid />
```

**Станет:**
```razor
@inject TestCompletionUiState CompletionUiState

@if (CompletionUiState.ShowResultImage)
{
    <div class="completion-image-container">
        <div class="completion-instruction">@CompletionUiState.InstructionText</div>
        <img src="@CompletionUiState.ImagePath" class="completion-image" />
    </div>
}
else
{
    <MessageHelper />
    <TestSequenseGrid />
}
```

+ Подписка на `CompletionUiState.OnStateChanged` в `@code`.

### 2. `MyComponent.razor.css`
Добавить стили для `.completion-image-container`, `.completion-instruction`, `.completion-image`.

### 3. `PreExecutionCoordinator.cs` (строка 9-16)
Добавить `RepeatRequested` в enum:
```csharp
public enum CycleExitReason
{
    // ... существующие
    RepeatRequested,  // Запрошен повтор теста
}
```

### 4. `PreExecutionCoordinator.MainLoop.cs`
**HandleTestCompleted** → вынести логику в `TestCompletionCoordinator`:

```csharp
// В ExecuteCycleAsync после testCompletionTcs.Task:
return await HandleTestCompletionAsync(ct);

private async Task<CycleExitReason> HandleTestCompletionAsync(CancellationToken ct)
{
    var testResult = CalculateTestResult();
    state.BoilerState.SetTestResult(testResult);
    coordinators.CompletionUiState.ShowImage(testResult);

    try
    {
        var result = await coordinators.CompletionCoordinator
            .HandleTestCompletedAsync(testResult, ct);

        return result switch
        {
            CompletionResult.Finished => CycleExitReason.TestCompleted,
            CompletionResult.RepeatRequested => CycleExitReason.RepeatRequested,
            _ => _pendingExitReason ?? CycleExitReason.SoftReset,
        };
    }
    finally
    {
        coordinators.CompletionUiState.HideImage();
    }
}
```

**HandleCycleExit** — добавить case для `RepeatRequested`:
```csharp
case CycleExitReason.RepeatRequested:
    // Запустить тест заново минуя сканирование
    break;
```

### 5. `PreExecutionCoordinators.cs`
Добавить зависимости:
```csharp
public TestCompletionCoordinator CompletionCoordinator { get; }
public TestCompletionUiState CompletionUiState { get; }
```

### 6. `StepsServiceExtensions.cs`
Регистрация новых сервисов:
```csharp
services.AddSingleton<TestCompletionCoordinator>();
services.AddSingleton<TestCompletionUiState>();
services.AddSingleton<ITestResultStorage, TestResultStorageStub>();
```

---

## Логика TestCompletionCoordinator.Flow.cs

```csharp
public async Task<CompletionResult> HandleTestCompletedAsync(int testResult, CancellationToken ct)
{
    IsWaitingForCompletion = true;
    OnCompletionStateChanged?.Invoke();

    try
    {
        // 1. Записать End = true
        await deps.PlcService.WriteAsync(BaseTags.ErrorSkip, true, ct);

        // 2. Ждать End = false (PLC сбросит)
        await deps.TagWaiter.WaitForFalseAsync(BaseTags.ErrorSkip, ct);

        // 3. Ждать 1 секунду
        await Task.Delay(1000, ct);

        // 4. Читаем Req_Repeat → решение
        var reqRepeat = deps.Subscription.GetValue<bool>(BaseTags.ErrorRetry);

        if (reqRepeat)
        {
            return await HandleRepeatAsync(testResult, ct);
        }
        else
        {
            return await HandleFinishAsync(testResult, ct);
        }
    }
    finally
    {
        IsWaitingForCompletion = false;
        OnCompletionStateChanged?.Invoke();
    }
}
```

**Логика ожидания:**
```
[End = true записан]
        │
        ▼
[Ждём End = false] ←── PLC сбрасывает End
        │
        ▼
[Delay 1 секунда] ←── Даём время PLC выставить Req_Repeat
        │
        ▼
[Читаем Req_Repeat]
        │
        ├── true → ПОВТОР
        └── false → ЗАВЕРШЕНИЕ
```

---

## Логика TestCompletionCoordinator.Repeat.cs

```csharp
private async Task<CompletionResult> HandleRepeatAsync(int testResult, CancellationToken ct)
{
    deps.Logger.LogInformation("Запрошен повтор теста (testResult={Result})", testResult);

    // ШАГ 1: СНАЧАЛА сохранить результат в MES/БД (для NOK обязательно!)
    if (testResult == 2)
    {
        var saveSuccess = await TrySaveWithRetryAsync(testResult, ct);
        if (!saveSuccess)
            return CompletionResult.Cancelled;
    }

    // ШАГ 2: Записать Ask_Repeat = true (PLC сбросит Req_Repeat и End)
    await deps.PlcService.WriteAsync(BaseTags.AskRepeat, true, ct);

    // ШАГ 3: Для NOK - ПОТОМ выполнить подготовку (шаги scan без сканирования)
    if (testResult == 2)
    {
        var prepareSuccess = await TryPrepareWithRetryAsync(ct);
        if (!prepareSuccess)
            return CompletionResult.Cancelled;
    }

    // ШАГ 4: Очистить данные
    ClearForRepeat();

    return CompletionResult.RepeatRequested;
}

// Сохранение с retry loop
private async Task<bool> TrySaveWithRetryAsync(int testResult, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        var result = await SaveToStorageAsync(testResult);
        if (result.IsSuccess)
        {
            deps.Logger.LogInformation("Результат {Status} сохранён", testResult == 1 ? "OK" : "NOK");
            return true;
        }

        // Показать диалог: "Не удалось сохранить" + кнопка "Повторить"
        var shouldRetry = await ShowSaveErrorDialogAsync(result.ErrorMessage);
        if (!shouldRetry)
            return false; // Сброс отменил операцию
    }
    return false;
}

// Подготовка с retry loop (для NOK)
private async Task<bool> TryPrepareWithRetryAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        var result = await PrepareForRepeatAsync(ct);
        if (result.IsSuccess)
        {
            deps.Logger.LogInformation("Подготовка к повтору завершена");
            return true;
        }

        // Для MES может потребоваться rework flow
        if (deps.AppSettings.UseMes && result.NeedsRework)
        {
            var reworkResult = await deps.ReworkDialog.ExecuteReworkFlowAsync(
                result.ErrorMessage, ExecuteReworkAsync);
            if (reworkResult.IsSuccess)
                continue; // Повторить подготовку после rework
            if (reworkResult.IsCancelled)
                return false;
        }

        // Показать диалог: "Не удалось подготовить" + кнопка "Повторить"
        var shouldRetry = await ShowPrepareErrorDialogAsync(result.ErrorMessage);
        if (!shouldRetry)
            return false;
    }
    return false;
}

// Подготовка: шаги ScanStep БЕЗ самого сканирования
private async Task<PrepareResult> PrepareForRepeatAsync(CancellationToken ct)
{
    var context = new PreExecutionContext
    {
        Barcode = state.BoilerState.SerialNumber, // Используем известный barcode
        OpcUa = deps.PlcService
    };

    var scanStep = deps.ScanSteps.GetScanStep();

    // Новый метод ExecuteWithoutScanAsync выполняет:
    // 1. LoadBoilerDataAsync (тип, рецепты из БД/MES)
    // 2. LoadTestSequenceAsync
    // 3. BuildTestMaps
    // 4. SaveBoilerState
    // 5. ResolveTestMaps
    // 6. ValidateRecipes
    // 7. InitializeDatabaseAsync (новая запись для повтора)
    // 8. WriteRecipesToPlcAsync
    // 9. InitializeRecipeProvider
    // НО ПРОПУСКАЕТ ValidateBarcode
    //
    // ВАЖНО: Каждый шаг устанавливает фазу через ExecutionPhaseState
    // → MessageService показывает "Загрузка рецептов...", "Проверка рецептов..." и т.д.

    return await scanStep.ExecuteWithoutScanAsync(context, ct);
}

private void ClearForRepeat()
{
    // Очистить грид (кроме шага сканирования)
    state.SequenseService.ClearAllExceptScan();

    // Очистить время шагов (кроме сканирования)
    state.TimingService.ClearAllExceptScan();

    // Очистить результаты тестов
    state.ResultsService.Clear();

    // Очистить историю ошибок
    state.ErrorService.ClearHistory();

    deps.Logger.LogInformation("Состояние очищено для повтора");
}
```

**Дополнительно в `ScanStepBase.cs`:**
```csharp
// Новый метод для повтора (без сканирования)
public async Task<PrepareResult> ExecuteWithoutScanAsync(PreExecutionContext context, CancellationToken ct)
{
    // Пропускаем ValidateBarcode - barcode уже известен

    // Выполняем остальные шаги...
    var loadError = await preparationFacade.LoadBoilerDataAsync(context);
    if (loadError != null) return PrepareResult.Fail(loadError);

    // ... остальные шаги как в ExecuteAsync
}
```

---

## Обработка сбросов

В `TestCompletionCoordinator` подписаться на:
- `PlcResetCoordinator.OnForceStop` → Cancel + HideImage
- `ErrorCoordinator.OnReset` → Cancel + HideImage

---

## Критические файлы

| Файл | Изменение |
|------|-----------|
| `PreExecutionCoordinator.MainLoop.cs` | Интеграция с TestCompletionCoordinator |
| `PreExecutionCoordinator.cs` | Новый enum RepeatRequested |
| `MyComponent.razor` | Условное отображение изображения |
| `BaseTags.cs` | Использовать ErrorSkip, ErrorRetry, AskRepeat |

---

## Верификация

1. Запустить приложение, выполнить тест до конца (OK)
2. Проверить показ изображения после завершения
3. Нажать "Один шаг" на PLC → должен произойти сброс
4. Повторить с "Повтор" → тест должен начаться заново
5. Проверить сценарий NOK
6. Проверить мягкий/жёсткий сброс во время ожидания → изображение должно скрыться
