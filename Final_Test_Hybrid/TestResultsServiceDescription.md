# TestResultsService

Сервис для накопления результатов тестов во время выполнения тестовой последовательности.

## Обзор

```
ITestResultsService (singleton)
├── Add()        - добавить результат
├── Clear()      - очистить все результаты
├── GetResults() - получить snapshot
└── OnChanged    - событие изменения
```

## Использование в тестовых шагах

```csharp
public class SomeTestStep(ITestResultsService resultsService) : ITestStep
{
    public async Task<TestStepResult> ExecuteAsync(...)
    {
        // Выполнение измерения
        var measuredValue = await MeasureSomething();

        // Сохранение результата
        resultsService.Add(
            parameterName: "Давление газа",
            value: measuredValue.ToString("F2"),
            tolerances: "18-25",
            unit: "мбар"
        );

        return TestStepResult.Success;
    }
}
```

## Очистка результатов

Вызывать в начале новой тестовой сессии:

```csharp
public class PreExecutionCoordinator(ITestResultsService resultsService)
{
    public async Task StartNewSession()
    {
        resultsService.Clear();
        // ...
    }
}
```

## UI компоненты

| Компонент | Путь | Назначение |
|-----------|------|------------|
| `TestResultsGrid` | `Components/Results/` | Грид с результатами тестов |
| `TestResultsTab` | `Components/Results/` | Вкладка: результаты + история ошибок |

### Подключение в UI

```razor
@* В любом компоненте *@
<TestResultsTab />

@* Или только грид результатов *@
<TestResultsGrid />
```

## Модель данных

```csharp
public record TestResultItem
{
    public Guid Id { get; init; }
    public DateTime Time { get; init; }
    public string ParameterName { get; init; }
    public string Value { get; init; }
    public string Tolerances { get; init; }
    public string Unit { get; init; }
}
```

## Потокобезопасность

- Сервис thread-safe (использует `Lock`)
- `GetResults()` возвращает копию списка
- `OnChanged` вызывается вне lock (безопасно для UI)

## Паттерн подписки в Blazor

```csharp
@inject ITestResultsService ResultsService
@implements IDisposable

@code {
    private bool _disposed;
    private IReadOnlyList<TestResultItem> _results = [];

    protected override void OnInitialized()
    {
        ResultsService.OnChanged += OnResultsChanged;
        _results = ResultsService.GetResults();
    }

    private void OnResultsChanged()
    {
        _ = InvokeAsync(() =>
        {
            if (_disposed) return;
            _results = ResultsService.GetResults();
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        _disposed = true;
        ResultsService.OnChanged -= OnResultsChanged;
    }
}
```

## DI регистрация

Уже зарегистрирован в `StepsServiceExtensions.cs`:

```csharp
services.AddSingleton<ITestResultsService, TestResultsService>();
```
