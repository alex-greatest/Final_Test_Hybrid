# Change: Handle Resets During Test Completion

## Why

**Этап 6** из `TestCompletionPlan.md` — обработка сбросов PLC во время процесса завершения теста.

### Проблема 1: Неполная очистка при сбросе

Сейчас `ClearStateOnReset()` очищает **меньше**, чем `ClearForTestCompletion()`:

| Компонент | ClearStateOnReset | ClearForTestCompletion |
|-----------|-------------------|------------------------|
| BoilerState.Clear() | ✅ | ✅ |
| PhaseState.Clear() | ✅ | ❌ |
| ClearBarcode() | ✅ | ✅ |
| IsHistoryEnabled = false | ✅ | ✅ |
| **StepTimingService.Clear()** | ❌ | ✅ |
| **RecipeProvider.Clear()** | ❌ | ✅ |
| StatusReporter.ClearAllExceptScan() | отдельно | ✅ |

Требование: **при сбросе должно очищаться то же самое, что и при завершении теста**.

### Проблема 2: Изображение не скрывается при сбросе

Когда `TestCompletionCoordinator` ожидает решения PLC (End/Req_Repeat), может прийти сигнал сброса:
- **Мягкий сброс** (`PlcResetCoordinator.OnForceStop`) — `wasInScanPhase = true`
- **Жёсткий сброс** (`ErrorCoordinator.OnReset`) — `wasInScanPhase = false`

Диалоги уже подписаны на сбросы, но `TestCompletionUiState` **не реагирует** — изображение может остаться видимым.

## What Changes

### 1. Выровнять ClearStateOnReset с ClearForTestCompletion

Добавить в `ClearStateOnReset()`:
```csharp
infra.StepTimingService.Clear();
infra.RecipeProvider.Clear();
```

### 2. Подписать TestCompletionUiState на события сброса

Изображение результата должно скрываться при любом сбросе:
- `PlcResetCoordinator.OnForceStop` → `HideImage()`
- `ErrorCoordinator.OnReset` → `HideImage()`

## Impact

- **Affected code:**
  - `PreExecutionCoordinator.cs` — обновить `ClearStateOnReset()`
  - `TestCompletionUiState.cs` — добавить подписки на сброс
  - `StepsServiceExtensions.cs` — зарегистрировать зависимости для подписок

- **Breaking changes:** Нет

- **Risk:** Низкий — выравнивание поведения, защитное поведение

## Что очищается при сбросах (после изменений)

### Единая таблица очистки

| Компонент | Мягкий сброс | Жёсткий сброс | Завершение теста |
|-----------|--------------|---------------|------------------|
| **BoilerState** | ✅ по AskEnd | ✅ сразу | ✅ |
| **PhaseState** | ✅ по AskEnd | ✅ сразу | — |
| **CurrentBarcode** | ✅ по AskEnd | ✅ сразу | ✅ |
| **Grid (StatusReporter)** | ✅ по AskEnd | ✅ сразу | ✅ |
| **StepTimingService** | ✅ по AskEnd | ✅ сразу | ✅ |
| **RecipeProvider** | ✅ по AskEnd | ✅ сразу | ✅ |
| **IsHistoryEnabled** | `false` | `false` | `false` |
| **ShowResultImage** | ✅ сразу | ✅ сразу | ✅ в finally |
| **ErrorService.History** | ❌ | ❌ | ❌ |
| **TestResultsService** | ❌ | ❌ | ❌ |

### Когда очищается история и результаты

История ошибок и результаты очищаются **НЕ при сбросе/завершении**, а **перед началом нового теста** в `ClearForNewTestStart()`.

**Почему:** Оператор должен видеть результаты предыдущего теста до сканирования нового котла.

## Картинка результата: когда появляется/скрывается

### Схема

```
[Тест завершён — OnSequenceCompleted]
        │
        ▼
HandleTestCompletionAsync()
        │
        ├─► ShowImage(testResult)     ← КАРТИНКА ПОЯВЛЯЕТСЯ
        │
        ▼
HandleTestCompletedAsync()
        │
        ├─ Write End = true
        ├─ Wait End = false (PLC сбросит)
        ├─ Delay 1 сек
        ├─ Read Req_Repeat
        │
        ├─── Req_Repeat = false ──► HandleFinish (сохранение) ──► Finished
        │                                                              │
        ├─── Req_Repeat = true, OK ──► AskRepeat = true ──► RepeatRequested
        │                                                              │
        └─── Req_Repeat = true, NOK ──► Save + Rework + AskRepeat ──► NokRepeatRequested
                                                                       │
        ◄──────────────────────────────────────────────────────────────┘
        │
        ▼
finally: HideImage()                  ← КАРТИНКА СКРЫВАЕТСЯ
        │
        ▼
HandleCycleExit(reason)
        │
        ├─ TestCompleted ──► ClearForTestCompletion() ──► [Ждать новый баркод]
        ├─ RepeatRequested ──► ClearForRepeat() ──► [Запуск теста заново]
        └─ NokRepeatRequested ──► ClearForNokRepeat() ──► [Подготовка + запуск]
```

### Таблица моментов

| Момент | Код | Файл:строка |
|--------|-----|-------------|
| **Появляется** | `CompletionUiState.ShowImage(testResult)` | `PreExecutionCoordinator.MainLoop.cs:165` |
| **Скрывается (норм.)** | `CompletionUiState.HideImage()` в finally | `PreExecutionCoordinator.MainLoop.cs:183` |
| **Скрывается (сброс)** | `HideImage()` по событию OnForceStop/OnReset | **НОВОЕ** — подписка в TestCompletionUiState |

### Время жизни картинки

| Сценарий | Картинка видна во время |
|----------|-------------------------|
| **Завершение (Finished)** | Write End → Wait End=false → Delay → Save → HideImage |
| **OK повтор** | Write End → Wait End=false → Delay → AskRepeat → HideImage |
| **NOK повтор** | Write End → Wait End=false → Delay → Save → Rework → AskRepeat → HideImage |
| **Сброс PLC** | Немедленно скрывается по событию |

## Flow очистки для всех сценариев

### 1. TestCompleted — Завершение теста (OK/NOK)

```
HandleCycleExit(TestCompleted)
        │
        ├─ BoilerState.SetTestRunning(false)
        │
        └─ ClearForTestCompletion()
            ├─ StatusReporter.ClearAllExceptScan()
            ├─ StepTimingService.Clear()
            ├─ RecipeProvider.Clear()
            ├─ BoilerState.Clear()
            ├─ ClearBarcode()
            └─ IsHistoryEnabled = false
```

**Следующий шаг:** Ожидание нового баркода

---

### 2. RepeatRequested — OK повтор

```
HandleCycleExit(RepeatRequested)
        │
        ├─ ClearForRepeat()
        │   ├─ IsHistoryEnabled = false
        │   ├─ StatusReporter.ClearAllExceptScan()
        │   ├─ StepTimingService.Clear()
        │   └─ TestCoordinator.ResetForRepeat()
        │
        └─ _skipNextScan = true
```

**НЕ чистится:** BoilerState, CurrentBarcode, RecipeProvider (котёл тот же)

**Следующий шаг:** `ExecuteRepeatPipelineAsync()` → запуск теста без сканирования

---

### 3. NokRepeatRequested — NOK повтор с подготовкой

```
HandleCycleExit(NokRepeatRequested)
        │
        ├─ ClearForNokRepeat()
        │   ├─ BoilerState.Clear()
        │   ├─ PhaseState.Clear()
        │   ├─ IsHistoryEnabled = false
        │   ├─ StatusReporter.ClearAllExceptScan()
        │   ├─ StepTimingService.Clear()
        │   └─ RecipeProvider.Clear()              ← ДОБАВИТЬ
        │
        ├─ _skipNextScan = true
        └─ _executeFullPreparation = true
```

**НЕ чистится:** CurrentBarcode (нужен для подготовки!)

**Следующий шаг:** `ExecuteNokRepeatPipelineAsync()` → полная подготовка + запуск

---

### 4. SoftReset — Мягкий сброс (по AskEnd)

```
PlcResetCoordinator.OnForceStop
        │
        ├─ ... (остановка тестов, диалогов) ...
        │
        ▼
PlcResetCoordinator.OnAskEndReceived
        │
        └─ HandleGridClear()
            ├─ ClearStateOnReset()
            │   ├─ BoilerState.Clear()
            │   ├─ PhaseState.Clear()
            │   ├─ ClearBarcode()
            │   ├─ IsHistoryEnabled = false
            │   ├─ StepTimingService.Clear()      ← ДОБАВИТЬ
            │   ├─ RecipeProvider.Clear()          ← ДОБАВИТЬ
            │   └─ _lastSuccessfulContext = null
            │
            └─ StatusReporter.ClearAllExceptScan()
```

**Следующий шаг:** Ожидание нового баркода

---

### 5. HardReset — Жёсткий сброс (сразу)

```
ErrorCoordinator.OnReset
        │
        └─ HandleCycleExit(HardReset)
            ├─ ClearStateOnReset()
            │   ├─ BoilerState.Clear()
            │   ├─ PhaseState.Clear()
            │   ├─ ClearBarcode()
            │   ├─ IsHistoryEnabled = false
            │   ├─ StepTimingService.Clear()      ← ДОБАВИТЬ
            │   ├─ RecipeProvider.Clear()          ← ДОБАВИТЬ
            │   └─ _lastSuccessfulContext = null
            │
            └─ StatusReporter.ClearAllExceptScan()
```

**Следующий шаг:** Ожидание нового баркода

---

## Сводная таблица очистки

| Компонент | TestCompleted | OK повтор | NOK повтор | SoftReset | HardReset |
|-----------|:-------------:|:---------:|:----------:|:---------:|:---------:|
| **BoilerState** | ✅ | ❌ | ✅ | ✅ | ✅ |
| **PhaseState** | ❌ | ❌ | ✅ | ✅ | ✅ |
| **CurrentBarcode** | ✅ | ❌ | ❌ | ✅ | ✅ |
| **Grid** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **StepTimingService** | ✅ | ✅ | ✅ | ✅* | ✅* |
| **RecipeProvider** | ✅ | ❌ | ✅* | ✅* | ✅* |
| **IsHistoryEnabled** | false | false | false | false | false |

**\*** — будет добавлено/исправлено в этом изменении

### Пояснения к компонентам

| Компонент | Что хранит |
|-----------|------------|
| **BoilerState** | Серийный номер, артикул, тип котла, результат теста, таймер |
| **PhaseState** | Текущая фаза выполнения |
| **CurrentBarcode** | Отсканированный штрихкод |
| **Grid** | Статусы шагов в UI (StatusReporter) |
| **StepTimingService** | Время выполнения шагов |
| **RecipeProvider** | Загруженные рецепты для текущего теста |
| **IsHistoryEnabled** | Флаг записи ошибок в историю |

---

## Что НЕ чистится никогда (до нового теста)

| Компонент | Когда чистится |
|-----------|----------------|
| **ErrorService.History** | `ClearForNewTestStart()` перед `IsHistoryEnabled = true` |
| **TestResultsService** | `ClearForNewTestStart()` перед `IsHistoryEnabled = true` |

**Почему:** Оператор должен видеть результаты предыдущего теста до сканирования нового котла.

## Design Decision

**Рекомендуемый подход:**
1. Добавить `StepTimingService.Clear()` и `RecipeProvider.Clear()` в `ClearStateOnReset()`
2. Подписать `TestCompletionUiState` на события сброса через конструктор
