# План реализации системы ошибок

## Обзор

Система мониторинга и отображения ошибок:
- **ПЛК-ошибки** — мониторятся всегда, автоматически появляются/исчезают по сигналам
- **Программные ошибки** — поднимаются/снимаются из кода
- **Индикация** — ActiveErrors (текущие) + History (журнал)
- **UI** — гриды + кнопка сброса

---

## Часть 1: Модели

### 1.1 Создать `Models/Errors/ErrorDefinition.cs`

```csharp
namespace Final_Test_Hybrid.Models.Errors;

public record ErrorDefinition(
    string Code,
    string Description,
    string? PlcTag = null,
    ErrorSeverity Severity = ErrorSeverity.Warning,
    bool ActivatesResetButton = false,
    string[]? PossibleStepIds = null)
{
    public bool IsPlcBound => PlcTag is not null;
    public bool IsGlobal => PossibleStepIds is null;
}

public enum ErrorSeverity { Info, Warning, Critical }
```

### 1.2 Создать `Models/Errors/ErrorDefinitions.cs`

```csharp
namespace Final_Test_Hybrid.Models.Errors;

public static class ErrorDefinitions
{
    // ═══════ ГЛОБАЛЬНЫЕ (PossibleStepIds = null) ═══════

    // ПЛК
    public static readonly ErrorDefinition EmergencyStop = new(
        "G001", "Аварийная остановка",
        PlcTag: "ns=3;s=\"DB_Safety\".\"EStop\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    // Программные
    public static readonly ErrorDefinition OpcConnectionLost = new(
        "G010", "Потеря связи с ПЛК",
        Severity: ErrorSeverity.Critical);

    public static readonly ErrorDefinition DatabaseError = new(
        "G011", "Ошибка базы данных",
        Severity: ErrorSeverity.Warning);

    // ═══════ ЛЮБОЙ ШАГ (PossibleStepIds = []) ═══════

    public static readonly ErrorDefinition StepTimeout = new(
        "S100", "Таймаут шага",
        PossibleStepIds: []);

    // ═══════ КОНКРЕТНЫЕ ШАГИ ═══════

    // TODO: Заполнить реальными ошибками

    // ═══════ ХЕЛПЕРЫ ═══════

    public static IReadOnlyList<ErrorDefinition> All => [
        EmergencyStop, OpcConnectionLost, DatabaseError, StepTimeout
    ];

    public static IEnumerable<ErrorDefinition> PlcErrors
        => All.Where(e => e.IsPlcBound);

    public static ErrorDefinition? ByCode(string code)
        => All.FirstOrDefault(e => e.Code == code);

    public static ErrorDefinition? ByPlcTag(string tag)
        => All.FirstOrDefault(e => e.PlcTag == tag);

    public static IEnumerable<ErrorDefinition> ForStep(string stepId)
        => All.Where(e => e.PossibleStepIds?.Contains(stepId) == true
                       || e.PossibleStepIds is { Length: 0 });
}
```

### 1.3 Обновить `Models/Errors/ActiveError.cs`

```csharp
namespace Final_Test_Hybrid.Models.Errors;

public record ActiveError
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime Time { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? StepId { get; init; }
    public string? StepName { get; init; }
    public ErrorSource Source { get; init; }
}

public enum ErrorSource { Plc, Application }
```

### 1.4 Обновить `Models/Errors/ErrorHistoryItem.cs`

```csharp
namespace Final_Test_Hybrid.Models.Errors;

public record ErrorHistoryItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? StepId { get; init; }
    public string? StepName { get; init; }
    public ErrorSource Source { get; init; }
}
```

---

## Часть 2: Сервисы

### 2.1 Создать `Services/Errors/IErrorService.cs`

```csharp
namespace Final_Test_Hybrid.Services.Errors;

public interface IErrorService
{
    // События для UI
    event Action? OnActiveErrorsChanged;
    event Action? OnHistoryChanged;

    // Получение данных (возвращает копию)
    IReadOnlyList<ActiveError> GetActiveErrors();
    IReadOnlyList<ErrorHistoryItem> GetHistory();

    // Программные ошибки
    void Raise(ErrorDefinition error, string? details = null);
    void RaiseInStep(ErrorDefinition error, string stepId, string stepName, string? details = null);
    void Clear(string errorCode);
    void ClearActiveApplicationErrors();

    // ПЛК ошибки (из PlcErrorMonitor)
    void RaisePlc(ErrorDefinition error, string? stepId = null, string? stepName = null);
    void ClearPlc(string errorCode);

    // Общее
    void ClearAllActiveErrors();
    void ClearHistory();

    // Для кнопки
    bool HasResettableErrors { get; }
    bool HasActiveErrors { get; }
}
```

### 2.2 Создать `Services/Errors/ErrorService.cs`

```csharp
namespace Final_Test_Hybrid.Services.Errors;

public sealed class ErrorService : IErrorService
{
    private readonly Lock _lock = new();
    private readonly List<ActiveError> _activeErrors = [];
    private readonly List<ErrorHistoryItem> _history = [];

    public event Action? OnActiveErrorsChanged;
    public event Action? OnHistoryChanged;

    public bool HasResettableErrors
    {
        get
        {
            lock (_lock)
            {
                return _activeErrors.Any(e =>
                    ErrorDefinitions.ByCode(e.Code)?.ActivatesResetButton == true);
            }
        }
    }

    public bool HasActiveErrors
    {
        get
        {
            lock (_lock)
            {
                return _activeErrors.Count > 0;
            }
        }
    }

    public IReadOnlyList<ActiveError> GetActiveErrors()
    {
        lock (_lock)
        {
            return _activeErrors.ToList();
        }
    }

    public IReadOnlyList<ErrorHistoryItem> GetHistory()
    {
        lock (_lock)
        {
            return _history.ToList();
        }
    }

    // ═══════ ПРОГРАММНЫЕ ОШИБКИ ═══════

    public void Raise(ErrorDefinition error, string? details = null)
        => AddError(error, ErrorSource.Application, null, null, details);

    public void RaiseInStep(ErrorDefinition error, string stepId, string stepName, string? details = null)
        => AddError(error, ErrorSource.Application, stepId, stepName, details);

    // ═══════ ПЛК ОШИБКИ ═══════

    public void RaisePlc(ErrorDefinition error, string? stepId = null, string? stepName = null)
        => AddError(error, ErrorSource.Plc, stepId, stepName, null);

    public void ClearPlc(string errorCode)
        => Clear(errorCode);

    // ═══════ ОЧИСТКА ═══════

    public void Clear(string errorCode)
    {
        var removed = false;

        lock (_lock)
        {
            var index = _activeErrors.FindIndex(e => e.Code == errorCode);
            if (index >= 0)
            {
                _activeErrors.RemoveAt(index);
                CloseHistoryRecord(errorCode);
                removed = true;
            }
        }

        if (removed)
        {
            NotifyChanges();
        }
    }

    public void ClearActiveApplicationErrors()
    {
        var cleared = false;

        lock (_lock)
        {
            var appErrors = _activeErrors
                .Where(e => e.Source == ErrorSource.Application)
                .ToList();

            foreach (var error in appErrors)
            {
                _activeErrors.Remove(error);
                CloseHistoryRecord(error.Code);
                cleared = true;
            }
        }

        if (cleared)
        {
            NotifyChanges();
        }
    }

    public void ClearAllActiveErrors()
    {
        var cleared = false;

        lock (_lock)
        {
            cleared = _activeErrors.Count > 0;

            foreach (var error in _activeErrors)
            {
                CloseHistoryRecord(error.Code);
            }

            _activeErrors.Clear();
        }

        if (cleared)
        {
            NotifyChanges();
        }
    }

    public void ClearHistory()
    {
        lock (_lock)
        {
            _history.Clear();
        }

        OnHistoryChanged?.Invoke();
    }

    // ═══════ PRIVATE ═══════

    private void AddError(ErrorDefinition def, ErrorSource source,
        string? stepId, string? stepName, string? details)
    {
        var shouldNotify = false;

        lock (_lock)
        {
            if (_activeErrors.Any(e => e.Code == def.Code))
            {
                return;
            }

            var description = string.IsNullOrEmpty(details)
                ? def.Description
                : $"{def.Description}: {details}";

            var now = DateTime.Now;

            _activeErrors.Add(new ActiveError
            {
                Time = now,
                Code = def.Code,
                Description = description,
                StepId = stepId,
                StepName = stepName,
                Source = source
            });

            _history.Add(new ErrorHistoryItem
            {
                StartTime = now,
                Code = def.Code,
                Description = description,
                StepId = stepId,
                StepName = stepName,
                Source = source
            });

            shouldNotify = true;
        }

        if (shouldNotify)
        {
            NotifyChanges();
        }
    }

    private void CloseHistoryRecord(string errorCode)
    {
        var item = _history.LastOrDefault(e => e.Code == errorCode && e.EndTime == null);
        if (item != null)
        {
            var index = _history.IndexOf(item);
            _history[index] = item with { EndTime = DateTime.Now };
        }
    }

    private void NotifyChanges()
    {
        OnActiveErrorsChanged?.Invoke();
        OnHistoryChanged?.Invoke();
    }
}
```

### 2.3 Создать `Services/Errors/IPlcErrorMonitorService.cs`

```csharp
namespace Final_Test_Hybrid.Services.Errors;

public interface IPlcErrorMonitorService
{
    Task StartAsync(CancellationToken ct);
    void SetCurrentStep(string? stepId, string? stepName);
}
```

### 2.4 Создать `Services/Errors/PlcErrorMonitorService.cs`

```csharp
namespace Final_Test_Hybrid.Services.Errors;

public sealed class PlcErrorMonitorService(
    IOpcSubscriptionService subscription,
    IErrorService errorService) : IPlcErrorMonitorService
{
    private string? _currentStepId;
    private string? _currentStepName;

    public async Task StartAsync(CancellationToken ct)
    {
        foreach (var error in ErrorDefinitions.PlcErrors)
        {
            await subscription.SubscribeAsync(error.PlcTag!, value =>
            {
                OnTagChanged(error, value);
                return Task.CompletedTask;
            }, ct);
        }
    }

    public void SetCurrentStep(string? stepId, string? stepName)
    {
        _currentStepId = stepId;
        _currentStepName = stepName;
    }

    private void OnTagChanged(ErrorDefinition error, object? value)
    {
        if (value is not bool isActive)
        {
            return;
        }

        string? stepId = null;
        string? stepName = null;

        if (!error.IsGlobal && _currentStepId != null)
        {
            stepId = _currentStepId;
            stepName = _currentStepName;
        }

        if (isActive)
        {
            errorService.RaisePlc(error, stepId, stepName);
        }
        else
        {
            errorService.ClearPlc(error.Code);
        }
    }
}
```

### 2.5 Удалить старые файлы

- `Services/Errors/IActiveErrorsService.cs`
- `Services/Errors/ActiveErrorsService.cs`
- `Services/Errors/IErrorHistoryService.cs`
- `Services/Errors/ErrorHistoryService.cs`

---

## Часть 3: DI регистрация

```csharp
services.AddSingleton<IErrorService, ErrorService>();
services.AddSingleton<IPlcErrorMonitorService, PlcErrorMonitorService>();
```

---

## Часть 4: Интеграция (очистка программных ошибок)

### 4.1 `ErrorCoordinator.Interrupts.cs` — ProcessInterruptAsync

```csharp
// Добавить зависимость IErrorService

// В ProcessInterruptAsync после LogInterrupt/NotifyInterrupt:
if (reason is InterruptReason.PlcConnectionLost or InterruptReason.TagTimeout)
{
    _errorService.ClearActiveApplicationErrors();
}
```

### 4.2 `PreExecutionCoordinator.Retry.cs`

```csharp
// Добавить зависимость IErrorService

private void HandleSoftStop()
{
    _externalSignal?.TrySetResult(PreExecutionResolution.SoftStop);
    _errorService.ClearActiveApplicationErrors();
}

private void HandleHardReset()
{
    _externalSignal?.TrySetResult(PreExecutionResolution.HardReset);
    _errorService.ClearActiveApplicationErrors();
}
```

### 4.3 `TestExecutionCoordinator.Execution.cs`

```csharp
// Добавить зависимость IErrorService

// В Complete():
_errorService.ClearActiveApplicationErrors();

// В Stop():
_errorService.ClearActiveApplicationErrors();
```

### 4.4 Запуск мониторинга ПЛК

В подходящем месте после подключения OPC:
```csharp
await plcErrorMonitorService.StartAsync(ct);
```

---

## Часть 5: UI компоненты

### 5.1 Создать `Components/Errors/ErrorResetButton.razor`

```razor
@inject IErrorService ErrorService
@implements IDisposable

<RadzenButton Style="height: 56px; min-width: 250px; font-size: 1.5rem; font-weight: 900; color: white"
              Text="@_buttonText"
              ButtonStyle="@_buttonStyle"
              Click="@OnClick" />

@code {
    private string _buttonText = "Нет ошибок";
    private ButtonStyle _buttonStyle = ButtonStyle.Success;

    protected override void OnInitialized()
    {
        ErrorService.OnActiveErrorsChanged += OnErrorsChanged;
        UpdateState();
    }

    private void OnErrorsChanged()
    {
        UpdateState();
        InvokeAsync(StateHasChanged);
    }

    private void UpdateState()
    {
        if (ErrorService.HasResettableErrors)
        {
            _buttonText = "Сброс ошибки";
            _buttonStyle = ButtonStyle.Danger;
        }
        else
        {
            _buttonText = "Нет ошибок";
            _buttonStyle = ButtonStyle.Success;
        }
    }

    private async Task OnClick()
    {
        if (ErrorService.HasResettableErrors)
        {
            // TODO: Отправить сигнал сброса в ПЛК
        }
    }

    public void Dispose()
    {
        ErrorService.OnActiveErrorsChanged -= OnErrorsChanged;
    }
}
```

### 5.2 Создать `Components/Errors/ActiveErrorsGrid.razor`

```razor
@inject IErrorService ErrorService
@implements IDisposable

<RadzenDataGrid Data="@_errors" TItem="ActiveError" AllowSorting="true">
    <Columns>
        <RadzenDataGridColumn Property="Time" Title="Время" Width="100px"
            FormatString="{0:HH:mm:ss}" />
        <RadzenDataGridColumn Property="Code" Title="Код" Width="80px" />
        <RadzenDataGridColumn Property="Description" Title="Описание" />
        <RadzenDataGridColumn Property="StepName" Title="Шаг" Width="120px">
            <Template Context="item">
                @(item.StepName ?? "—")
            </Template>
        </RadzenDataGridColumn>
        <RadzenDataGridColumn Property="Source" Title="Источник" Width="100px">
            <Template Context="item">
                @(item.Source == ErrorSource.Plc ? "ПЛК" : "Программа")
            </Template>
        </RadzenDataGridColumn>
    </Columns>
</RadzenDataGrid>

@code {
    private IReadOnlyList<ActiveError> _errors = [];

    protected override void OnInitialized()
    {
        ErrorService.OnActiveErrorsChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        _errors = ErrorService.GetActiveErrors();
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        ErrorService.OnActiveErrorsChanged -= Refresh;
    }
}
```

### 5.3 Создать `Components/Errors/ErrorHistoryGrid.razor`

```razor
@inject IErrorService ErrorService
@implements IDisposable

<RadzenDataGrid Data="@_history" TItem="ErrorHistoryItem" AllowSorting="true">
    <Columns>
        <RadzenDataGridColumn Property="StartTime" Title="Начало" Width="100px"
            FormatString="{0:HH:mm:ss}" />
        <RadzenDataGridColumn Property="EndTime" Title="Конец" Width="100px">
            <Template Context="item">
                @if (item.EndTime.HasValue)
                {
                    @item.EndTime.Value.ToString("HH:mm:ss")
                }
                else
                {
                    <span style="color: red; font-weight: bold;">активна</span>
                }
            </Template>
        </RadzenDataGridColumn>
        <RadzenDataGridColumn Title="Длительность" Width="100px">
            <Template Context="item">
                @GetDuration(item)
            </Template>
        </RadzenDataGridColumn>
        <RadzenDataGridColumn Property="Code" Title="Код" Width="80px" />
        <RadzenDataGridColumn Property="Description" Title="Описание" />
        <RadzenDataGridColumn Property="StepName" Title="Шаг" Width="120px">
            <Template Context="item">
                @(item.StepName ?? "—")
            </Template>
        </RadzenDataGridColumn>
    </Columns>
</RadzenDataGrid>

@code {
    private IReadOnlyList<ErrorHistoryItem> _history = [];

    protected override void OnInitialized()
    {
        ErrorService.OnHistoryChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        _history = ErrorService.GetHistory();
        InvokeAsync(StateHasChanged);
    }

    private string GetDuration(ErrorHistoryItem item)
    {
        var end = item.EndTime ?? DateTime.Now;
        var duration = end - item.StartTime;
        return duration.ToString(@"hh\:mm\:ss");
    }

    public void Dispose()
    {
        ErrorService.OnHistoryChanged -= Refresh;
    }
}
```

### 5.4 Обновить `Components/Errors/ErrorsTab.razor`

Интегрировать ActiveErrorsGrid и ErrorHistoryGrid.

### 5.5 Обновить `MyComponent.razor`

Заменить RadzenButton "Success" на `<ErrorResetButton />`.

---

## Сводка файлов

### Создать

| Файл | Описание |
|------|----------|
| `Models/Errors/ErrorDefinition.cs` | Модель определения ошибки |
| `Models/Errors/ErrorDefinitions.cs` | Справочник всех ошибок |
| `Services/Errors/IErrorService.cs` | Интерфейс сервиса ошибок |
| `Services/Errors/ErrorService.cs` | Реализация сервиса |
| `Services/Errors/IPlcErrorMonitorService.cs` | Интерфейс мониторинга ПЛК |
| `Services/Errors/PlcErrorMonitorService.cs` | Реализация мониторинга |
| `Components/Errors/ErrorResetButton.razor` | Кнопка сброса |
| `Components/Errors/ActiveErrorsGrid.razor` | Грид активных ошибок |
| `Components/Errors/ErrorHistoryGrid.razor` | Грид истории |

### Изменить

| Файл | Изменения |
|------|-----------|
| `Models/Errors/ActiveError.cs` | +StepId, +StepName, +Source |
| `Models/Errors/ErrorHistoryItem.cs` | +StepId, +StepName, +Source |
| `ErrorCoordinator.Interrupts.cs` | +IErrorService, +ClearActiveApplicationErrors |
| `PreExecutionCoordinator.Retry.cs` | +IErrorService, +ClearActiveApplicationErrors |
| `TestExecutionCoordinator.Execution.cs` | +IErrorService, +ClearActiveApplicationErrors |
| `ErrorsTab.razor` | Интегрировать гриды |
| `MyComponent.razor` | Заменить кнопку |
| DI регистрация | Добавить сервисы |

### Удалить

| Файл |
|------|
| `Services/Errors/IActiveErrorsService.cs` |
| `Services/Errors/ActiveErrorsService.cs` |
| `Services/Errors/IErrorHistoryService.cs` |
| `Services/Errors/ErrorHistoryService.cs` |

---

## Порядок реализации

1. **Модели** — ErrorDefinition.cs, обновить ActiveError.cs, ErrorHistoryItem.cs
2. **ErrorDefinitions.cs** — пока с несколькими примерами
3. **IErrorService + ErrorService** — основной сервис
4. **IPlcErrorMonitorService + PlcErrorMonitorService** — мониторинг ПЛК
5. **DI регистрация**
6. **UI компоненты** — кнопка, гриды
7. **Интеграция** — очистка в координаторах
8. **Удалить старые файлы**
9. **Заполнить ErrorDefinitions** реальными ошибками
10. **Тестирование**
