
# Документация: Система параллельного выполнения тестов

## Оглавление

1. [Общая концепция](#1-общая-концепция)
2. [Архитектура системы](#2-архитектура-системы)
3. [Структура Excel-файла](#3-структура-excel-файла)
4. [Компоненты системы](#4-компоненты-системы)
5. [Процесс выполнения](#5-процесс-выполнения)
6. [Синхронизация и параллелизм](#6-синхронизация-и-параллелизм)
7. [Потокобезопасность](#7-потокобезопасность)
8. [Создание нового шага](#8-создание-нового-шага)
9. [Регистрация в DI](#9-регистрация-в-di)
10. [Статусы выполнения](#10-статусы-выполнения)
11. [Обработка ошибок](#11-обработка-ошибок)
12. [Диаграммы](#12-диаграммы)

---

## 1. Общая концепция

Система предназначена для параллельного выполнения тестовых последовательностей на 4 независимых каналах (колонках). Каждая колонка представляет отдельный тестовый канал, который может выполнять свои шаги независимо от других.

### Основные принципы:

1. **4 параллельных канала** - Каждый канал выполняет свою последовательность шагов
2. **Синхронизация через Map** - Все 4 канала синхронизируются на границах Map
3. **Автоматический переход** - После синхронизации автоматически начинается следующая Map
4. **Остановка при ошибке** - Ошибка в любом канале останавливает всю последовательность

### Что такое Map?

**Map (карта)** - это группа строк Excel между двумя разделителями `<TEST STEP>`. Все 4 колонки выполняют шаги внутри одной Map параллельно, затем синхронизируются и переходят к следующей Map.

```
Excel файл:
┌─────────┬─────────┬─────────┬─────────┐
│ Col1    │ Col2    │ Col3    │ Col4    │
├─────────┼─────────┼─────────┼─────────┤ ← Map 0 начало
│ StepA   │ StepD   │ StepG   │ StepJ   │
│ StepB   │         │ StepH   │         │
│ StepC   │         │         │         │
├─────────┼─────────┼─────────┼─────────┤
│<TEST>   │<TEST>   │<TEST>   │<TEST>   │ ← Синхронизация (конец Map 0)
├─────────┼─────────┼─────────┼─────────┤ ← Map 1 начало
│ StepX   │ StepY   │         │ StepZ   │
│         │         │         │         │
└─────────┴─────────┴─────────┴─────────┘ ← Map 1 конец
```

---

## 2. Архитектура системы

### Диаграмма компонентов

```
┌────────────────────────────────────────────────────────────────────────┐
│                           УРОВЕНЬ УПРАВЛЕНИЯ                            │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│  ┌──────────────────┐         ┌────────────────────────┐               │
│  │  ScanStepManager │────────►│ TestExecutionCoordinator│               │
│  │                  │         │                        │               │
│  │  • Сканирование  │         │  • Координация         │               │
│  │  • Валидация     │         │  • Синхронизация       │               │
│  │  • Старт теста   │         │  • Контроль ошибок     │               │
│  └──────────────────┘         └───────────┬────────────┘               │
│                                           │                            │
└───────────────────────────────────────────┼────────────────────────────┘
                                            │
┌───────────────────────────────────────────┼────────────────────────────┐
│                         УРОВЕНЬ ВЫПОЛНЕНИЯ │                            │
├───────────────────────────────────────────┼────────────────────────────┤
│                                           ▼                            │
│           ┌───────────────────────────────────────────────┐            │
│           │              ColumnExecutor[]                 │            │
│           │  ┌─────────┬─────────┬─────────┬─────────┐   │            │
│           │  │Executor0│Executor1│Executor2│Executor3│   │            │
│           │  │ Col 0   │ Col 1   │ Col 2   │ Col 3   │   │            │
│           │  └────┬────┴────┬────┴────┬────┴────┬────┘   │            │
│           └───────┼─────────┼─────────┼─────────┼────────┘            │
│                   │         │         │         │                      │
│                   ▼         ▼         ▼         ▼                      │
│           ┌───────────────────────────────────────────────┐            │
│           │            TestStepContext[]                  │            │
│           │  ┌─────────┬─────────┬─────────┬─────────┐   │            │
│           │  │Context0 │Context1 │Context2 │Context3 │   │            │
│           │  │Variables│Variables│Variables│Variables│   │            │
│           │  └─────────┴─────────┴─────────┴─────────┘   │            │
│           └───────────────────────────────────────────────┘            │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
                                            │
┌───────────────────────────────────────────┼────────────────────────────┐
│                           УРОВЕНЬ ШАГОВ   │                            │
├───────────────────────────────────────────┼────────────────────────────┤
│                                           ▼                            │
│           ┌───────────────────────────────────────────────┐            │
│           │              ITestStep (Singletons)           │            │
│           │                                               │            │
│           │  • CheckResistanceStep                        │            │
│           │  • MeasureVoltageStep                         │            │
│           │  • PrintLabelStep                             │            │
│           │  • WaitForSignalStep                          │            │
│           │  • ...                                        │            │
│           └───────────────────────────────────────────────┘            │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

### Ключевые классы

| Класс | Назначение | Расположение |
|-------|------------|--------------|
| `ScanStepManager` | Управление сканированием и запуском | `Services/Steps/Infrastructure/Execution/` |
| `TestExecutionCoordinator` | Координация 4 колонок | `Services/Steps/Infrastructure/Execution/` |
| `ColumnExecutor` | Выполнение шагов одной колонки | `Services/Steps/Infrastructure/Execution/` |
| `TestStepContext` | Контекст выполнения шага | `Services/Steps/Infrastructure/Registrator/` |
| `ITestStep` | Интерфейс шага | `Services/Steps/Infrastructure/Interaces/` |
| `TestMap` | Модель данных одной Map | `Models/Steps/` |
| `TestMapRow` | Одна строка (4 шага) | `Models/Steps/` |

---

## 3. Структура Excel-файла

### Формат файла

Excel-файл должен содержать 4 колонки, где каждая колонка — это последовательность шагов для одного канала.

```
┌───────────────┬───────────────┬───────────────┬───────────────┐
│    Column A   │    Column B   │    Column C   │    Column D   │
│   (Канал 0)   │   (Канал 1)   │   (Канал 2)   │   (Канал 3)   │
├───────────────┼───────────────┼───────────────┼───────────────┤
│ check-resist  │ measure-volt  │               │ print-label   │
│ wait-signal   │               │ check-resist  │               │
│               │ measure-volt  │ wait-signal   │ check-resist  │
├───────────────┼───────────────┼───────────────┼───────────────┤
│ <TEST STEP>   │ <TEST STEP>   │ <TEST STEP>   │ <TEST STEP>   │
├───────────────┼───────────────┼───────────────┼───────────────┤
│ final-check   │ final-check   │ final-check   │ final-check   │
└───────────────┴───────────────┴───────────────┴───────────────┘
```

### Правила заполнения

1. **Идентификаторы шагов** - В ячейках указываются ID шагов, зарегистрированных в `ITestStepRegistry`
2. **Пустые ячейки** - Означают, что канал пропускает эту строку
3. **Разделитель Map** - Строка `<TEST STEP>` во всех 4 колонках означает границу Map
4. **Выравнивание** - `<TEST STEP>` ОБЯЗАН быть во всех 4 колонках одной строки

### Примеры валидных и невалидных файлов

**Валидно:**
```
│ step-a   │ step-b   │          │ step-c   │  ← OK: пустая ячейка
│<TEST>    │<TEST>    │<TEST>    │<TEST>    │  ← OK: разделитель во всех
```

**Невалидно:**
```
│ step-a   │<TEST>    │ step-b   │ step-c   │  ← ОШИБКА: <TEST> не во всех колонках
```

---

## 4. Компоненты системы

### 4.1 TestExecutionCoordinator

**Файл:** `Services/Steps/Infrastructure/Execution/TestExecutionCoordinator.cs`

Главный координатор, который управляет 4 колонками и обеспечивает синхронизацию.

```csharp
public class TestExecutionCoordinator : IDisposable
{
    private const int ColumnCount = 4;
    private readonly ColumnExecutor[] _executors;
    private List<TestMap> _maps = [];
    private int _currentMapIndex;
    private CancellationTokenSource? _cts;
    private readonly Lock _stateLock = new();

    // События
    public event Action? OnStateChanged;        // При изменении состояния любого executor
    public event Action? OnSequenceCompleted;   // По завершении всей последовательности
    public event Action<string>? OnError;       // При ошибке

    // Свойства
    public IReadOnlyList<ColumnExecutor> Executors => _executors;
    public int CurrentMapIndex => _currentMapIndex;
    public int TotalMaps => _maps.Count;
    public bool IsRunning { get; private set; }
    public bool HasErrors => _executors.Any(e => e.HasFailed);

    // Методы
    public void SetMaps(List<TestMap> maps);    // Загрузить последовательность
    public async Task StartAsync();              // Запустить выполнение
    public void Stop();                          // Остановить выполнение
}
```

**Логика работы:**

1. `SetMaps()` - Загружает список Map и сбрасывает все executor'ы
2. `StartAsync()` - Запускает цикл по всем Map:
   - Для каждой Map запускает 4 executor'а параллельно через `Task.WhenAll`
   - Ждёт завершения всех 4 колонок
   - Если есть ошибки - останавливается
   - Иначе переходит к следующей Map
3. `Stop()` - Отменяет выполнение через `CancellationToken`

### 4.2 ColumnExecutor

**Файл:** `Services/Steps/Infrastructure/Execution/ColumnExecutor.cs`

Выполняет шаги одной колонки последовательно.

```csharp
public class ColumnExecutor
{
    public int ColumnIndex { get; }
    public string? CurrentStepName { get; private set; }
    public string? CurrentStepDescription { get; private set; }
    public string Status { get; private set; } = "Ожидание";
    public string? ErrorMessage { get; private set; }
    public string? ResultValue { get; private set; }
    public bool HasFailed { get; private set; }

    private readonly TestStepContext _context;
    private readonly ILogger _logger;

    public event Action? OnStateChanged;

    // Выполнить все шаги Map для этой колонки
    public async Task ExecuteMapAsync(TestMap map, CancellationToken ct);

    // Сбросить состояние
    public void Reset();
}
```

**Логика работы:**

1. Получает Map и итерирует по строкам (`TestMapRow`)
2. Для каждой строки берёт шаг своей колонки: `row.Steps[ColumnIndex]`
3. Если шаг `null` - пропускает (статус "Пропуск")
4. Если шаг есть - выполняет через `step.ExecuteAsync(_context, ct)`
5. При успехе - переходит к следующему шагу
6. При ошибке - устанавливает `HasFailed = true` и прекращает выполнение

### 4.3 TestStepContext

**Файл:** `Services/Steps/Infrastructure/Registrator/TestStepContext.cs`

Контекст, передаваемый в каждый шаг при выполнении.

```csharp
public class TestStepContext(int columnIndex, OpcUaTagService opcUa, ILogger logger)
{
    public int ColumnIndex { get; } = columnIndex;          // Индекс колонки (0-3)
    public OpcUaTagService OpcUa { get; } = opcUa;          // Доступ к OPC UA тегам
    public ILogger Logger { get; } = logger;                // Логгер
    public Dictionary<string, object> Variables { get; } = []; // Переменные между шагами
}
```

**Важно:** Каждая колонка имеет СВОЙ экземпляр `TestStepContext` с отдельным `Variables`. Это обеспечивает изоляцию данных между колонками.

### 4.4 ITestStep

**Файл:** `Services/Steps/Infrastructure/Interaces/ITestStep.cs`

Интерфейс, который должен реализовать каждый шаг.

```csharp
public interface ITestStep
{
    string Id { get; }                    // Уникальный идентификатор
    string Name { get; }                  // Отображаемое имя
    string Description { get; }           // Описание шага

    Task<TestStepResult> ExecuteAsync(
        TestStepContext context,
        CancellationToken ct);
}
```

---

## 5. Процесс выполнения

### Полный цикл работы

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         ПОЛНЫЙ ЦИКЛ ВЫПОЛНЕНИЯ                          │
└─────────────────────────────────────────────────────────────────────────┘

1. ИНИЦИАЛИЗАЦИЯ
   ┌──────────────┐
   │ Form1.cs     │──► Регистрация TestExecutionCoordinator в DI
   │              │──► Создание ScanStepManager
   └──────────────┘

2. ОЖИДАНИЕ ОПЕРАТОРА
   ┌──────────────┐     ┌──────────────┐
   │ Оператор     │────►│ OperatorState │──► IsAuthenticated = true
   │ логинится    │     └──────────────┘
   └──────────────┘            │
                               ▼
   ┌──────────────┐     ┌──────────────┐
   │ AutoReady    │────►│ ScanStepManager│──► Активация режима сканирования
   │ IsReady=true │     └──────────────┘
   └──────────────┘

3. СКАНИРОВАНИЕ ШТРИХКОДА
   ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
   │ Сканер       │────►│ RawInputService│────►│ScanStepManager│
   │ (штрихкод)   │     │              │     │              │
   └──────────────┘     └──────────────┘     └───────┬──────┘
                                                     │
                                                     ▼
                                             ProcessBarcodeAsync()
                                                     │
                                                     ▼
                                             Валидация штрихкода
                                             Загрузка Maps из рецепта

4. ЗАПУСК ВЫПОЛНЕНИЯ
   ┌──────────────────────────────────────────────────────────────────────┐
   │ ScanStepManager.StartExecution()                                     │
   │                                                                      │
   │   coordinator.SetMaps(maps);     // Загрузить Maps                   │
   │   await coordinator.StartAsync(); // Запустить                       │
   └──────────────────────────────────────────────────────────────────────┘

5. ВЫПОЛНЕНИЕ MAP (для каждой Map)
   ┌──────────────────────────────────────────────────────────────────────┐
   │ TestExecutionCoordinator.ExecuteCurrentMap()                         │
   │                                                                      │
   │   ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐       │
   │   │ Executor 0 │ │ Executor 1 │ │ Executor 2 │ │ Executor 3 │       │
   │   │    ▼       │ │    ▼       │ │    ▼       │ │    ▼       │       │
   │   │  Step 1    │ │  Step 1    │ │  Step 1    │ │  Step 1    │       │
   │   │  Step 2    │ │  Step 2    │ │  (пусто)   │ │  Step 2    │       │
   │   │  Step 3    │ │  (конец)   │ │  Step 2    │ │  (конец)   │       │
   │   │  (конец)   │ │            │ │  (конец)   │ │            │       │
   │   └────────────┘ └────────────┘ └────────────┘ └────────────┘       │
   │         │              │              │              │               │
   │         └──────────────┴──────────────┴──────────────┘               │
   │                              │                                       │
   │                    Task.WhenAll() ← Синхронизация                    │
   │                              │                                       │
   │                              ▼                                       │
   │                    HasErrors? ──► Да ──► Остановка                   │
   │                              │                                       │
   │                              ▼ Нет                                   │
   │                    Следующая Map                                     │
   └──────────────────────────────────────────────────────────────────────┘

6. ЗАВЕРШЕНИЕ
   ┌──────────────────────────────────────────────────────────────────────┐
   │ TestExecutionCoordinator.FinalizeExecution()                         │
   │                                                                      │
   │   OnSequenceCompleted?.Invoke();                                     │
   │                                                                      │
   │   ┌──────────────┐                                                   │
   │   │ScanStepManager│──► HandleSequenceCompleted()                     │
   │   │              │       │                                           │
   │   │              │       ├──► Если ошибки: ShowError()               │
   │   │              │       └──► Если успех: ShowSuccess()              │
   │   │              │                                                   │
   │   │              │──► UnblockInput() ──► Готов к новому сканированию │
   │   └──────────────┘                                                   │
   └──────────────────────────────────────────────────────────────────────┘
```

---

## 6. Синхронизация и параллелизм

### Механизм синхронизации

Синхронизация происходит на границах Map через `Task.WhenAll`:

```csharp
// TestExecutionCoordinator.ExecuteCurrentMap()
private async Task ExecuteCurrentMap()
{
    var map = _maps[_currentMapIndex];

    // Запуск всех 4 колонок параллельно
    var tasks = _executors.Select(e => e.ExecuteMapAsync(map, _cts!.Token));

    // Ожидание завершения ВСЕХ колонок
    await Task.WhenAll(tasks);

    // Только после этого переход к следующей Map
}
```

### Пример выполнения

```
Время →

Map 0:
┌────────┬────────┬────────┬────────┐
│ Col 0  │ Col 1  │ Col 2  │ Col 3  │
├────────┼────────┼────────┼────────┤
│ Step A │ Step D │        │ Step G │  t=0
│   ↓    │   ↓    │  skip  │   ↓    │
│ Step B │   ✓    │        │   ✓    │  t=1
│   ↓    │  wait  │        │  wait  │
│ Step C │  wait  │        │  wait  │  t=2
│   ↓    │  wait  │        │  wait  │
│   ✓    │  wait  │        │  wait  │  t=3
└────────┴────────┴────────┴────────┘
         │
         ▼ Task.WhenAll() ← Все завершили Map 0

Map 1:   │
┌────────┼────────┬────────┬────────┐
│ Col 0  │ Col 1  │ Col 2  │ Col 3  │
├────────┼────────┼────────┼────────┤
│ Step X │ Step Y │ Step Z │        │  t=4
...
```

### Точки синхронизации

1. **Начало Map** - Все 4 колонки начинают одновременно
2. **Конец Map** - Быстрые колонки ждут медленные через `Task.WhenAll`
3. **Ошибка** - При ошибке в любой колонке вся последовательность останавливается

---

## 7. Потокобезопасность

### Архитектура потокобезопасности

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     ПОТОКОБЕЗОПАСНАЯ АРХИТЕКТУРА                        │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Shared Resources (Thread-Safe):                                        │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │  OpcUaTagService  ─── Потокобезопасный (внутренняя синхронизация) │ │
│  │  ILogger          ─── Потокобезопасный (Serilog)                  │ │
│  │  ITestStep        ─── Stateless Singleton (без состояния)         │ │
│  └───────────────────────────────────────────────────────────────────┘ │
│                                                                         │
│  Isolated Resources (Per-Column):                                       │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐                       │
│  │Context 0│ │Context 1│ │Context 2│ │Context 3│                       │
│  │Variables│ │Variables│ │Variables│ │Variables│                       │
│  └─────────┘ └─────────┘ └─────────┘ └─────────┘                       │
│       │           │           │           │                            │
│       ▼           ▼           ▼           ▼                            │
│  Sequential   Sequential   Sequential   Sequential                     │
│  Execution    Execution    Execution    Execution                      │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Почему Variables НЕ нужна потокобезопасность?

`Dictionary<string, object> Variables` в `TestStepContext` НЕ является потокобезопасным, но это **не проблема** по следующим причинам:

1. **Изолированность** - Каждая колонка имеет СВОЙ экземпляр `TestStepContext`
2. **Последовательность** - Шаги внутри одной колонки выполняются ПОСЛЕДОВАТЕЛЬНО
3. **Нет пересечений** - Колонка 0 никогда не обращается к Variables колонки 1

```csharp
// Создание изолированных контекстов
private static ColumnExecutor CreateExecutor(int index, OpcUaTagService opcUa, ILoggerFactory loggerFactory)
{
    // Каждый executor получает СВОЙ context
    var context = new TestStepContext(index, opcUa, loggerFactory.CreateLogger($"Column{index}"));
    return new ColumnExecutor(index, context, ...);
}
```

### Stateless Steps

Шаги (`ITestStep`) - это **stateless singletons**. Они не хранят состояние между вызовами:

```csharp
// ПРАВИЛЬНО - Stateless
public class CheckResistanceStep : ITestStep
{
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        // Всё состояние в context или локальных переменных
        var value = await context.OpcUa.ReadAsync("resistance");
        return TestStepResult.Pass($"{value} Ohm");
    }
}

// НЕПРАВИЛЬНО - Stateful (НЕ ДЕЛАТЬ ТАК!)
public class BadStep : ITestStep
{
    private int _counter;  // ← ОПАСНО! Shared state между колонками!

    public async Task<TestStepResult> ExecuteAsync(...)
    {
        _counter++;  // Race condition при параллельном вызове!
    }
}
```

### Синхронизация координатора

`TestExecutionCoordinator` использует `Lock` для защиты изменения состояния:

```csharp
private readonly Lock _stateLock = new();

private bool TryStartExecution()
{
    lock (_stateLock)
    {
        if (IsRunning) return false;
        IsRunning = true;
        _cts = new CancellationTokenSource();
        return true;
    }
}

public void Stop()
{
    lock (_stateLock)
    {
        if (!IsRunning) return;
        _cts?.Cancel();
    }
}
```

---

## 8. Создание нового шага

### Шаблон шага

```csharp
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps;

[StepRegistration("my-step-id")]  // Атрибут для автоматической регистрации
public class MyCustomStep : ITestStep
{
    public string Id => "my-step-id";
    public string Name => "Мой шаг";
    public string Description => "Описание того, что делает шаг";

    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        // 1. Получение данных из context
        var columnIndex = context.ColumnIndex;
        var logger = context.Logger;

        // 2. Чтение/запись OPC UA тегов
        var value = await context.OpcUa.ReadAsync($"Tag_{columnIndex}");

        // 3. Использование Variables для передачи данных между шагами
        if (context.Variables.TryGetValue("previousValue", out var prev))
        {
            // Использовать значение из предыдущего шага
        }
        context.Variables["currentValue"] = value;

        // 4. Проверка отмены
        if (ct.IsCancellationRequested)
        {
            return TestStepResult.Cancelled();
        }

        // 5. Возврат результата
        if (value < 0)
        {
            return TestStepResult.Fail("Значение отрицательное");
        }

        return TestStepResult.Pass($"Значение: {value}");
    }
}
```

### Правила написания шагов

1. **Без состояния** - Не храните данные в полях класса
2. **Используйте context** - Все данные через `TestStepContext`
3. **Проверяйте CancellationToken** - Для корректной отмены
4. **Логируйте** - Используйте `context.Logger`
5. **Возвращайте понятные сообщения** - Для отображения в UI

---

## 9. Регистрация в DI

### Form1.cs

```csharp
// Регистрация координатора и связанных сервисов
services.AddSingleton<ITestStepRegistry, TestStepRegistry>();
services.AddSingleton<ITestSequenceLoader, TestSequenceLoader>();
services.AddSingleton<ITestMapBuilder, TestMapBuilder>();
services.AddSingleton<ITestMapResolver, TestMapResolver>();
services.AddSingleton<TestExecutionCoordinator>();
services.AddSingleton<ScanStepManager>();

// Получение и инициализация
_ = _serviceProvider.GetRequiredService<ScanStepManager>();
```

### Жизненный цикл

- `TestExecutionCoordinator` - **Singleton**, создаётся один раз при старте
- `ColumnExecutor[]` - Создаются внутри координатора
- `TestStepContext[]` - Создаются для каждого executor'а
- `ITestStep` - **Singleton**, stateless

---

## 10. Статусы выполнения

### Статусы ColumnExecutor

| Статус | Описание |
|--------|----------|
| `Ожидание` | Начальное состояние или ожидание синхронизации |
| `Выполняется` | Шаг выполняется |
| `Готово` | Шаг успешно завершён |
| `Пропуск` | Пустая ячейка, шаг пропущен |
| `Пропущен` | Шаг пропущен по условию (result.Skipped) |
| `Ошибка` | Шаг завершился с ошибкой |
| `Отменён` | Выполнение отменено |

### UI отображение

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Шаг                 │ Описание                │ Статус      │ Результат  │
├──────────────────────────────────────────────────────────────────────────┤
│ Проверка R          │ Проверяет сопротивление │ ▶ Выполн... │            │ ← Col 0
│ Измерение U         │ Измеряет напряжение     │ ✓ Готово    │ 12.5V      │ ← Col 1
│ (пусто)             │                         │ ─ Пропуск   │            │ ← Col 2
│ Печать этикетки     │ Печатает на принтере    │ ✗ Ошибка    │ Нет бумаги │ ← Col 3
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 11. Обработка ошибок

### Уровни обработки ошибок

1. **Уровень шага** - `ITestStep.ExecuteAsync()` может вернуть `TestStepResult.Fail(message)`
2. **Уровень executor'а** - `ColumnExecutor` ловит исключения и устанавливает `HasFailed = true`
3. **Уровень координатора** - `TestExecutionCoordinator` проверяет `HasErrors` после каждой Map
4. **Уровень менеджера** - `ScanStepManager` обрабатывает `OnSequenceCompleted` и показывает уведомление

### Поведение при ошибке

```
Map 0:
┌────────┬────────┬────────┬────────┐
│ Col 0  │ Col 1  │ Col 2  │ Col 3  │
├────────┼────────┼────────┼────────┤
│ Step A │ Step D │ Step G │ Step J │
│   ✓    │   ✓    │   ✗    │   ✓    │  ← Ошибка в Col 2
│ Step B │ Step E │  STOP  │ Step K │
│   ✓    │   ✓    │        │   ✓    │
│ Step C │  STOP  │        │  STOP  │  ← Все останавливаются
└────────┴────────┴────────┴────────┘
         │
         ▼
    HasErrors = true
         │
         ▼
  OnSequenceCompleted()
         │
         ▼
  ShowError("Выполнение прервано из-за ошибки")
```

### Уведомления пользователю

```csharp
// ScanStepManager.HandleSequenceCompleted()
private void HandleSequenceCompleted()
{
    UnblockInput();

    if (_coordinator.HasErrors)
    {
        _notificationService.ShowError("Тест завершён", "Выполнение прервано из-за ошибки");
        return;
    }

    _notificationService.ShowSuccess("Тест завершён", "Все шаги выполнены успешно");
}
```

---

## 12. Диаграммы

### Диаграмма классов

```
┌───────────────────────────────────────────────────────────────────────────┐
│                           CLASS DIAGRAM                                    │
└───────────────────────────────────────────────────────────────────────────┘

┌─────────────────────┐         ┌─────────────────────┐
│    ScanStepManager  │────────►│TestExecutionCoordinator│
├─────────────────────┤         ├─────────────────────┤
│ - _coordinator      │         │ - _executors[4]     │
│ - _processLock      │         │ - _maps             │
│ + IsProcessing      │         │ - _cts              │
├─────────────────────┤         │ - _stateLock        │
│ + ProcessBarcodeAsync()│      │ + Executors         │
│ + HandleSequenceCompleted()│  │ + IsRunning         │
└─────────────────────┘         │ + HasErrors         │
                                ├─────────────────────┤
                                │ + SetMaps()         │
                                │ + StartAsync()      │
                                │ + Stop()            │
                                └──────────┬──────────┘
                                           │
                                           │ owns
                                           ▼
                                ┌─────────────────────┐
                                │   ColumnExecutor    │
                                ├─────────────────────┤
                                │ + ColumnIndex       │
                                │ + CurrentStepName   │
                                │ + Status            │
                                │ + HasFailed         │
                                │ - _context          │
                                ├─────────────────────┤
                                │ + ExecuteMapAsync() │
                                │ + Reset()           │
                                └──────────┬──────────┘
                                           │
                                           │ uses
                                           ▼
                                ┌─────────────────────┐
                                │   TestStepContext   │
                                ├─────────────────────┤
                                │ + ColumnIndex       │
                                │ + OpcUa             │
                                │ + Logger            │
                                │ + Variables         │
                                └──────────┬──────────┘
                                           │
                                           │ passed to
                                           ▼
                                ┌─────────────────────┐
                                │     <<interface>>   │
                                │      ITestStep      │
                                ├─────────────────────┤
                                │ + Id                │
                                │ + Name              │
                                │ + Description       │
                                ├─────────────────────┤
                                │ + ExecuteAsync()    │
                                └─────────────────────┘
```

### Sequence Diagram - Успешное выполнение

```
┌──────────┐ ┌───────────────┐ ┌────────────────────────┐ ┌──────────────┐
│ Operator │ │ScanStepManager│ │TestExecutionCoordinator│ │ColumnExecutor│
└────┬─────┘ └───────┬───────┘ └───────────┬────────────┘ └──────┬───────┘
     │               │                     │                     │
     │  Scan barcode │                     │                     │
     │──────────────►│                     │                     │
     │               │                     │                     │
     │               │  SetMaps(maps)      │                     │
     │               │────────────────────►│                     │
     │               │                     │                     │
     │               │  StartAsync()       │                     │
     │               │────────────────────►│                     │
     │               │                     │                     │
     │               │                     │  ExecuteMapAsync()  │
     │               │                     │────────────────────►│ x4 parallel
     │               │                     │                     │
     │               │                     │    OnStateChanged   │
     │               │                     │◄────────────────────│
     │               │                     │                     │
     │               │                     │  Task.WhenAll()     │
     │               │                     │◄────────────────────│
     │               │                     │                     │
     │               │  OnSequenceCompleted│                     │
     │               │◄────────────────────│                     │
     │               │                     │                     │
     │  Show success │                     │                     │
     │◄──────────────│                     │                     │
     │               │                     │                     │
```
