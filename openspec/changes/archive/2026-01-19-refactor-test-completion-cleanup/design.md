# Design: Test Completion Cleanup Flow

## Context

Система выполнения тестов имеет несколько состояний данных:
- **Грид** (StepStatusReporter) — отображение шагов теста
- **Время шагов** (StepTimingService) — хронометраж выполнения
- **Таймер сканирования** (StepTimingService.ScanTiming) — отдельный таймер для шага сканирования
- **Рецепты** (RecipeProvider) — параметры текущего теста
- **BoilerState** — данные о котле (серийник, артикул)
- **История ошибок** (ErrorService.History) — лог ошибок теста
- **Результаты** (TestResultsService) — измеренные значения

## Goals

- Оператор видит результаты завершённого теста до начала нового
- Новый тест начинается с чистого состояния
- Логика очистки централизована и понятна

## Non-Goals

- Изменение логики сброса PLC (SoftReset, HardReset)
- Изменение логики Repeat (OK/NOK)

## Decisions

### Decision 1: Двухфазная очистка

**Фаза 1 — При завершении теста (TestCompleted):**
```
ClearForTestCompletion():
├─ StatusReporter.ClearAllExceptScan()  // Очистить грид
├─ StepTimingService.Clear()            // Очистить время
├─ RecipeProvider.Clear()               // Очистить рецепты
├─ BoilerState.Clear()                  // Очистить данные котла
└─ ErrorService.IsHistoryEnabled = false // Выключить запись истории
```

**Что НЕ чистим:**
- ErrorService.History — оператор смотрит ошибки
- TestResultsService — оператор смотрит результаты

**Фаза 2 — При начале нового цикла (SetAcceptingInput = true):**
```
ResetForNewCycle():
└─ StepTimingService.ResetScanTiming()  // Сбросить и запустить таймер сканирования
```

**Фаза 3 — При начале нового теста (перед включением истории):**
```
ClearForNewTestStart():
├─ ErrorService.ClearHistory()    // Очистить историю ошибок
└─ TestResultsService.Clear()     // Очистить результаты
```

### Decision 2: Место вызова ResetScanTiming

Вызывать в `SetAcceptingInput(true)` — это момент когда система готова принять новый штрихкод.

Таймер сканирования:
- Запускается при `SetAcceptingInput(true)` → `ResetScanTiming()`
- Останавливается в `StopScanTiming()` после успешного ScanStep

### Decision 3: Место вызова ClearForNewTestStart

Вызывать **перед** `IsHistoryEnabled = true` в:
- `ExecutePreExecutionPipelineAsync` — обычный запуск
- `ExecuteRepeatPipelineAsync` — OK повтор
- `ExecuteNokRepeatPipelineAsync` — NOK повтор (внутри вызывает ExecutePreExecutionPipelineAsync)

### Decision 4: Упрощение существующих методов

ClearForRepeat и ClearForNokRepeat больше не должны:
- Чистить историю ошибок
- Чистить результаты

Это делает ClearForNewTestStart при запуске pipeline.

## Data Flow

```
[Тест завершён OK/NOK]
        │
        ▼
HandleTestCompletionAsync()
├─ SetTestResult(1 or 2)
└─ return TestCompleted
        │
        ▼
HandleCycleExit(TestCompleted)
├─ SetTestRunning(false)
└─ ClearForTestCompletion()     ← NEW
        │
        ▼
[Оператор видит результаты]
        │
        ▼
[Следующая итерация цикла]
        │
        ▼
RunSingleCycleAsync()
└─ SetAcceptingInput(true)
   └─ ResetScanTiming()          ← NEW (таймер сканирования стартует)
        │
        ▼
[Новый штрихкод сканирован]
        │
        ▼
ExecutePreExecutionPipelineAsync()
├─ ClearForNewTestStart()        ← NEW (история + результаты)
├─ ScanStep...
├─ StopScanTiming()              (таймер сканирования останавливается)
├─ IsHistoryEnabled = true
└─ ...
```

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Двойная очистка при Repeat | ClearForRepeat/ClearForNokRepeat уже делают часть очистки — убрать дубликаты |
| Оператор не успеет посмотреть | Данные сохраняются до следующего сканирования |

## Open Questions

- Нужно ли показывать диалог подтверждения перед очисткой старых результатов?
  - Ответ: Нет, это стандартный workflow — новый тест = новые данные
