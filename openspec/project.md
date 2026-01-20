# Project Context

## Purpose
Система автоматизированного тестирования котельного оборудования (Final Test Hybrid).
Обеспечивает выполнение последовательности тестовых шагов для проверки котлов,
сбор данных с датчиков через OPC-UA, управление исполнительными механизмами,
формирование отчетов и сохранение результатов в базу данных.

## Tech Stack
- **.NET 10** - целевой фреймворк
- **WinForms** - основной UI фреймворк для desktop-приложения
- **Blazor Hybrid** - интеграция веб-компонентов в desktop-приложение
- **OPC-UA** - протокол для связи с ПЛК и промышленным оборудованием
- **PostgreSQL** - база данных для хранения результатов тестирования
- **Entity Framework Core** - ORM для работы с базой данных

## Project Conventions

### Code Style
- Язык: **C#** (последняя версия с .NET 10)
- Использование **partial classes** для разделения больших классов на логические части
- **async/await** для всех асинхронных операций
- Именование на английском языке (классы, методы, свойства)
- Комментарии и UI на русском языке
- Форматирование: стандартные правила C# (4 пробела для отступов)
- Не добавлять лишние комментарии и docstrings без необходимости

### Clean Code
- Читаемый, понятный код с осмысленными именами переменных, методов и классов
- Методы должны быть короткими и выполнять одну задачу (Single Responsibility)
- Избегать дублирования кода (DRY - Don't Repeat Yourself)
- Понятные абстракции, избегать магических чисел/строк
- Самодокументирующийся код - комментарии только там, где логика неочевидна

### Method & Class Design
- **Минимум control flow** - не больше одного управляющего оператора (if, for, while) на метод
- **Компактные функции** - небольшие методы с единой ответственностью, легко тестируемые
- **Обязательные блоки {}** - для всех конструкций (if, for, while, foreach)
- **Отдельные файлы** - record, class, enum, interface выносить в отдельные файлы
- **Лимит 300 строк** - максимум на класс, при превышении разбивать на partial classes по логике
- **Без оверинжиниринга** - не усложнять код без необходимости
- **Без лишнего защитного программирования** - не добавлять проверки на невозможные сценарии

### XML Documentation
- Все публичные методы должны иметь XML-документацию
- Приватные методы: только `<summary>`
- Публичные методы: `<summary>`, `<param>`, `<returns>`, `<exception>`
- Пример:
```csharp
/// <summary>
/// Краткое описание метода.
/// </summary>
/// <param name="paramName">Описание параметра.</param>
/// <returns>Описание возвращаемого значения.</returns>
/// <exception cref="ExceptionType">Когда выбрасывается.</exception>
```

### Logging
- Использовать **DualLogger** везде для логирования
- Логировать важные события, ошибки и состояния системы
- Формат: `_logger.Info/Debug/Error/Warn("сообщение")`

### Architecture Patterns
- **Strategy Pattern** - для сменных алгоритмов выполнения шагов и режимов сканирования
- **State Machine** - управление состояниями тестового процесса (Idle, Running, Paused, Retry, Finished)
- **WaitGroup Pattern** - синхронизация параллельных операций ожидания колонок/точек
- **Dependency Injection** - внедрение зависимостей через конструкторы и Microsoft.Extensions.DependencyInjection
- **Partial Classes** - разделение сервисов на логические файлы (например, StepTimingService разбит на .cs, .Columns.cs, .Scan.cs)

### Domain-Driven Design (DDD)
- **Ubiquitous Language** - единый язык домена в коде и коммуникации
- **Слои**: Domain, Application, Infrastructure, Presentation
- **Value Objects** - для неизменяемых концепций домена
- **Entities** - для объектов с идентичностью
- **Aggregates** - защищают инварианты домена
- **Repositories** - работают только с агрегатами
- Доменная логика должна быть в доменном слое, а не в сервисах или контроллерах

### Testing Strategy
- Функциональное тестирование через UI
- Интеграционное тестирование связи с OPC-UA
- Ручное тестирование на реальном оборудовании

### Git Workflow
- Фичи разрабатываются в отдельных ветках (например, `retry_end`)
- Коммиты на русском языке с кратким описанием изменений
- Мерж в основную ветку после проверки

## Domain Context

### Основные сущности
- **Step (Шаг)** - единица тестирования, содержит набор точек для проверки
- **Point (Точка)** - конкретный параметр для измерения/установки (температура, давление и т.д.)
- **Column (Колонка)** - логическая группа точек, связанных с одним агрегатом
- **Recipe (Рецепт)** - набор шагов для тестирования конкретного типа котла
- **ScanMode** - режим сканирования (Fixed, Scanning) для определения стабильности значений

### Состояния выполнения
- **Idle** - ожидание запуска
- **Running** - выполнение теста
- **Paused** - пауза
- **Retry** - повторная попытка после ошибки
- **Finished** - завершение

### Timing и Tolerance
- Каждый шаг имеет время выполнения и допустимые отклонения параметров
- Система отслеживает стабильность значений в пределах Tolerance
- Поддержка режима повторов (Retry) при сбоях

## Architecture

```
Program.cs → Form1.cs (DI) → BlazorWebView → Radzen UI

Excel → TestMapBuilder → TestMapResolver → TestMap
                                            ↓
                          TestExecutionCoordinator
                          ├── 4 × ColumnExecutor (parallel)
                          ├── ExecutionStateManager
                          └── ErrorCoordinator
```

### Step Execution Flow

```
[Barcode] → PreExecutionCoordinator → TestExecutionCoordinator → [OK/NOK]
                    │                           │
            ScanStep (10 steps)         4 × ColumnExecutor
            BlockBoilerAdapterStep      ExecuteMapOnAllColumns
                    │                           │
            StartTestExecution()        OnSequenceCompleted
            TryStartInBackground()→bool         │
                    │                   HandleTestCompleted()
            false → RollbackTestStart()
                    PipelineFailed
```

## Key Services & Coordinators

### Coordinators

| Сервис | Назначение |
|--------|------------|
| `TestExecutionCoordinator` | Основной координатор выполнения тестов |
| `PreExecutionCoordinator` | Фаза подготовки (сканирование, блокировка адаптера) |
| `ErrorCoordinator` | Обработка прерываний и ошибок |
| `PlcResetCoordinator` | Обработка сброса PLC (мягкий/жёсткий) |

### State Management

| Сервис | Назначение |
|--------|------------|
| `ExecutionStateManager` | Состояние выполнения теста |
| `ExecutionActivityTracker` | Отслеживание активности (pre-execution/test execution) |
| `BoilerState` | Состояние котла |
| `StepTimingService` | Таймеры для UI (scan, columns) |

### Pausable vs Non-Pausable Services

| Контекст | Сервис |
|----------|--------|
| Тестовые шаги | `PausableOpcUaTagService`, `PausableTagWaiter` |
| Системные операции | `OpcUaTagService`, `TagWaiter` |

## OPC-UA Layer

| Сервис | Назначение |
|--------|------------|
| `OpcUaConnectionService` | Session, auto-reconnect |
| `OpcUaSubscription` | Pub/sub, callbacks |
| `OpcUaTagService` | Read/write (`ReadResult<T>`, `WriteResult`) |
| `TagWaiter` | Multi-tag conditions |

## DI Patterns

| Паттерн | Пример |
|---------|--------|
| Extension chain | `AddFinalTestServices()` → `AddOpcUaServices()` |
| Singleton state | `ExecutionStateManager`, `BoilerState` |
| Pausable decorator | `PausableOpcUaTagService` wraps `OpcUaTagService` |
| DbContextFactory | `AddDbContextFactory<AppDbContext>()` |

## Test Step Interfaces

```
ITestStep ← IRequiresPlcSubscriptions, IRequiresRecipes, IHasPlcBlock
IScanBarcodeStep, IPreExecutionStep (отдельные)
```

## File Locations

| Category | Path |
|----------|------|
| Entry | `Program.cs`, `Form1.cs` |
| Components | `Components/{Engineer,Main,Overview}/` |
| Services | `Services/{OpcUa,Steps,Database}/` |
| Models | `Models/{Steps,Errors,Database}/` |
| Guides | `*.Guide.md` в корне проекта |

## Important Constraints
- Приложение работает на Windows (WinForms)
- Требуется подключение к OPC-UA серверу для работы с оборудованием
- Критична стабильность работы - система управляет реальным оборудованием
- Необходима поддержка работы в оффлайн-режиме (без OPC-UA) для отладки

## External Dependencies
- **OPC-UA Server** - связь с ПЛК и промышленными контроллерами
- **PostgreSQL Database** - хранение рецептов, результатов тестов, истории
- **Modbus** (опционально) - альтернативный протокол связи с оборудованием

## Safety Patterns

### Hang Protection

| Сценарий | Защита |
|----------|--------|
| Пустые Maps | `StartTestExecution()→false` + `RollbackTestStart()` → `PipelineFailed` |
| Двойной старт | `TryStartInBackground()→false`, состояние не меняется |
| Исключение в `OnSequenceCompleted` | `InvokeSequenceCompletedSafely()` — логирует, cleanup выполняется |

### TOCTOU Prevention

Захватывать поле в локальную переменную перед `await` или в event handler:
```csharp
var step = _state.FailedStep;  // Захват
if (step != null) { await ExecuteStepCoreAsync(step, ct); }
```

### CancellationToken Sync

| Событие | Отменить |
|---------|----------|
| Reset + AutoMode OFF | `_loopCts`, `_currentCts` |
| ForceStop | `_currentCts`, `_cts` |
| Logout | `_loopCts` |

## Accepted Patterns (NOT bugs)

| Паттерн | Почему OK |
|---------|-----------|
| `ExecutionStateManager.State` без Lock | Atomic enum, stale read OK для UI |
| `?.TrySetResult()` без синхронизации | Идемпотентна |
| Fire-and-forget в singleton | `.ContinueWith` или внутренний try-catch |
| `TryStartInBackground()` | Исключения в `RunWithErrorHandlingAsync` |
