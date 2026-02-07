# MessageService: Система управления сообщениями

## Обзор

Система отображает контекстные сообщения пользователю в зависимости от текущего состояния приложения. Использует **правила с приоритетами** — первое сработавшее правило с наивысшим приоритетом определяет сообщение.

```
┌─────────────────────────────────────────────────────────────┐
│                    MessageHelper (UI)                       │
│                         ▲                                   │
│                         │ CurrentMessage                    │
│                         │                                   │
│                  ┌──────┴──────┐                            │
│                  │MessageService│◄── OnChange               │
│                  └──────────────┘                           │
│                         ▲                                   │
│     ┌───────────────────┼───────────────────┐               │
│     │                   │                   │               │
│ OperatorState    ScanModeController    BoilerState          │
│ AutoReady        ExecutionPhaseState   ErrorCoordinator     │
│ Connection       PlcResetCoordinator                        │
└─────────────────────────────────────────────────────────────┘
```

---

## MessageService

**Файл:** `Services/Main/Messages/MessageService.cs`

Центральный сервис управления сообщениями. Использует массив правил с приоритетами.

```csharp
public class MessageService
{
    private readonly (int priority, Func<bool> condition, Func<string> message)[] _rules;

    // Зависимости
    private readonly OperatorState _operator;
    private readonly AutoReadySubscription _autoReady;
    private readonly OpcUaConnectionState _connection;
    private readonly ScanModeController _scanMode;
    private readonly ExecutionPhaseState _phaseState;
    private readonly ErrorCoordinator _errorCoord;
    private readonly PlcResetCoordinator _resetCoord;
    private readonly BoilerState _boilerState;

    // Возвращает сообщение от первого сработавшего правила с наивысшим приоритетом
    public string CurrentMessage { get; }

    // Уведомление об изменении
    public void NotifyChanged();
}
```

---

## Таблица правил

| Приоритет | Условие | Сообщение |
|-----------|---------|-----------|
| 200 | `!Connection && ResetActive` | "Потеря связи с PLC. Выполняется сброс..." |
| 190 | `TagTimeout && ResetActive` | "Нет ответа от ПЛК. Выполняется сброс..." |
| 180 | `!Connection` | "Нет связи с PLC" |
| 170 | `TagTimeout` | "Нет ответа от ПЛК" |
| 160 | `!AutoReady && ResetActive` | "Нет автомата. Выполняется сброс..." |
| 150 | `ResetActive` | "Сброс теста..." |
| 140 | `!IsAuthenticated` | "Войдите в систему" |
| 130 | `IsAuthenticated && !AutoReady` | "Ожидание автомата" |
| 125 | `CurrentInterrupt == BoilerLock` | "Блокировка котла. Ожидание восстановления" |
| **120** | `ScanModeEnabled && !IsTestRunning && Phase == null` | "Отсканируйте серийный номер котла" |
| 110 | `Phase != null` | Сообщение фазы (GetPhaseMessage) |

**Фазы выполнения (правило 110):**

| Phase | Сообщение |
|-------|-----------|
| `BarcodeReceived` | "Штрихкод получен" |
| `ValidatingSteps` | "Проверка шагов..." |
| `ValidatingRecipes` | "Проверка рецептов..." |
| `LoadingRecipes` | "Загрузка рецептов..." |
| `CreatingDbRecords` | "Создание записей в БД..." |
| `WaitingForAdapter` | "Подсоедините адаптер к котлу и нажмите \"Блок\"" |

---

## Правило 125 (BoilerLock)

Правило показывает сообщение:

- Условие: `CurrentInterrupt == InterruptReason.BoilerLock`
- Сообщение: `"Блокировка котла. Ожидание восстановления"`
- Приоритет: `125` (ниже reset/connection/auto-ready критичных правил)

Когда сообщение очищается:

1. `BoilerLockRuntimeService` определяет, что условие блокировки больше не выполняется.
2. Вызывает `ErrorCoordinator.ForceStop()`.
3. Внутри `ForceStop()` выполняется `ClearCurrentInterrupt()`.
4. Поднимается `OnInterruptChanged`.
5. `MessageService` пересчитывает правила, и правило 125 перестаёт срабатывать.

Подробная логика с условиями `1005==1/2` и `1153=0`: `Docs/BoilerLockGuide.md`.

---

## Логика правила 120 (Сканирование)

Сообщение "Отсканируйте серийный номер котла" показывается только когда:
- `ScanModeEnabled = true` — режим сканирования включён
- `IsTestRunning = false` — тест НЕ выполняется
- `Phase == null` — нет активной фазы выполнения

**Управление `IsTestRunning`:**

| Момент | Действие | Место |
|--------|----------|-------|
| BlockBoilerAdapterStep запускается | `SetTestRunning(true)` | `BlockBoilerAdapterStep.ExecuteAsync()` |
| Тест завершён | `SetTestRunning(false)` | `PreExecutionCoordinator.HandleCycleExit()` |
| Сброс (Clear) | `_isTestRunning = false` | `BoilerState.Clear()` |

**Поток:**

```
Ожидание сканирования  ──► ScanStep (подготовка) ──► BlockBoilerAdapterStep ──► Тест ──► Завершение
      │                         │                         │                     │           │
IsTestRunning=false      Phase через 110          IsTestRunning=true      Phase=null   IsTestRunning=false
      │                         │                         │                     │           │
"Отсканируйте..."        "Загрузка..."           "Подсоедините..."         (пусто)    "Отсканируйте..."
```

---

## Потокобезопасность

| Компонент | Защита |
|-----------|--------|
| `BoilerState.IsTestRunning` | `lock (_lock)` |
| `BoilerState.SetTestRunning()` | `lock (_lock)` |
| `MessageService.CurrentMessage` | `lock (_lock)` |
| Вложенные локи | Разные объекты — deadlock невозможен |
| Stale read | Допустим для UI |

---

## Подписки на изменения

```csharp
private void SubscribeToChanges()
{
    _operator.OnStateChanged += NotifyChanged;
    _autoReady.OnStateChanged += NotifyChanged;
    _connection.ConnectionStateChanged += _ => NotifyChanged();
    _scanMode.OnStateChanged += NotifyChanged;
    _phaseState.OnChanged += NotifyChanged;
    _errorCoord.OnInterruptChanged += NotifyChanged;
    _resetCoord.OnActiveChanged += NotifyChanged;
    _boilerState.OnChanged += NotifyChanged;
}
```

---

## Жизненный цикл сообщений

### Сценарий: Успешное сканирование и тест

```
Состояние                              Сообщение              Приоритет
──────────────────────────────────────────────────────────────────────────
1. Не залогинен                       "Войдите в систему"         140
2. Залогинен, нет автомата            "Ожидание автомата"         130
3. Автомат есть                       "Отсканируйте..."           120
4. Штрихкод получен                   "Штрихкод получен"          110
5. Проверка шагов                     "Проверка шагов..."         110
6. Загрузка рецептов                  "Загрузка рецептов..."      110
7. Ожидание адаптера                  "Подсоедините адаптер..."   110
8. Тест выполняется                   ""                          —
9. Тест завершён                      "Отсканируйте..."           120
```

### Сценарий: Потеря связи во время теста

```
Состояние                              Сообщение              Приоритет
──────────────────────────────────────────────────────────────────────────
1. Тест выполняется                   ""                          —
2. Потеря связи                       "Нет связи с PLC"           180
3. Инициирован сброс                  "Потеря связи... сброс..."  200
4. Связь восстановлена                "Сброс теста..."            150
5. Сброс завершён                     "Отсканируйте..."           120
```

---

## Ключевые файлы

| Файл | Назначение |
|------|------------|
| `Services/Main/Messages/MessageService.cs` | Центральный сервис сообщений |
| `Services/Main/Messages/ExecutionPhaseState.cs` | Состояние фазы выполнения |
| `Models/BoilerState.cs` | Состояние котла (IsTestRunning) |
| `Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs` | Контроллер режима сканирования |
| `Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.cs` | Координатор ошибок |
| `Services/Steps/Infrastructure/PlcReset/PlcResetCoordinator.cs` | Координатор сброса PLC |

---

## См. также

- [CLAUDE.md](CLAUDE.md) — общие правила и паттерны проекта
- [ErrorCoordinatorGuide.md](ErrorCoordinatorGuide.md) — обработка прерываний
- [PlcResetGuide.md](PlcResetGuide.md) — логика сброса PLC
- [Docs/BoilerLockGuide.md](Docs/BoilerLockGuide.md) — runtime-логика блокировок котла
