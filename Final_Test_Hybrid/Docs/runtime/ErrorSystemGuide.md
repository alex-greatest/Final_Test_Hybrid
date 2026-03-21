# ErrorSystemGuide.md

Руководство по работе с системой ошибок.

## Обзор

Система мониторинга и отображения ошибок:
- **ПЛК-ошибки** — мониторятся автоматически через `PlcErrorMonitorService`, кроме deferred-исключений `П-403-03` / `П-407-03`
- **Программные ошибки** — поднимаются/снимаются из кода через `IErrorService`
- **UI** — `ActiveErrorsGrid` (текущие) + `ErrorHistoryGrid` (журнал) + `ErrorResetButton`

## Быстрый старт

### Поднять ошибку

```csharp
// Inject в конструктор
public MyService(IErrorService errorService) { ... }

// Глобальная ошибка (без контекста шага)
errorService.Raise(ErrorDefinitions.DatabaseError, "Детали ошибки");

// Ошибка в контексте шага
errorService.RaiseInStep(ErrorDefinitions.StepTimeout, stepId, stepName, "Детали");
```

### Снять ошибку

```csharp
// По коду ошибки
errorService.Clear(ErrorDefinitions.DatabaseError.Code);

// Все программные ошибки (Application source)
errorService.ClearActiveApplicationErrors();

// Все активные ошибки
errorService.ClearAllActiveErrors();
```

## Определения ошибок

Все ошибки определяются в `Models/Errors/ErrorDefinitions.cs`:

```csharp
public static readonly ErrorDefinition MyNewError = new(
    Code: "G020",                           // Уникальный код
    Description: "Описание для пользователя",
    PlcTag: null,                           // или "ns=3;s=..." для ПЛК
    Severity: ErrorSeverity.Warning,        // Info, Warning, Critical
    ActivatesResetButton: true,             // Показывать красную кнопку сброса
    PossibleStepIds: null);                 // null = глобальная (контекст шага добавляется при вызове)
```

**Текущее правило проекта:** для всех ошибок в `ErrorDefinitions*.cs` используется `ActivatesResetButton: true`.

**Не забудь добавить в `All`:**
```csharp
public static IReadOnlyList<ErrorDefinition> All => [
    EmergencyStop, OpcConnectionLost, DatabaseError, ..., MyNewError
];
```

## Типы ошибок

| Тип | PlcTag | PossibleStepIds | Пример |
|-----|--------|-----------------|--------|
| Глобальная ПЛК | `"ns=3;s=..."` | `null` | EmergencyStop |
| Глобальная программная | `null` | `null` | OpcConnectionLost, DatabaseError, StepTimeout, StepExecutionFailed |

## Автоматическая обработка

### PlcErrorMonitorService

Автоматически подписывается на все `ErrorDefinitions.PlcErrors` при старте,
кроме deferred-исключений `П-403-03` и `П-407-03`.
При изменении тега вызывает `RaisePlc` / `ClearPlc`.

### GasValveTubeDeferredErrorService

Для ошибок `П-403-03` (`Gas/Set_Gas_and_P_Burner_Max_Levels`) и
`П-407-03` (`Gas/Set_Gas_and_P_Burner_Min_Levels`) используется отдельный deferred-path:

- при `Al_NotConnectSensorPGB=true` нижняя строка сразу показывает
  `Не подключена трубка газового клапана`, но только пока активен
  соответствующий gas-step;
- `RaisePlc(...)` выполняется только если тот же тег остаётся активным 30 секунд;
- при `false` main message очищается сразу;
- при выходе из соответствующего шага active message-state и pending 30-секундный
  defer тоже снимаются сразу;
- если ошибка уже была поднята, `ClearPlc(...)` вызывается сразу на `false`.

### ErrorCoordinator

| Событие | Действие |
|---------|----------|
| PlcConnectionLost | `Raise(OpcConnectionLost)` |
| TagTimeout | `Raise(TagReadTimeout)` |
| Recovery (Auto restored) | `Clear(OpcConnectionLost)`, `Clear(TagReadTimeout)` |
| Reset | `Clear(TagReadTimeout)` + `OnReset` |
| ForceStop | Снимает interrupt (`ClearCurrentInterrupt`), без прямого `ClearActiveApplicationErrors()` |

`ClearActiveApplicationErrors()` в reset-путях выполняется в координаторах выполнения
(`PreExecutionCoordinator`, `TestExecutionCoordinator`) в их обработчиках остановки/завершения.

### TestExecutionCoordinator

| Событие | Действие |
|---------|----------|
| Step Error | Диалог Retry/Skip показывается **СРАЗУ** (Channel-based signaling) |
| Complete | `ClearActiveApplicationErrors()` |

**Примечание:** Ошибки шагов обрабатываются через `Channel<bool>` сигнал — диалог появляется немедленно, другие колонки продолжают работу. См. `../execution/StateManagementGuide.md` секция "Channel-based Error Handling".

### PreExecutionCoordinator

| Событие | Действие |
|---------|----------|
| SoftStop | `ClearActiveApplicationErrors()` (через `SignalResolution`, при активных подписках `PreExecution`) |
| HardReset | `ClearActiveApplicationErrors()` (через `SignalResolution`, при активных подписках `PreExecution`) |

## UI Компоненты

### ErrorResetButton

```razor
<ErrorResetButton />
```
- Зелёная "Нет ошибок" если `!HasResettableErrors`
- Красная "Сброс ошибки" если есть ошибки с `ActivatesResetButton = true`

### FloatingErrorBadgeHost

```razor
<FloatingErrorBadgeHost />
```
- Плавающий жёлтый треугольник показывается, когда есть ошибки с `ActivatesResetButton = true` и включён `UseFloatingErrorBadge`.
- При переходе из состояния `нет сбрасываемых ошибок` в состояние `есть сбрасываемые ошибки` бейдж должен начать мерцать сразу после появления.
- Это правило распространяется и на стартовые ошибки, которые уже активны до полного монтирования UI: компонент делает bounded retry JS-перезапуска анимации, пока бейдж остаётся видимым.
- Геометрия бейджа читается из `Settings:FloatingErrorBadge` в `appsettings.json` при старте приложения; текущие значения по умолчанию повторяют исторический вид (`84x84`, иконка `78`, bubble `24x24`, шрифт `13`).
- Изменение размеров через `appsettings.json` применяется только после перезапуска приложения; hot-reload конфигурации для floating badge не используется.

### ActiveErrorsGrid / ErrorHistoryGrid

```razor
<ActiveErrorsGrid />
<ErrorHistoryGrid />
```

Используются внутри `ErrorsTab.razor`.

## Подписка на изменения

```csharp
// В компоненте
@inject IErrorService ErrorService
@implements IAsyncDisposable

@code {
    private bool _disposed;

    protected override void OnInitialized()
    {
        ErrorService.OnActiveErrorsChanged += OnErrorsChanged;
    }

    private void OnErrorsChanged()
    {
        _ = InvokeAsync(() =>
        {
            if (_disposed) return;
            StateHasChanged();
        });
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        ErrorService.OnActiveErrorsChanged -= OnErrorsChanged;
        return ValueTask.CompletedTask;
    }
}
```

## Модели данных

### ActiveError

```csharp
public record ActiveError
{
    public DateTime Time { get; init; }
    public string Code { get; init; }
    public string Description { get; init; }
    public ErrorSeverity Severity { get; init; }
    public ErrorSource Source { get; init; }      // Application или Plc
    public string? StepId { get; init; }
    public string? StepName { get; init; }
    public bool ActivatesResetButton { get; init; }
}
```

### ErrorHistoryItem

```csharp
public record ErrorHistoryItem
{
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }       // null = ещё активна
    public string Code { get; init; }
    public string Description { get; init; }
    public ErrorSeverity Severity { get; init; }
    public ErrorSource Source { get; init; }
    public string? StepId { get; init; }
    public string? StepName { get; init; }
}
```

## Файловая структура

```
Models/Errors/
├── ErrorDefinition.cs      # Record + ErrorSeverity enum
├── ErrorDefinitions.cs     # Статические определения всех ошибок
├── ActiveError.cs          # Модель активной ошибки
└── ErrorHistoryItem.cs     # Модель записи истории

Services/Errors/
├── IErrorService.cs        # Интерфейс
├── ErrorService.cs         # Реализация (singleton, thread-safe)
├── IPlcErrorMonitorService.cs
├── PlcErrorMonitorService.cs
├── GasValveTubeDeferredErrorService.cs
└── PlcErrorValueNormalizer.cs

Components/Errors/
├── ErrorsTab.razor         # Вкладка с табами Active/History
├── ActiveErrorsGrid.razor  # Грид активных ошибок
├── ErrorHistoryGrid.razor  # Грид истории
└── ErrorResetButton.razor  # Кнопка сброса
```

## Добавление новой ПЛК-ошибки

1. Добавь определение в `ErrorDefinitions.cs`:
```csharp
public static readonly ErrorDefinition OverheatAlarm = new(
    "P001", "Перегрев оборудования",
    PlcTag: "ns=3;s=\"DB_Alarms\".\"Overheat\"",
    Severity: ErrorSeverity.Critical,
    ActivatesResetButton: true);
```

2. Добавь в `All`:
```csharp
public static IReadOnlyList<ErrorDefinition> All => [
    ..., OverheatAlarm
];
```

3. Если ошибка не требует deferred-поведения, готово:
   `PlcErrorMonitorService` автоматически подпишется при старте.

### Исключение: deferred PLC-ошибка

Если оператор должен сначала увидеть low-priority main message, а сама PLC-ошибка
должна подниматься только после выдержки времени, такую ошибку нельзя оставлять
в generic immediate-path `PlcErrorMonitorService`.
Нужно выделять отдельный owner-service по аналогии с `GasValveTubeDeferredErrorService`.

## История ошибок (IsHistoryEnabled)

История записывается только когда `IsHistoryEnabled = true`.

| Момент | Действие | Где |
|--------|----------|-----|
| После успешного ScanStep | `IsHistoryEnabled = true` | `PreExecutionCoordinator.Pipeline` |
| При завершении теста (OK/NOK) | `IsHistoryEnabled = false` | `PreExecutionCoordinator.HandleTestCompletionAsync` |
| При сбросе PLC (любой) | `IsHistoryEnabled = false` | `PreExecutionCoordinator.ClearStateOnReset` |

### Поведение при включении

При установке `IsHistoryEnabled = true` все текущие активные ошибки из `_activeErrors` автоматически копируются в историю. Это гарантирует, что ошибки, возникшие ДО сканирования (например, ПЛК-ошибки), попадут в журнал и будут корректно закрыты при их снятии.

**Сценарий:**
1. ПЛК-ошибка возникает ДО сканирования → попадает в `_activeErrors`
2. ScanStep успешно завершается → `IsHistoryEnabled = true` → ошибка копируется в историю
3. Ошибка исправляется → `CloseHistoryRecord()` находит запись и ставит `EndTime`

### Поведение при выключении

При установке `IsHistoryEnabled = false` все открытые записи в истории (где `EndTime == null`) автоматически закрываются — устанавливается `EndTime = DateTime.Now`. Это гарантирует, что при сохранении результатов теста все записи истории имеют заполненное время окончания.

**Сценарий:**
1. Тест завершается (OK или NOK)
2. `HandleTestCompletionAsync` устанавливает `IsHistoryEnabled = false`
3. Все открытые записи получают `EndTime` = момент завершения теста
4. Результаты сохраняются с полной историей ошибок

## Ограничения

- История ограничена **1000 записей** (FIFO)
- Дубликаты ошибок (по Code) игнорируются — нельзя поднять одну ошибку дважды
- ПЛК-ошибки не очищаются через `ClearActiveApplicationErrors()` — только через сигнал с ПЛК
